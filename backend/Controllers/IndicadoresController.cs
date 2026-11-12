using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/indicadores")]
[Authorize(Roles = RolNombres.Docente)]
public class IndicadoresController(
    AppDbContext db,
    AnaliticaProvider analitica,
    AnaliticaService servicio) : ControladorAcademicoBase(db, analitica)
{
    /// <summary>Panel de indicadores del curso (HU-07 / RF-05).</summary>
    [HttpGet("curso/{cursoId:int}")]
    [ProducesResponseType<DashboardCursoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardCursoDto>> Dashboard(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var contexto = await Analitica.CargarAsync(cursoId, ct);
        if (contexto is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        return Ok(servicio.CalcularDashboard(contexto));
    }

    /// <summary>Ficha individual de seguimiento del estudiante (HU-08 / RF-06).</summary>
    [HttpGet("curso/{cursoId:int}/estudiantes/{estudianteId:int}")]
    [ProducesResponseType<FichaEstudianteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FichaEstudianteDto>> Ficha(
        int cursoId, int estudianteId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var contexto = await Analitica.CargarAsync(cursoId, ct);
        if (contexto is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        var ficha = servicio.CalcularFicha(contexto, estudianteId);
        if (ficha is null)
            return NotFound(new ErrorResponse("El estudiante no está matriculado activamente en este curso."));

        return Ok(ficha);
    }
}
