using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Inicia sesión con correo y contraseña y devuelve un JWT con el rol del usuario.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Correo, request.Password, ct);
        if (result is null)
            return Unauthorized(new ErrorResponse("Correo o contraseña incorrectos."));

        return Ok(result);
    }

    /// <summary>Devuelve los datos del usuario autenticado (a partir del token).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UsuarioDto> Me()
    {
        _ = int.TryParse(User.FindFirst("sub")?.Value, out var id);
        return Ok(new UsuarioDto(
            id,
            User.FindFirst("name")?.Value ?? string.Empty,
            User.FindFirst("email")?.Value ?? string.Empty,
            User.FindFirst("role")?.Value ?? string.Empty));
    }
}
