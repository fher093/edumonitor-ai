using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/actividades/{actividadId:int}/calificaciones")]
[Authorize(Roles = $"{RolNombres.Docente},{RolNombres.AdministradorTecnico}")]
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

        return Ok(await ListarInternoAsync(actividad, ct));
    }

    /// <summary>
    /// Registra o actualiza, en lote, notas y estados de los estudiantes (HU-05 / RN-02).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<List<CalificacionActividadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CalificacionActividadDto>>> Registrar(
        int actividadId, [FromBody] RegistrarCalificacionesRequest request, CancellationToken ct)
    {
        if (request.Calificaciones is null || request.Calificaciones.Count == 0)
            return BadRequest(new ErrorResponse("Debe enviar al menos una calificación."));

        var actividad = await Db.Actividades.FirstOrDefaultAsync(a => a.Id == actividadId, ct);
        if (actividad is null) return NotFound(new ErrorResponse("Actividad no encontrada."));

        var error = await VerificarAccesoCursoAsync(actividad.CursoId, ct);
        if (error is not null) return error;

        var duplicados = request.Calificaciones
            .GroupBy(c => c.EstudianteId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicados.Count > 0)
            return BadRequest(new ErrorResponse(
                $"La solicitud contiene estudiantes repetidos: {string.Join(", ", duplicados)}."));

        var matriculados = await Db.Matriculas
            .Where(m => m.CursoId == actividad.CursoId && m.Estado == MatriculaEstados.Activa)
            .Select(m => m.EstudianteId)
            .ToListAsync(ct);
        var setMatriculados = matriculados.ToHashSet();

        var existentes = await Db.Calificaciones
            .Where(c => c.ActividadId == actividadId)
            .ToDictionaryAsync(c => c.EstudianteId, ct);

        var ahora = DateTime.UtcNow;

        foreach (var item in request.Calificaciones)
        {
            if (!setMatriculados.Contains(item.EstudianteId))
                return BadRequest(new ErrorResponse(
                    $"El estudiante {item.EstudianteId} no está matriculado activamente en el curso de esta actividad."));

            var estado = (item.Estado ?? string.Empty).Trim().ToLowerInvariant();
            if (!EstadosActividad.EsValido(estado))
                return BadRequest(new ErrorResponse(
                    $"Estado inválido para el estudiante {item.EstudianteId}. Use calificada, nota_cero, sin_entrega, pendiente o completada."));

            decimal? nota;
            switch (estado)
            {
                case EstadosActividad.Calificada:
                    if (item.Nota is null)
                        return BadRequest(new ErrorResponse(
                            $"El estudiante {item.EstudianteId} tiene estado calificada pero no se envió la nota."));
                    if (item.Nota < 0 || item.Nota > actividad.PuntajeMaximo)
                        return BadRequest(new ErrorResponse(
                            $"La nota del estudiante {item.EstudianteId} debe estar entre 0 y {actividad.PuntajeMaximo}."));
                    if (item.Nota == 0) estado = EstadosActividad.NotaCero;
                    nota = item.Nota;
                    break;

                case EstadosActividad.NotaCero:
                    if (item.Nota is not null && item.Nota != 0)
                        return BadRequest(new ErrorResponse(
                            $"El estado nota_cero del estudiante {item.EstudianteId} solo admite nota 0."));
                    nota = 0m;
                    break;

                default:
                    nota = null;
                    break;
            }

            var observacion = string.IsNullOrWhiteSpace(item.Observacion) ? null : item.Observacion.Trim();

            if (existentes.TryGetValue(item.EstudianteId, out var calificacion))
            {
                calificacion.Nota = nota;
                calificacion.Estado = estado;
                calificacion.Observacion = observacion;
                calificacion.FechaActualizacion = ahora;
            }
            else
            {
                Db.Calificaciones.Add(new Calificacion
                {
                    ActividadId = actividadId,
                    EstudianteId = item.EstudianteId,
                    Nota = nota,
                    Estado = estado,
                    Observacion = observacion,
                    FechaRegistro = ahora,
                    FechaActualizacion = ahora
                });
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await ListarInternoAsync(actividad, ct));
    }

    private async Task<List<CalificacionActividadDto>> ListarInternoAsync(
        Actividad actividad, CancellationToken ct)
    {
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
        return estudiantes.Select(estudiante =>
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
        }).ToList();
    }
}
