using Backend.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Endpoints de verificación de autorización por rol (HU-01, criterio 3).
/// Sin token → 401; token con otro rol → 403.
/// </summary>
[ApiController]
[Route("api/acceso")]
public class AccesoController : ControllerBase
{
    [HttpGet("docente")]
    [Authorize(Roles = RolNombres.Docente)]
    public IActionResult SoloDocente() => Ok(new { acceso = "docente", usuario = User.FindFirst("email")?.Value });

    [HttpGet("admin")]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    public IActionResult SoloAdministrador() => Ok(new { acceso = "administrador", usuario = User.FindFirst("email")?.Value });
}
