using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

public record ReglaAlertaDto(
    int Id,
    string Tipo,
    string Nombre,
    string ParametrosJson,
    bool Activa,
    DateTime FechaActualizacion);

public record EditarReglaAlertaRequest(
    bool Activa,
    [Required, StringLength(400, MinimumLength = 2)] string ParametrosJson);

public record AlertaDto(
    string Tipo,
    string TipoEtiqueta,
    string Regla,
    int EstudianteId,
    string Identificacion,
    string Nombre,
    string Evidencia,
    string Severidad,
    DateTime FechaCalculo);

public record ResumenAlertasDto(
    int Total,
    int Descenso,
    int Atraso,
    int BajaFinalizacion,
    int EstudiantesAfectados);

public record AlertasCursoDto(
    int CursoId,
    string CursoNombre,
    string CursoCodigo,
    string Periodo,
    decimal EscalaCalificacion,
    DateTime FechaCalculo,
    List<AlertaDto> Alertas,
    ResumenAlertasDto Resumen,
    List<ReglaAlertaDto> Reglas,
    List<string> Advertencias);
