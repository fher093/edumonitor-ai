using Backend.Domain;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Backend.Controllers;

/// <summary>
/// Registra la acción en la auditoría (HU-17) con el usuario, el recurso (ids de la ruta) y el resultado.
/// Si la petición viene del agente de IA (cabecera X-Herramienta-Agente con una herramienta válida),
/// se registra en la categoría "agente" con la herramienta usada.
/// Un controlador puede añadir un detalle no sensible con <see cref="AgregarDetalle"/>.
/// Nunca registra el cuerpo, la consulta ni las cabeceras de autorización.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuditarAttribute(string categoria = CategoriasAuditoria.Consulta) : Attribute, IAsyncActionFilter
{
    public const string CabeceraHerramienta = "X-Herramienta-Agente";
    public const string CabeceraConsulta = "X-Consulta-Agente";
    private const string ClaveDetalle = "auditoria.detalle";

    /// <summary>Nombre de la acción; si se omite se usa "controlador.accion".</summary>
    public string? Accion { get; init; }

    public static void AgregarDetalle(HttpContext http, string detalle) => http.Items[ClaveDetalle] = detalle;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Si el método tiene su propio [Auditar], el de la clase no se aplica (evita registros dobles):
        // MVC ordena los filtros de la clase antes que los del método, así que el último es el más específico.
        if (!ReferenceEquals(context.Filters.OfType<AuditarAttribute>().LastOrDefault(), this))
        {
            await next();
            return;
        }

        var ejecutado = await next();
        var http = context.HttpContext;

        var codigo = ejecutado.Exception is not null && !ejecutado.ExceptionHandled
            ? StatusCodes.Status500InternalServerError
            : ejecutado.Result switch
            {
                IStatusCodeActionResult { StatusCode: { } c } => c,
                _ => http.Response.StatusCode
            };

        var categoriaEfectiva = categoria;
        var detalles = new List<string>();
        var herramienta = http.Request.Headers[CabeceraHerramienta].ToString();
        if (AuditoriaService.HerramientasAgente.Contains(herramienta))
        {
            categoriaEfectiva = CategoriasAuditoria.Agente;
            detalles.Add($"herramienta={herramienta}");
            var consulta = http.Request.Headers[CabeceraConsulta].ToString();
            if (consulta.Length is > 0 and <= 36 && consulta.All(ch => char.IsLetterOrDigit(ch) || ch == '-'))
                detalles.Add($"consulta={consulta}");
        }
        if (http.Items.TryGetValue(ClaveDetalle, out var extra) && extra is string texto && texto.Length > 0)
            detalles.Add(texto);

        var accion = Accion ?? NombrePorDefecto(context);
        var usuario = http.User;
        _ = int.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId);

        var auditoria = http.RequestServices.GetRequiredService<AuditoriaService>();
        await auditoria.RegistrarAsync(new EventoAuditoria(
            categoriaEfectiva,
            accion,
            AuditoriaService.ResultadoDesdeCodigo(codigo),
            codigo,
            Recurso(context),
            detalles.Count == 0 ? null : string.Join("; ", detalles),
            usuarioId == 0 ? null : usuarioId,
            usuario.FindFirst("email")?.Value,
            usuario.FindFirst("role")?.Value,
            http.Connection.RemoteIpAddress?.ToString()));
    }

    private static string NombrePorDefecto(ActionExecutingContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor d
            ? $"{d.ControllerName}.{d.ActionName}".ToLowerInvariant()
            : "accion";

    /// <summary>"curso/3/estudiante/7": solo ids de la ruta, nunca la cadena de consulta.</summary>
    private static string? Recurso(ActionExecutingContext context)
    {
        var partes = new List<string>();
        var valores = context.RouteData.Values;
        if (valores.TryGetValue("cursoId", out var curso)) partes.Add($"curso/{curso}");
        if (valores.TryGetValue("estudianteId", out var est)) partes.Add($"estudiante/{est}");
        if (valores.TryGetValue("actividadId", out var act)) partes.Add($"actividad/{act}");
        if (valores.TryGetValue("id", out var id))
        {
            var nombre = context.ActionDescriptor is ControllerActionDescriptor d ? d.ControllerName.ToLowerInvariant() : "recurso";
            partes.Add($"{nombre}/{id}");
        }
        return partes.Count == 0 ? null : string.Join("/", partes);
    }
}
