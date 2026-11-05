using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Base para controladores académicos: resuelve el usuario autenticado y aplica
/// el control de acceso por curso (RN-12 / HU-06).
/// </summary>
public abstract class ControladorAcademicoBase(AppDbContext db, AnaliticaProvider analitica) : ControllerBase
{
    protected AppDbContext Db { get; } = db;
    protected AnaliticaProvider Analitica { get; } = analitica;

    protected int ObtenerUsuarioId() =>
        int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    protected bool EsAdmin() => User.IsInRole(RolNombres.AdministradorTecnico);

    protected async Task<bool> PuedeAccederAlCursoAsync(int cursoId, CancellationToken ct)
    {
        if (EsAdmin()) return await Db.Cursos.AnyAsync(c => c.Id == cursoId, ct);
        var usuarioId = ObtenerUsuarioId();
        return await Db.Cursos.AnyAsync(c => c.Id == cursoId && c.DocenteId == usuarioId, ct);
    }

    protected async Task<ActionResult?> VerificarAccesoCursoAsync(int cursoId, CancellationToken ct)
    {
        if (!await Db.Cursos.AnyAsync(c => c.Id == cursoId, ct))
            return NotFound(new ErrorResponse("Curso no encontrado."));
        if (!await PuedeAccederAlCursoAsync(cursoId, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("No tienes permiso para acceder a este curso."));
        return null;
    }
}
