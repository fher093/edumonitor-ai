using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Consulta de actividades evaluables sincronizadas desde Moodle (HU-05 / RF-03).
/// EduMonitor no crea, edita ni elimina actividades: eso se hace en Moodle, que es la fuente de verdad.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Roles = RolNombres.Docente)]
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
        var moodleCourseId = await Db.Cursos
            .Where(c => c.Id == cursoId)
            .Select(c => c.MoodleCourseId)
            .FirstAsync(ct);
        var urlBase = await EnlacesMoodle.ObtenerUrlBaseAsync(Db, ct);

        var actividades = await Db.Actividades.AsNoTracking()
            .Where(a => a.CursoId == cursoId)
            .OrderBy(a => a.Parcial)
            .ThenBy(a => a.FechaEntrega ?? DateTime.MaxValue)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                Actividad = a,
                TotalCalificados = a.Calificaciones.Count(c =>
                    c.Estado == EstadosActividad.Calificada || c.Estado == EstadosActividad.NotaCero)
            })
            .ToListAsync(ct);

        return Ok(actividades
            .Select(a => ADto(a.Actividad, a.TotalCalificados, totalEstudiantes, ahora, urlBase, moodleCourseId))
            .ToList());
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
        var moodleCourseId = await Db.Cursos
            .Where(c => c.Id == actividad.CursoId)
            .Select(c => c.MoodleCourseId)
            .FirstAsync(ct);
        var urlBase = await EnlacesMoodle.ObtenerUrlBaseAsync(Db, ct);

        return Ok(ADto(actividad, totalCalificados, totalEstudiantes, DateTime.UtcNow, urlBase, moodleCourseId));
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

    private static ActividadDto ADto(
        Actividad a, int totalCalificados, int totalEstudiantes, DateTime ahora, string? urlBase, long? moodleCourseId) =>
        new(
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
            totalCalificados,
            totalEstudiantes,
            a.FechaEntrega is not null && a.FechaEntrega.Value <= ahora,
            a.FechaCreacion,
            a.MoodleGradeItemId,
            EnlacesMoodle.Actividad(urlBase, moodleCourseId, a.MoodleGradeItemId));
}
