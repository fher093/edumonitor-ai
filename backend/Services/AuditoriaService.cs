using System.Text.RegularExpressions;
using Backend.Data;
using Backend.Domain;

namespace Backend.Services;

/// <summary>Datos de un evento de auditoría antes de guardarlo.</summary>
public sealed record EventoAuditoria(
    string Categoria,
    string Accion,
    string Resultado,
    int? CodigoHttp = null,
    string? Recurso = null,
    string? Detalle = null,
    int? UsuarioId = null,
    string? UsuarioCorreo = null,
    string? Rol = null,
    string? Ip = null);

/// <summary>
/// Registro de auditoría (HU-17 / RF-12). Escribe en un contexto propio para no mezclar el registro
/// con los cambios de la petición, y nunca interrumpe la petición si el registro falla.
/// </summary>
public sealed partial class AuditoriaService(
    IServiceScopeFactory scopes,
    TimeProvider reloj,
    ILogger<AuditoriaService> logger)
{
    /// <summary>Herramientas válidas del agente (cabecera X-Herramienta-Agente).</summary>
    public static readonly IReadOnlySet<string> HerramientasAgente = new HashSet<string>
    {
        "listar_cursos_autorizados",
        "obtener_calificaciones",
        "obtener_progreso",
        "identificar_alertas",
        "generar_resumen"
    };

    public async Task RegistrarAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RegistrosAuditoria.Add(new RegistroAuditoria
            {
                Fecha = reloj.GetUtcNow().UtcDateTime,
                UsuarioId = evento.UsuarioId,
                UsuarioCorreo = Recortar(evento.UsuarioCorreo, 254),
                Rol = Recortar(evento.Rol, 40),
                Categoria = Recortar(evento.Categoria, 20) ?? CategoriasAuditoria.Consulta,
                Accion = Recortar(evento.Accion, 80) ?? "desconocida",
                Recurso = Recortar(evento.Recurso, 200),
                Detalle = Recortar(Sanitizar(evento.Detalle), 500),
                Resultado = Recortar(evento.Resultado, 20) ?? "exito",
                CodigoHttp = evento.CodigoHttp,
                Ip = Recortar(evento.Ip, 45)
            });
            // CancellationToken.None: el registro se completa aunque el cliente cierre la conexión.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo registrar el evento de auditoría {Accion}.", evento.Accion);
        }
    }

    public static string ResultadoDesdeCodigo(int codigo) => codigo switch
    {
        >= 200 and < 300 => "exito",
        401 or 403 => "denegado",
        404 => "no_encontrado",
        400 or 409 or 422 => "rechazado",
        _ => "error"
    };

    /// <summary>
    /// Defensa adicional: elimina cualquier cosa con forma de secreto (JWT, cabecera Bearer,
    /// pares clave=valor de contraseñas o tokens) aunque un llamador la pase por error.
    /// </summary>
    public static string? Sanitizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto;
        var limpio = JwtRegex().Replace(texto, "[token omitido]");
        limpio = BearerRegex().Replace(limpio, "Bearer [omitido]");
        limpio = SecretoRegex().Replace(limpio, m => $"{m.Groups[1].Value}=[omitido]");
        return limpio;
    }

    private static string? Recortar(string? valor, int maximo) =>
        valor is null ? null : valor.Length <= maximo ? valor : valor[..maximo];

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"\b(password|contrasena|contraseña|clave|token|secret|secreto)\s*[=:]\s*\S+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretoRegex();
}
