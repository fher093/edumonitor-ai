namespace Backend.Dtos;

public record RegistroAuditoriaDto(
    long Id,
    DateTime Fecha,
    int? UsuarioId,
    string? UsuarioCorreo,
    string? Rol,
    string Categoria,
    string Accion,
    string? Recurso,
    string? Detalle,
    string Resultado,
    int? CodigoHttp);

public record PaginaAuditoriaDto(
    int Total,
    int Pagina,
    int Tamano,
    List<RegistroAuditoriaDto> Registros);
