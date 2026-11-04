using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/analisis")]
[Authorize(Roles = $"{RolNombres.Docente},{RolNombres.AdministradorTecnico}")]
public class AnalisisController(
    AppDbContext db,
    AnaliticaProvider analitica,
    AnaliticaService servicio) : ControladorAcademicoBase(db, analitica)
{
    /// <summary>Análisis grupal por actividad y tendencias del curso (HU-11 / RF-08).</summary>
    [HttpGet("curso/{cursoId:int}")]
    [ProducesResponseType<AnalisisGrupalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalisisGrupalDto>> Analizar(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var contexto = await Analitica.CargarAsync(cursoId, ct);
        if (contexto is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        return Ok(servicio.CalcularAnalisis(contexto));
    }
}
