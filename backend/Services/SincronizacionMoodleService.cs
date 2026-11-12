using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class MoodleServiceException(string mensaje) : Exception(mensaje);

/// <summary>Cliente del microservicio MoodleService.</summary>
public interface IMoodleServiceClient
{
    Task<ExtraccionMoodleDto> ExtraerAsync(string bearerToken, IReadOnlyCollection<long>? cursos, CancellationToken ct);

    /// <summary>Foto de perfil desde Moodle (null si no existe o MoodleService no la entrega).</summary>
    Task<(byte[] Datos, string Tipo)?> ObtenerFotoAsync(string bearerToken, string ruta, CancellationToken ct);
}

public class MoodleServiceHttpClient(HttpClient http) : IMoodleServiceClient
{
    public async Task<(byte[] Datos, string Tipo)?> ObtenerFotoAsync(string bearerToken, string ruta, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"api/moodle/foto?ruta={Uri.EscapeDataString(ruta)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        try
        {
            using var resp = await http.SendAsync(req, ct);
            var tipo = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!resp.IsSuccessStatusCode || !tipo.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
            return (await resp.Content.ReadAsByteArrayAsync(ct), tipo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<ExtraccionMoodleDto> ExtraerAsync(
        string bearerToken, IReadOnlyCollection<long>? cursos, CancellationToken ct)
    {
        var url = "api/moodle/extraccion" + (cursos is { Count: > 0 } ? $"?cursos={string.Join(',', cursos)}" : "");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new MoodleServiceException(
                $"El microservicio de Moodle no está disponible ({http.BaseAddress}). Verifica que MoodleService esté en ejecución.");
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                string? mensaje = null;
                try { mensaje = (await resp.Content.ReadFromJsonAsync<ErrorResponse>(ct))?.Message; }
                catch (JsonException) { /* cuerpo no JSON */ }
                throw new MoodleServiceException(mensaje ?? $"MoodleService respondió HTTP {(int)resp.StatusCode}.");
            }

            return await resp.Content.ReadFromJsonAsync<ExtraccionMoodleDto>(ct)
                   ?? throw new MoodleServiceException("MoodleService devolvió una respuesta vacía.");
        }
    }
}

/// <summary>
/// Orquesta la sincronización (RF-03): pide los datos a MoodleService, los importa y registra
/// fecha, resultado, contadores y errores en SincronizacionesMoodle.
/// </summary>
public class SincronizacionMoodleService(
    AppDbContext db,
    IMoodleServiceClient cliente,
    MoodleImportador importador,
    TimeProvider clock,
    ILogger<SincronizacionMoodleService> logger)
{
    private static readonly SemaphoreSlim EnEjecucion = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool EstaEnEjecucion => EnEjecucion.CurrentCount == 0;

    public async Task<SincronizacionMoodleDto?> EjecutarAsync(
        int? usuarioId, string? usuarioCorreo, string bearerToken, IReadOnlyCollection<long>? cursos, CancellationToken ct)
    {
        if (!await EnEjecucion.WaitAsync(0, ct)) return null;
        try
        {
            var registro = new SincronizacionMoodle
            {
                FechaInicio = clock.GetUtcNow().UtcDateTime,
                Estado = EstadosSincronizacion.EnCurso,
                UsuarioId = usuarioId,
                UsuarioCorreo = usuarioCorreo
            };
            db.SincronizacionesMoodle.Add(registro);
            await db.SaveChangesAsync(ct);

            List<DetalleSincronizacionDto> detalle;
            try
            {
                var datos = await cliente.ExtraerAsync(bearerToken, cursos, ct);
                registro.UrlMoodle = datos.UrlMoodle;

                var r = await importador.ImportarAsync(datos, ct);
                registro.CursosImportados = r.CursosImportados;
                registro.CursosOmitidos = r.CursosOmitidos;
                registro.EstudiantesImportados = r.Estudiantes.Count;
                registro.MatriculasActivas = r.MatriculasActivas;
                registro.MatriculasRetiradas = r.MatriculasRetiradas;
                registro.ActividadesImportadas = r.ActividadesImportadas;
                registro.CalificacionesImportadas = r.CalificacionesImportadas;
                detalle = r.Detalle;

                var errores = detalle.Count(d => d.Tipo == "error");
                var advertencias = detalle.Count(d => d.Tipo == "advertencia");
                registro.Estado = datos.Cursos.Count > 0 && r.CursosImportados == 0
                    ? EstadosSincronizacion.Fallida
                    : errores + advertencias > 0 ? EstadosSincronizacion.ConAdvertencias : EstadosSincronizacion.Exitosa;
                registro.Mensaje = datos.Cursos.Count == 0
                    ? "Moodle no devolvió cursos visibles para el usuario técnico."
                    : $"{r.CursosImportados} de {datos.Cursos.Count} curso(s) sincronizados desde {datos.Sitio} (Moodle {datos.VersionMoodle})." +
                      (errores > 0 ? $" {errores} error(es)." : "") +
                      (advertencias > 0 ? $" {advertencias} advertencia(s)." : "");
            }
            catch (MoodleServiceException ex)
            {
                db.ChangeTracker.Clear();
                db.SincronizacionesMoodle.Attach(registro);
                registro.Estado = EstadosSincronizacion.Fallida;
                registro.Mensaje = Recortar(ex.Message, 500);
                detalle = [new DetalleSincronizacionDto("error", null, ex.Message)];
                logger.LogWarning("Sincronización con Moodle fallida: {Mensaje}", ex.Message);
            }

            registro.FechaFin = clock.GetUtcNow().UtcDateTime;
            registro.DetalleJson = JsonSerializer.Serialize(detalle, Json);
            await db.SaveChangesAsync(ct);
            return ADto(registro);
        }
        finally
        {
            EnEjecucion.Release();
        }
    }

    public static SincronizacionMoodleDto ADto(SincronizacionMoodle s) => new(
        s.Id, s.FechaInicio, s.FechaFin, s.Estado, s.UsuarioCorreo, s.UrlMoodle,
        s.CursosImportados, s.CursosOmitidos, s.EstudiantesImportados, s.MatriculasActivas, s.MatriculasRetiradas,
        s.ActividadesImportadas, s.CalificacionesImportadas, s.Mensaje,
        string.IsNullOrEmpty(s.DetalleJson)
            ? []
            : JsonSerializer.Deserialize<List<DetalleSincronizacionDto>>(s.DetalleJson, Json) ?? []);

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
