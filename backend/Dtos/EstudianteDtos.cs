using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public record CrearEstudianteRequest(
    [Required, StringLength(20, MinimumLength = 3)] string Identificacion,
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, EmailAddress, StringLength(254)] string Correo);

public record EstudianteDto(
    int Id,
    string Identificacion,
    string Nombre,
    string Correo,
    bool Activo);

public record MatricularEstudianteRequest(
    [Required] int EstudianteId);

public record MatriculaDto(
    int Id,
    int CursoId,
    int EstudianteId,
    string Identificacion,
    string Nombre,
    string Correo,
    DateTime FechaMatricula,
    string Estado);
