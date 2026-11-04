using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public record CrearActividadRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, StringLength(40, MinimumLength = 2)] string Tipo,
    [Required, StringLength(40)] string Componente,
    [Range(1, 2)] int Parcial = 1,
    DateTime? FechaEntrega = null,
    [Range(0.1, 100)] decimal PuntajeMaximo = 10m);

public record EditarActividadRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, StringLength(40, MinimumLength = 2)] string Tipo,
    [Required, StringLength(40)] string Componente,
    [Range(1, 2)] int Parcial = 1,
    DateTime? FechaEntrega = null,
    [Range(0.1, 100)] decimal PuntajeMaximo = 10m);

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
    DateTime FechaCreacion);

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

public record RegistrarCalificacionItem(
    [Required] int EstudianteId,
    decimal? Nota,
    [Required, StringLength(20)] string Estado,
    [StringLength(500)] string? Observacion = null);

public record RegistrarCalificacionesRequest(
    [Required, MinLength(1)] List<RegistrarCalificacionItem> Calificaciones);

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
