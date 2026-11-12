using Backend.Data;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Base para controladores académicos: resuelve el usuario autenticado y aplica
/// el control de acceso por curso (RN-12 / HU-06). Solo el docente propietario accede a los datos
/// académicos del curso; el administrador técnico no ve notas ni estudiantes (mínimo privilegio).
/// </summary>
public abstract class ControladorAcademicoBase(AppDbContext db, AnaliticaProvider analitica) : ControllerBase
{
    protected AppDbContext Db { get; } = db;
    protected AnaliticaProvider Analitica { get; } = analitica;

    protected int ObtenerUsuarioId() =>
        int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    protected async Task<ActionResult?> VerificarAccesoCursoAsync(int cursoId, CancellationToken ct)
    {
        var docenteId = await Db.Cursos
            .Where(c => c.Id == cursoId)
            .Select(c => (int?)c.DocenteId)
            .FirstOrDefaultAsync(ct);
        if (docenteId is null)
            return NotFound(new ErrorResponse("Curso no encontrado."));
        if (docenteId != ObtenerUsuarioId())
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("No tienes permiso para acceder a este curso."));
        return null;
    }
}
