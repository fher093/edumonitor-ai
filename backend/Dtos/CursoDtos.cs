using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public record CrearCursoRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, StringLength(50, MinimumLength = 1)] string Codigo,
    [Required, StringLength(50, MinimumLength = 1)] string Periodo,
    [Range(1, 100)] decimal EscalaCalificacion = 20.0m,
    int? DocenteId = null,
    [StringLength(100)] string? Paralelo = null);

public record EditarCursoRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, StringLength(50, MinimumLength = 1)] string Codigo,
    [Required, StringLength(50, MinimumLength = 1)] string Periodo,
    [Range(1, 100)] decimal EscalaCalificacion = 20.0m,
    int? DocenteId = null,
    [StringLength(100)] string? Paralelo = null);

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
    DateTime FechaCreacion);
