namespace Backend.Dtos;

/// <summary>
/// Actividad evaluable sincronizada desde Moodle. <c>UrlMoodle</c> abre el ítem en Moodle
/// (vista de calificación del ítem) para que el docente califique o edite allí.
/// </summary>
public record ActividadDto(
    int Id,
    int CursoId,
    string Nombre,
    string Tipo,
    string Componente,
    string ComponenteEtiqueta,
    string ComponenteEtiquetaCorta,
    int Parcial,
    DateTime? FechaEntrega,
    decimal PuntajeMaximo,
    int TotalCalificados,
    int TotalEstudiantes,
    bool Vencida,
    DateTime FechaCreacion,
    long? MoodleGradeItemId = null,
    string? UrlMoodle = null);

public record CalificacionActividadDto(
    int ActividadId,
    int EstudianteId,
    string Identificacion,
    string Nombre,
    string Correo,
    decimal? Nota,
    decimal PuntajeMaximo,
    string Estado,
    string EstadoEtiqueta,
    string? Observacion,
    DateTime? FechaActualizacion);

public record FinalizacionEstudianteDto(
    int EstudianteId,
    string Identificacion,
    string Nombre,
    int TotalActividades,
    int Completadas,
    int Vencidas,
    int SinEntrega,
    int Pendientes,
    decimal? PorcentajeFinalizacion);

public record FinalizacionCursoDto(
    int CursoId,
    string CursoNombre,
    DateTime FechaCalculo,
    List<FinalizacionEstudianteDto> Estudiantes);
