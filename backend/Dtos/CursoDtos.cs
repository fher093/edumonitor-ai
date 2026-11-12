namespace Backend.Dtos;

public record CursoDto(
    int Id,
    int DocenteId,
    string DocenteNombre,
    string Nombre,
    string Codigo,
    string Periodo,
    decimal EscalaCalificacion,
    string? Paralelo,
    int TotalEstudiantes,
    DateTime FechaCreacion,
    string Origen = "local",
    DateTime? FechaSincronizacion = null,
    long? MoodleCourseId = null,
    string? UrlMoodle = null); // Página del curso en Moodle (null si el curso no proviene de Moodle).
