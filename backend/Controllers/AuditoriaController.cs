using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>Consulta de la auditoría (HU-17 / RF-12). Solo el administrador técnico.</summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = RolNombres.AdministradorTecnico)]
public class AuditoriaController(AppDbContext db) : ControllerBase
{
    public const int TamanoMaximo = 200;

    /// <summary>Registros de auditoría, del más reciente al más antiguo, filtrables y paginados (RNF-07).</summary>
    [HttpGet]
    [ProducesResponseType<PaginaAuditoriaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaAuditoriaDto>> Listar(
        [FromQuery] string? categoria,
        [FromQuery] string? accion,
        [FromQuery] string? usuario,
        [FromQuery] string? resultado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 50,
        CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamano = Math.Clamp(tamano, 1, TamanoMaximo);

        var consulta = db.RegistrosAuditoria.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(categoria)) consulta = consulta.Where(r => r.Categoria == categoria.Trim());
        if (!string.IsNullOrWhiteSpace(accion)) consulta = consulta.Where(r => r.Accion.Contains(accion.Trim()));
        if (!string.IsNullOrWhiteSpace(usuario))
            consulta = consulta.Where(r => r.UsuarioCorreo != null && r.UsuarioCorreo.Contains(usuario.Trim().ToLower()));
        if (!string.IsNullOrWhiteSpace(resultado)) consulta = consulta.Where(r => r.Resultado == resultado.Trim());
        if (desde is not null) consulta = consulta.Where(r => r.Fecha >= desde.Value.ToUniversalTime());
        if (hasta is not null) consulta = consulta.Where(r => r.Fecha <= hasta.Value.ToUniversalTime());

        var total = await consulta.CountAsync(ct);
        var registros = await consulta
            .OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(r => new RegistroAuditoriaDto(
                r.Id, r.Fecha, r.UsuarioId, r.UsuarioCorreo, r.Rol, r.Categoria, r.Accion,
                r.Recurso, r.Detalle, r.Resultado, r.CodigoHttp))
            .ToListAsync(ct);

        return Ok(new PaginaAuditoriaDto(total, pagina, tamano, registros));
    }
}
