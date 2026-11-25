using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/informes")]
[Authorize(Roles = RolNombres.Docente)]
[Auditar]
public class InformesController(
    AppDbContext db,
    AnaliticaProvider analitica,
    AnaliticaService servicio) : ControladorAcademicoBase(db, analitica)
{
    /// <summary>Informe del período del curso, con fecha de corte y fuente de los datos (HU-16 / RF-11).</summary>
    [HttpGet("curso/{cursoId:int}")]
    [ProducesResponseType<InformeCursoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InformeCursoDto>> PorCurso(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var curso = await Db.Cursos.AsNoTracking()
            .Where(c => c.Id == cursoId)
            .Select(c => new { c.Paralelo, DocenteNombre = c.Docente.Nombre, c.Origen, c.FechaSincronizacion, c.MoodleCourseId })
            .FirstAsync(ct);

        // Un único contexto: todas las secciones del informe usan exactamente los mismos datos y la misma fecha.
        var contexto = await Analitica.CargarAsync(cursoId, ct);
        if (contexto is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        var urlBase = await EnlacesMoodle.ObtenerUrlBaseAsync(Db, ct);
        var desdeMoodle = curso.Origen == OrigenesDatos.Moodle;

        return Ok(new InformeCursoDto(
            contexto.Curso.Id,
            contexto.Curso.Nombre,
            contexto.Curso.Codigo,
            contexto.Curso.Periodo,
            curso.Paralelo,
            curso.DocenteNombre,
            contexto.Ahora,
            desdeMoodle ? "Moodle (sincronización automática)" : "Registro local de EduMonitor",
            curso.FechaSincronizacion,
            desdeMoodle ? EnlacesMoodle.Curso(urlBase, curso.MoodleCourseId) : null,
            servicio.CalcularDashboard(contexto),
            servicio.CalcularAnalisis(contexto),
            servicio.CalcularAlertasCurso(contexto),
            servicio.CalcularRecomendaciones(contexto)));
    }
}
