using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = $"{RolNombres.Docente},{RolNombres.AdministradorTecnico}")]
public class ActividadesController(AppDbContext db, AnaliticaProvider analitica)
    : ControladorAcademicoBase(db, analitica)
{
    /// <summary>Lista las actividades evaluables de un curso autorizado (HU-05 / RF-03).</summary>
    [HttpGet("cursos/{cursoId:int}/actividades")]
    [ProducesResponseType<List<ActividadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ActividadDto>>> Listar(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var ahora = DateTime.UtcNow;
        var totalEstudiantes = await Db.Matriculas.CountAsync(
            m => m.CursoId == cursoId && m.Estado == MatriculaEstados.Activa, ct);

        var actividades = await Db.Actividades.AsNoTracking()
            .Where(a => a.CursoId == cursoId)
            .OrderBy(a => a.Parcial)
            .ThenBy(a => a.FechaEntrega ?? DateTime.MaxValue)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.CursoId,
                a.Nombre,
                a.Tipo,
                a.Componente,
                a.Parcial,
                a.FechaEntrega,
                a.PuntajeMaximo,
                a.FechaCreacion,
                TotalCalificados = a.Calificaciones.Count(c =>
                    c.Estado == EstadosActividad.Calificada || c.Estado == EstadosActividad.NotaCero)
            })
            .ToListAsync(ct);

        var resultado = actividades.Select(a => new ActividadDto(
            a.Id,
            a.CursoId,
            a.Nombre,
            a.Tipo,
            a.Componente,
            ComponentesEvaluacion.Etiqueta(a.Componente),
            ComponentesEvaluacion.EtiquetaCorta(a.Componente),
            a.Parcial,
            a.FechaEntrega,
            a.PuntajeMaximo,
            a.TotalCalificados,
            totalEstudiantes,
            a.FechaEntrega is not null && a.FechaEntrega.Value <= ahora,
            a.FechaCreacion)).ToList();

        return Ok(resultado);
    }

    /// <summary>Obtiene una actividad autorizada.</summary>
    [HttpGet("actividades/{id:int}")]
    [ProducesResponseType<ActividadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActividadDto>> Obtener(int id, CancellationToken ct)
    {
        var actividad = await Db.Actividades.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (actividad is null) return NotFound(new ErrorResponse("Actividad no encontrada."));

        var error = await VerificarAccesoCursoAsync(actividad.CursoId, ct);
        if (error is not null) return error;

        var totalEstudiantes = await Db.Matriculas.CountAsync(
            m => m.CursoId == actividad.CursoId && m.Estado == MatriculaEstados.Activa, ct);
        var totalCalificados = await Db.Calificaciones.CountAsync(c =>
            c.ActividadId == id &&
            (c.Estado == EstadosActividad.Calificada || c.Estado == EstadosActividad.NotaCero), ct);

        return Ok(new ActividadDto(
            actividad.Id, actividad.CursoId, actividad.Nombre, actividad.Tipo, actividad.Componente,
            ComponentesEvaluacion.Etiqueta(actividad.Componente),
            ComponentesEvaluacion.EtiquetaCorta(actividad.Componente),
            actividad.Parcial, actividad.FechaEntrega, actividad.PuntajeMaximo, totalCalificados,
            totalEstudiantes,
            actividad.FechaEntrega is not null && actividad.FechaEntrega <= DateTime.UtcNow,
            actividad.FechaCreacion));
    }

    /// <summary>Crea una actividad con fecha de entrega, puntaje máximo y componente UTA (HU-05).</summary>
    [HttpPost("cursos/{cursoId:int}/actividades")]
    [ProducesResponseType<ActividadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActividadDto>> Crear(
        int cursoId, [FromBody] CrearActividadRequest request, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var componente = request.Componente.Trim().ToLowerInvariant();
        if (!ComponentesEvaluacion.Todos.Contains(componente))
            return BadRequest(new ErrorResponse("El componente debe ser contacto_docente, practico_experimental o autonomo."));

        var ahora = DateTime.UtcNow;
        var actividad = new Actividad
        {
            CursoId = cursoId,
            Nombre = request.Nombre.Trim(),
            Tipo = request.Tipo.Trim(),
            Componente = componente,
            Parcial = request.Parcial,
            FechaEntrega = NormalizarFecha(request.FechaEntrega),
            PuntajeMaximo = request.PuntajeMaximo,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };

        Db.Actividades.Add(actividad);
        await Db.SaveChangesAsync(ct);

        var totalEstudiantes = await Db.Matriculas.CountAsync(
            m => m.CursoId == cursoId && m.Estado == MatriculaEstados.Activa, ct);

        var dto = new ActividadDto(
            actividad.Id, actividad.CursoId, actividad.Nombre, actividad.Tipo, actividad.Componente,
            ComponentesEvaluacion.Etiqueta(actividad.Componente),
            ComponentesEvaluacion.EtiquetaCorta(actividad.Componente),
            actividad.Parcial, actividad.FechaEntrega, actividad.PuntajeMaximo, 0, totalEstudiantes,
            actividad.FechaEntrega is not null && actividad.FechaEntrega <= ahora,
            actividad.FechaCreacion);

        return CreatedAtAction(nameof(Obtener), new { id = actividad.Id }, dto);
    }

    /// <summary>Edita una actividad existente (HU-05).</summary>
    [HttpPut("actividades/{id:int}")]
    [ProducesResponseType<ActividadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActividadDto>> Editar(
        int id, [FromBody] EditarActividadRequest request, CancellationToken ct)
    {
        var actividad = await Db.Actividades.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (actividad is null) return NotFound(new ErrorResponse("Actividad no encontrada."));

        var error = await VerificarAccesoCursoAsync(actividad.CursoId, ct);
        if (error is not null) return error;

        var componente = request.Componente.Trim().ToLowerInvariant();
        if (!ComponentesEvaluacion.Todos.Contains(componente))
            return BadRequest(new ErrorResponse("El componente debe ser contacto_docente, practico_experimental o autonomo."));

        var notaMayor = await Db.Calificaciones
            .Where(c => c.ActividadId == id && c.Nota.HasValue && c.Nota > request.PuntajeMaximo)
            .AnyAsync(ct);
        if (notaMayor)
            return BadRequest(new ErrorResponse(
                $"El nuevo puntaje máximo ({request.PuntajeMaximo}) es menor que una nota ya registrada."));

        actividad.Nombre = request.Nombre.Trim();
        actividad.Tipo = request.Tipo.Trim();
        actividad.Componente = componente;
        actividad.Parcial = request.Parcial;
        actividad.FechaEntrega = NormalizarFecha(request.FechaEntrega);
        actividad.PuntajeMaximo = request.PuntajeMaximo;
        actividad.FechaActualizacion = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);

        var totalEstudiantes = await Db.Matriculas.CountAsync(
            m => m.CursoId == actividad.CursoId && m.Estado == MatriculaEstados.Activa, ct);
        var totalCalificados = await Db.Calificaciones.CountAsync(c =>
            c.ActividadId == id &&
            (c.Estado == EstadosActividad.Calificada || c.Estado == EstadosActividad.NotaCero), ct);

        return Ok(new ActividadDto(
            actividad.Id, actividad.CursoId, actividad.Nombre, actividad.Tipo, actividad.Componente,
            ComponentesEvaluacion.Etiqueta(actividad.Componente),
            ComponentesEvaluacion.EtiquetaCorta(actividad.Componente),
            actividad.Parcial, actividad.FechaEntrega, actividad.PuntajeMaximo, totalCalificados,
            totalEstudiantes,
            actividad.FechaEntrega is not null && actividad.FechaEntrega <= DateTime.UtcNow,
            actividad.FechaCreacion));
    }

    /// <summary>Elimina una actividad y sus calificaciones (HU-05).</summary>
    [HttpDelete("actividades/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var actividad = await Db.Actividades.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (actividad is null) return NotFound(new ErrorResponse("Actividad no encontrada."));

        var error = await VerificarAccesoCursoAsync(actividad.CursoId, ct);
        if (error is not null) return error;

        Db.Actividades.Remove(actividad);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Consulta la finalización de actividades por estudiante (RF-06).</summary>
    [HttpGet("cursos/{cursoId:int}/finalizacion")]
    [ProducesResponseType<FinalizacionCursoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalizacionCursoDto>> Finalizacion(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var contexto = await Analitica.CargarAsync(cursoId, ct);
        if (contexto is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        var calificaciones = contexto.Calificaciones
            .GroupBy(c => (c.ActividadId, c.EstudianteId))
            .ToDictionary(g => g.Key, g => g.First());

        var estudiantes = contexto.Estudiantes.Select(estudiante =>
        {
            var total = 0;
            var completadas = 0;
            var vencidas = 0;
            var sinEntrega = 0;
            var pendientes = 0;

            foreach (var actividad in contexto.Actividades)
            {
                calificaciones.TryGetValue((actividad.Id, estudiante.Id), out var registro);
                var estado = EstadosActividad.Resolver(
                    registro?.Estado, registro?.Nota, actividad.FechaEntrega, contexto.Ahora);
                total++;
                if (EstadosActividad.EsCompletada(estado)) completadas++;
                if (estado == EstadosActividad.SinEntrega) sinEntrega++;
                if (estado == EstadosActividad.Pendiente) pendientes++;
                if (actividad.FechaEntrega is not null && actividad.FechaEntrega.Value <= contexto.Ahora) vencidas++;
            }

            return new FinalizacionEstudianteDto(
                estudiante.Id,
                estudiante.Identificacion,
                estudiante.Nombre,
                total,
                completadas,
                vencidas,
                sinEntrega,
                pendientes,
                vencidas == 0 ? null : Math.Round((decimal)completadas / vencidas * 100m, 2));
        }).ToList();

        return Ok(new FinalizacionCursoDto(cursoId, contexto.Curso.Nombre, contexto.Ahora, estudiantes));
    }

    private static DateTime? NormalizarFecha(DateTime? fecha)
    {
        if (fecha is null) return null;
        return fecha.Value.Kind switch
        {
            DateTimeKind.Utc => fecha.Value,
            DateTimeKind.Local => fecha.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha.Value, DateTimeKind.Utc)
        };
    }
}
