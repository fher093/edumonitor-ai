using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Consulta de calificaciones sincronizadas desde Moodle (HU-05 / RN-02).
/// Las notas se registran en Moodle; EduMonitor solo las lee y resuelve el estado efectivo.
/// </summary>
[ApiController]
[Route("api/actividades/{actividadId:int}/calificaciones")]
[Authorize(Roles = RolNombres.Docente)]
public class CalificacionesController(AppDbContext db, AnaliticaProvider analitica)
    : ControladorAcademicoBase(db, analitica)
{
    /// <summary>
    /// Lista las calificaciones de una actividad para todos los estudiantes matriculados.
    /// Los estudiantes sin registro aparecen con estado efectivo (sin entrega o pendiente).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<CalificacionActividadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CalificacionActividadDto>>> Listar(int actividadId, CancellationToken ct)
    {
        var actividad = await Db.Actividades.AsNoTracking().FirstOrDefaultAsync(a => a.Id == actividadId, ct);
        if (actividad is null) return NotFound(new ErrorResponse("Actividad no encontrada."));

        var error = await VerificarAccesoCursoAsync(actividad.CursoId, ct);
        if (error is not null) return error;

        var estudiantes = await Db.Matriculas.AsNoTracking()
            .Where(m => m.CursoId == actividad.CursoId && m.Estado == MatriculaEstados.Activa)
            .OrderBy(m => m.Estudiante.Nombre)
            .Select(m => new
            {
                m.Estudiante.Id,
                m.Estudiante.Identificacion,
                m.Estudiante.Nombre,
                m.Estudiante.Correo
            })
            .ToListAsync(ct);

        var calificaciones = await Db.Calificaciones.AsNoTracking()
            .Where(c => c.ActividadId == actividad.Id)
            .ToDictionaryAsync(c => c.EstudianteId, ct);

        var ahora = DateTime.UtcNow;
        return Ok(estudiantes.Select(estudiante =>
        {
            calificaciones.TryGetValue(estudiante.Id, out var registro);
            var estado = EstadosActividad.Resolver(
                registro?.Estado, registro?.Nota, actividad.FechaEntrega, ahora);
            var nota = estado switch
            {
                EstadosActividad.NotaCero => 0m,
                EstadosActividad.Calificada => registro?.Nota,
                _ => (decimal?)null
            };

            return new CalificacionActividadDto(
                actividad.Id,
                estudiante.Id,
                estudiante.Identificacion,
                estudiante.Nombre,
                estudiante.Correo,
                nota,
                actividad.PuntajeMaximo,
                estado,
                EstadosActividad.Etiqueta(estado),
                registro?.Observacion,
                registro?.FechaActualizacion);
        }).ToList());
    }
}
