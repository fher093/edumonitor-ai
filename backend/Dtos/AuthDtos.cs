using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// En .NET 10 la validación de records posicionales debe ir en el parámetro
// del constructor primario, no en la propiedad (si no, el binder lanza 500).
public record LoginRequest(
    [Required, StringLength(254)] string Correo,
    [Required, StringLength(128)] string Password);

public record UsuarioDto(int Id, string Nombre, string Correo, string Rol);

public record LoginResponse(string Token, DateTime ExpiresAt, UsuarioDto Usuario);

public record ErrorResponse(string Message);
