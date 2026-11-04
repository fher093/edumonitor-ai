using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/cursos")]
[Authorize(Roles = $"{RolNombres.AdministradorTecnico},{RolNombres.Docente}")]
public class CursosController(AppDbContext db) : ControllerBase
{
    private int ObtenerUsuarioId() =>
        int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    private bool EsAdmin() =>
        User.IsInRole(RolNombres.AdministradorTecnico);

    /// <summary>Lista los cursos disponibles (docente ve solo los propios; admin ve todos) (HU-04 / RF-04).</summary>
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
                c.FechaCreacion))
            .ToListAsync(ct);

        return Ok(cursos);
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

    /// <summary>Lista los docentes activos disponibles para asignarles asignaturas (solo Administrador).</summary>
    [HttpGet("docentes-disponibles")]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType<List<UsuarioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<UsuarioDto>>> ListarDocentesDisponibles(CancellationToken ct)
    {
        var docentes = await db.Usuarios
            .Where(u => u.Rol.Nombre == RolNombres.Docente && u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioDto(u.Id, u.Nombre, u.Correo, u.Rol.Nombre))
            .ToListAsync(ct);

        return Ok(docentes);
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
            curso.FechaCreacion));
    }

    /// <summary>Crea un nuevo curso (exclusivo para Administrador Técnico) (HU-02 / RF-02).</summary>
    [HttpPost]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType<CursoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CursoDto>> Crear([FromBody] CrearCursoRequest request, CancellationToken ct)
    {
        var targetDocenteId = request.DocenteId ?? 0;
        if (targetDocenteId <= 0)
        {
            return BadRequest(new ErrorResponse("Debe seleccionar el docente responsable de la asignatura."));
        }

        var docente = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == targetDocenteId && u.Rol.Nombre == RolNombres.Docente, ct);
        if (docente is null)
        {
            return BadRequest(new ErrorResponse("El docente seleccionado no existe o no tiene el rol de docente."));
        }

        var nombre = request.Nombre.Trim();
        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var periodo = request.Periodo.Trim();

        var existe = await db.Cursos.AnyAsync(c =>
            c.DocenteId == targetDocenteId &&
            c.Codigo == codigo &&
            c.Periodo == periodo, ct);

        if (existe)
        {
            return BadRequest(new ErrorResponse($"Ya existe un curso con el código '{codigo}' en el período '{periodo}' para el docente '{docente.Nombre}'."));
        }

        var ahora = DateTime.UtcNow;
        var curso = new Curso
        {
            DocenteId = targetDocenteId,
            Nombre = nombre,
            Codigo = codigo,
            Periodo = periodo,
            EscalaCalificacion = request.EscalaCalificacion > 0 ? request.EscalaCalificacion : 20.0m,
            Paralelo = string.IsNullOrWhiteSpace(request.Paralelo) ? null : request.Paralelo.Trim(),
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };

        db.Cursos.Add(curso);
        await db.SaveChangesAsync(ct);

        var dto = new CursoDto(
            curso.Id,
            curso.DocenteId,
            docente.Nombre,
            curso.Nombre,
            curso.Codigo,
            curso.Periodo,
            curso.EscalaCalificacion,
            curso.Paralelo,
            0,
            curso.FechaCreacion);

        return CreatedAtAction(nameof(Obtener), new { id = curso.Id }, dto);
    }

    /// <summary>Edita un curso (exclusivo para Administrador Técnico) (HU-02 / RF-02).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType<CursoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CursoDto>> Editar(int id, [FromBody] EditarCursoRequest request, CancellationToken ct)
    {
        var curso = await db.Cursos
            .Include(c => c.Docente)
            .Include(c => c.Matriculas)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        var targetDocenteId = request.DocenteId ?? curso.DocenteId;
        var docente = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == targetDocenteId && u.Rol.Nombre == RolNombres.Docente, ct);
        if (docente is null)
        {
            return BadRequest(new ErrorResponse("El docente seleccionado no existe o no tiene el rol de docente."));
        }

        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var periodo = request.Periodo.Trim();

        var duplicado = await db.Cursos.AnyAsync(c =>
            c.DocenteId == targetDocenteId &&
            c.Id != id &&
            c.Codigo == codigo &&
            c.Periodo == periodo, ct);

        if (duplicado)
        {
            return BadRequest(new ErrorResponse($"Ya existe otro curso con el código '{codigo}' en el período '{periodo}' para este docente."));
        }

        curso.DocenteId = targetDocenteId;
        curso.Nombre = request.Nombre.Trim();
        curso.Codigo = codigo;
        curso.Periodo = periodo;
        if (request.EscalaCalificacion > 0)
            curso.EscalaCalificacion = request.EscalaCalificacion;
        curso.Paralelo = string.IsNullOrWhiteSpace(request.Paralelo) ? null : request.Paralelo.Trim();
        curso.FechaActualizacion = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new CursoDto(
            curso.Id,
            curso.DocenteId,
            docente.Nombre,
            curso.Nombre,
            curso.Codigo,
            curso.Periodo,
            curso.EscalaCalificacion,
            curso.Paralelo,
            curso.Matriculas.Count(m => m.Estado == MatriculaEstados.Activa),
            curso.FechaCreacion));
    }

    /// <summary>Elimina un curso (exclusivo para Administrador Técnico) (HU-02 / RF-02).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        db.Cursos.Remove(curso);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Lista los estudiantes matriculados en un curso (HU-03 / RF-03).</summary>
    [HttpGet("{cursoId:int}/matriculas")]
    [ProducesResponseType<List<MatriculaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<MatriculaDto>>> ListarMatriculas(
        int cursoId, [FromQuery] string? q, [FromQuery] string? estado, CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId, ct);
        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));
        if (!EsAdmin() && curso.DocenteId != usuarioId)
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

    /// <summary>Matricula a un estudiante en un curso (HU-03 / RF-03).</summary>
    [HttpPost("{cursoId:int}/matriculas")]
    [ProducesResponseType<MatriculaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MatriculaDto>> Matricular(int cursoId, [FromBody] MatricularEstudianteRequest request, CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId, ct);
        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));
        if (!EsAdmin() && curso.DocenteId != usuarioId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("No tienes permiso para matricular en este curso."));

        var estudiante = await db.Estudiantes.FirstOrDefaultAsync(e => e.Id == request.EstudianteId, ct);
        if (estudiante is null) return NotFound(new ErrorResponse("Estudiante no encontrado."));

        var matriculaExistente = await db.Matriculas
            .FirstOrDefaultAsync(m => m.CursoId == cursoId && m.EstudianteId == request.EstudianteId, ct);

        if (matriculaExistente is not null)
        {
            if (matriculaExistente.Estado == MatriculaEstados.Activa)
                return BadRequest(new ErrorResponse("El estudiante ya está matriculado en este curso."));

            matriculaExistente.Estado = MatriculaEstados.Activa;
            matriculaExistente.FechaMatricula = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(ListarMatriculas), new { cursoId }, new MatriculaDto(
                matriculaExistente.Id,
                matriculaExistente.CursoId,
                estudiante.Id,
                estudiante.Identificacion,
                estudiante.Nombre,
                estudiante.Correo,
                matriculaExistente.FechaMatricula,
                matriculaExistente.Estado));
        }

        var nuevaMatricula = new Matricula
        {
            CursoId = cursoId,
            EstudianteId = request.EstudianteId,
            FechaMatricula = DateTime.UtcNow,
            Estado = MatriculaEstados.Activa
        };

        db.Matriculas.Add(nuevaMatricula);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListarMatriculas), new { cursoId }, new MatriculaDto(
            nuevaMatricula.Id,
            nuevaMatricula.CursoId,
            estudiante.Id,
            estudiante.Identificacion,
            estudiante.Nombre,
            estudiante.Correo,
            nuevaMatricula.FechaMatricula,
            nuevaMatricula.Estado));
    }

    /// <summary>Desmatricula a un estudiante de un curso (HU-03 / RF-03).</summary>
    [HttpDelete("{cursoId:int}/matriculas/{estudianteId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desmatricular(int cursoId, int estudianteId, CancellationToken ct)
    {
        var usuarioId = ObtenerUsuarioId();
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.Id == cursoId, ct);
        if (curso is null) return NotFound(new ErrorResponse("Curso no encontrado."));
        if (!EsAdmin() && curso.DocenteId != usuarioId)
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("No tienes permiso para desmatricular en este curso."));

        var matricula = await db.Matriculas
            .FirstOrDefaultAsync(m => m.CursoId == cursoId && m.EstudianteId == estudianteId, ct);

        if (matricula is null) return NotFound(new ErrorResponse("El estudiante no está matriculado en este curso."));

        db.Matriculas.Remove(matricula);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
