using System.Net.Http.Headers;
using System.Text.Json;
using AIService.Agente;

namespace AIService.Clientes;

/// <summary>
/// Cliente de solo lectura de la API del backend. Reenvía el JWT del docente, de modo que las herramientas
/// del agente tienen exactamente los mismos permisos que el docente (RN-12), y marca cada petición con la
/// herramienta usada para que quede en la auditoría (HU-17).
/// </summary>
public sealed class BackendClient(HttpClient http, ILogger<BackendClient> logger) : IBackendAcademico
{
    public const string CabeceraHerramienta = "X-Herramienta-Agente";
    public const string CabeceraConsulta = "X-Consulta-Agente";

    public async Task<RespuestaBackend> GetAsync(
        string ruta, string herramienta, ContextoConsulta contexto, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ruta.TrimStart('/'));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", contexto.Token);
        req.Headers.Add(CabeceraHerramienta, herramienta);
        req.Headers.Add(CabeceraConsulta, contexto.ConsultaId);

        try
        {
            using var resp = await http.SendAsync(req, ct);
            var codigo = (int)resp.StatusCode;
            if (!resp.IsSuccessStatusCode) return new RespuestaBackend(codigo, null);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return new RespuestaBackend(codigo, doc.RootElement.Clone());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("La herramienta {Herramienta} no pudo consultar el backend: {Error}", herramienta, ex.Message);
            return new RespuestaBackend(503, null);
        }
    }
}
