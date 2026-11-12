namespace Backend.Dtos;

public record MatriculaDto(
    int Id,
    int CursoId,
    int EstudianteId,
    string Identificacion,
    string Nombre,
    string Correo,
    DateTime FechaMatricula,
    string Estado);
