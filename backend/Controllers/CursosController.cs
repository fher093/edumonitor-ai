using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Consulta de cursos sincronizados desde Moodle (RF-03). Los cursos y matrículas se crean en Moodle;
/// EduMonitor no los registra ni los edita.
/// </summary>
[ApiController]
[Route("api/cursos")]
[Authorize(Roles = $"{RolNombres.AdministradorTecnico},{RolNombres.Docente}")]
public class CursosController(AppDbContext db) : ControllerBase
{
    private int ObtenerUsuarioId() =>
        int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    private bool EsAdmin() =>
        User.IsInRole(RolNombres.AdministradorTecnico);

    /// <summary>
    /// Lista los cursos (HU-04 / RF-04). El docente ve solo los propios; el administrador técnico ve todos
    /// para verificar la sincronización (solo datos del curso, sin notas).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<CursoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CursoDto>>> Listar(
        [FromQuery] string? periodo,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var query = db.Cursos.Include(c => c.Docente).AsQueryable();

        if (!EsAdmin())
        {
            query = query.Where(c => c.DocenteId == usuarioId);
        }

        if (!string.IsNullOrWhiteSpace(periodo))
        {
            var periodoFiltro = periodo.Trim();
            query = query.Where(c => c.Periodo == periodoFiltro);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var filtro = q.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Nombre.ToLower().Contains(filtro) ||
                c.Codigo.ToLower().Contains(filtro) ||
                (c.Paralelo != null && c.Paralelo.ToLower().Contains(filtro)));
        }

        var cursos = await query
            .OrderByDescending(c => c.FechaCreacion)
            .Select(c => new CursoDto(
                c.Id,
                c.DocenteId,
                c.Docente.Nombre,
                c.Nombre,
                c.Codigo,
                c.Periodo,
                c.EscalaCalificacion,
                c.Paralelo,
                c.Matriculas.Count(m => m.Estado == MatriculaEstados.Activa),
                c.FechaCreacion,
                c.Origen,
                c.FechaSincronizacion,
                c.MoodleCourseId,
                null))
            .ToListAsync(ct);

        var urlBase = await EnlacesMoodle.ObtenerUrlBaseAsync(db, ct);
        return Ok(cursos.Select(c => c with { UrlMoodle = EnlacesMoodle.Curso(urlBase, c.MoodleCourseId) }).ToList());
    }

    /// <summary>Lista los períodos académicos disponibles para el usuario (HU-09 / RF-04).</summary>
    [HttpGet("periodos")]
    [ProducesResponseType<List<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> ListarPeriodos(CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var query = db.Cursos.AsNoTracking().AsQueryable();

        if (!EsAdmin())
        {
            query = query.Where(c => c.DocenteId == usuarioId);
        }

        var periodos = await query
            .Select(c => c.Periodo)
            .Distinct()
            .OrderByDescending(p => p)
            .ToListAsync(ct);

        return Ok(periodos);
    }

    /// <summary>Obtiene el detalle de un curso.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CursoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CursoDto>> Obtener(int id, CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var curso = await db.Cursos
            .Include(c => c.Docente)
            .Include(c => c.Matriculas)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));
        if (!EsAdmin() && curso.DocenteId != usuarioId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("No tienes permiso para acceder a este curso."));

        return Ok(new CursoDto(
            curso.Id,
            curso.DocenteId,
            curso.Docente.Nombre,
            curso.Nombre,
            curso.Codigo,
            curso.Periodo,
            curso.EscalaCalificacion,
            curso.Paralelo,
            curso.Matriculas.Count(m => m.Estado == MatriculaEstados.Activa),
            curso.FechaCreacion,
            curso.Origen,
            curso.FechaSincronizacion,
            curso.MoodleCourseId,
            EnlacesMoodle.Curso(await EnlacesMoodle.ObtenerUrlBaseAsync(db, ct), curso.MoodleCourseId)));
    }

    /// <summary>
    /// Nómina del curso (HU-03 / RF-03). Solo para el docente propietario: el administrador técnico
    /// no accede a datos de estudiantes (mínimo privilegio, RN-12).
    /// </summary>
    [HttpGet("{cursoId:int}/matriculas")]
    [Authorize(Roles = RolNombres.Docente)]
    [ProducesResponseType<List<MatriculaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<MatriculaDto>>> ListarMatriculas(
        int cursoId, [FromQuery] string? q, [FromQuery] string? estado, CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId, ct);
        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));
        if (curso.DocenteId != usuarioId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("No tienes permiso para acceder a las matrículas de este curso."));

        var query = db.Matriculas
            .Where(m => m.CursoId == cursoId)
            .Include(m => m.Estudiante)
            .AsQueryable();

        var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? MatriculaEstados.Activa : estado.Trim();
        if (!string.Equals(estadoFiltro, "Todas", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(estadoFiltro, "Todos", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.Estado == estadoFiltro);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var filtro = q.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.Estudiante.Identificacion.ToLower().Contains(filtro) ||
                m.Estudiante.Nombre.ToLower().Contains(filtro) ||
                m.Estudiante.Correo.ToLower().Contains(filtro));
        }

        var matriculas = await query
            .OrderBy(m => m.Estudiante.Nombre)
            .Select(m => new MatriculaDto(
                m.Id,
                m.CursoId,
                m.EstudianteId,
                m.Estudiante.Identificacion,
                m.Estudiante.Nombre,
                m.Estudiante.Correo,
                m.FechaMatricula,
                m.Estado))
            .ToListAsync(ct);

        return Ok(matriculas);
    }
}
