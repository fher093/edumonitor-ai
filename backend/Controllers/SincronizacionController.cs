using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Options;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Controllers;

/// <summary>Sincronización de cursos, matriculados, actividades y notas desde Moodle (RF-03).</summary>
[ApiController]
[Route("api/sincronizacion/moodle")]
public class SincronizacionController(
    AppDbContext db,
    SincronizacionMoodleService sincronizacion,
    IOptions<SincronizacionAutomaticaOptions> automatica) : ControllerBase
{
    /// <summary>Ejecuta la sincronización y devuelve su registro (fecha, contadores y errores).</summary>
    [HttpPost]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType<SincronizacionMoodleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SincronizacionMoodleDto>> Sincronizar(
        [FromBody] SincronizarMoodleRequest? request, CancellationToken ct)
    {
        var bearer = Request.Headers.Authorization.ToString();
        bearer = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? bearer[7..].Trim() : bearer;
        var usuarioId = int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : (int?)null;

        var resultado = await sincronizacion.EjecutarAsync(
            usuarioId, User.FindFirst("email")?.Value, bearer, request?.CursosMoodle, ct);

        return resultado is null
            ? Conflict(new ErrorResponse("Ya hay una sincronización en curso. Espera a que termine."))
            : Ok(resultado);
    }

    /// <summary>Historial de sincronizaciones, de la más reciente a la más antigua.</summary>
    [HttpGet("historial")]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType<List<SincronizacionMoodleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SincronizacionMoodleDto>>> Historial(
        [FromQuery] int limite = 20, CancellationToken ct = default)
    {
        var registros = await db.SincronizacionesMoodle.AsNoTracking()
            .OrderByDescending(s => s.FechaInicio)
            .Take(Math.Clamp(limite, 1, 100))
            .ToListAsync(ct);
        return Ok(registros.Select(SincronizacionMoodleService.ADto).ToList());
    }

    /// <summary>Fecha y estado de la última sincronización (fuente de los datos que ve el docente, RNF-08).</summary>
    [HttpGet("estado")]
    [Authorize(Roles = $"{RolNombres.Docente},{RolNombres.AdministradorTecnico}")]
    [ProducesResponseType<EstadoSincronizacionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EstadoSincronizacionDto>> Estado(CancellationToken ct)
    {
        var ultima = await db.SincronizacionesMoodle.AsNoTracking()
            .Where(s => s.Estado != EstadosSincronizacion.EnCurso)
            .OrderByDescending(s => s.FechaInicio)
            .FirstOrDefaultAsync(ct);
        var cursosMoodle = await db.Cursos.CountAsync(c => c.Origen == OrigenesDatos.Moodle, ct);
        var auto = automatica.Value;
        return Ok(new EstadoSincronizacionDto(
            ultima?.FechaFin ?? ultima?.FechaInicio, ultima?.Estado, ultima?.UrlMoodle, cursosMoodle,
            auto.Habilitada, auto.Habilitada ? Math.Max(1, auto.IntervaloMinutos) : null));
    }
}
