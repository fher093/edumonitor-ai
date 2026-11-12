using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Gestión de las cuentas de los docentes (HU-02, solo Administrador Técnico).
/// La sincronización con Moodle asigna cada curso al docente cuyo correo coincide con el de Moodle,
/// por eso el correo de la cuenta debe ser el mismo que el docente usa en Moodle.
/// Las cuentas no se eliminan (conservan sus cursos e historial): se desactivan.
/// </summary>
[ApiController]
[Route("api/usuarios/docentes")]
[Authorize(Roles = RolNombres.AdministradorTecnico)]
public class UsuariosController(AppDbContext db, IPasswordHasher hasher, TimeProvider clock) : ControllerBase
{
    /// <summary>Lista las cuentas de docentes, con búsqueda por nombre o correo.</summary>
    [HttpGet]
    [ProducesResponseType<List<DocenteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DocenteDto>>> Listar([FromQuery] string? q, CancellationToken ct)
    {
        var query = Docentes();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var filtro = q.Trim().ToLowerInvariant();
            query = query.Where(u => u.Nombre.ToLower().Contains(filtro) || u.Correo.Contains(filtro));
        }

        var ahora = Ahora();
        var docentes = await query
            .OrderBy(u => u.Nombre)
            .Select(u => new { Usuario = u, Cursos = u.Cursos.Count })
            .ToListAsync(ct);

        return Ok(docentes.Select(d => ADto(d.Usuario, d.Cursos, ahora)).ToList());
    }

    /// <summary>Crea la cuenta de un docente con una clave temporal.</summary>
    [HttpPost]
    [ProducesResponseType<DocenteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocenteDto>> Crear([FromBody] CrearDocenteRequest request, CancellationToken ct)
    {
        var correo = NormalizarCorreo(request.Correo);
        var errorClave = ValidarClave(request.ClaveTemporal);
        if (errorClave is not null) return BadRequest(new ErrorResponse(errorClave));
        if (await db.Usuarios.AnyAsync(u => u.Correo == correo, ct))
            return Conflict(new ErrorResponse($"Ya existe una cuenta con el correo {correo}."));

        var rolId = await db.Roles.Where(r => r.Nombre == RolNombres.Docente).Select(r => r.Id).FirstAsync(ct);
        var ahora = Ahora();
        var usuario = new Usuario
        {
            Nombre = request.Nombre.Trim(),
            Correo = correo,
            PasswordHash = hasher.Hash(request.ClaveTemporal),
            RolId = rolId,
            Activo = true,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Listar), null, ADto(usuario, 0, ahora));
    }

    /// <summary>Edita el nombre y el correo de un docente (el correo debe coincidir con Moodle).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<DocenteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocenteDto>> Editar(int id, [FromBody] EditarDocenteRequest request, CancellationToken ct)
    {
        var usuario = await Docentes().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NotFound(new ErrorResponse("Docente no encontrado."));

        var correo = NormalizarCorreo(request.Correo);
        if (await db.Usuarios.AnyAsync(u => u.Correo == correo && u.Id != id, ct))
            return Conflict(new ErrorResponse($"Ya existe una cuenta con el correo {correo}."));

        usuario.Nombre = request.Nombre.Trim();
        usuario.Correo = correo;
        usuario.FechaActualizacion = Ahora();
        await db.SaveChangesAsync(ct);

        return Ok(await DtoAsync(usuario, ct));
    }

    /// <summary>Activa o desactiva la cuenta. Un docente desactivado no puede iniciar sesión.</summary>
    [HttpPut("{id:int}/estado")]
    [ProducesResponseType<DocenteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocenteDto>> CambiarEstado(
        int id, [FromBody] CambiarEstadoDocenteRequest request, CancellationToken ct)
    {
        var usuario = await Docentes().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NotFound(new ErrorResponse("Docente no encontrado."));

        usuario.Activo = request.Activo;
        usuario.FechaActualizacion = Ahora();
        await db.SaveChangesAsync(ct);

        return Ok(await DtoAsync(usuario, ct));
    }

    /// <summary>Quita el bloqueo por intentos fallidos de inicio de sesión.</summary>
    [HttpPost("{id:int}/desbloquear")]
    [ProducesResponseType<DocenteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocenteDto>> Desbloquear(int id, CancellationToken ct)
    {
        var usuario = await Docentes().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NotFound(new ErrorResponse("Docente no encontrado."));

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.FechaActualizacion = Ahora();
        await db.SaveChangesAsync(ct);

        return Ok(await DtoAsync(usuario, ct));
    }

    /// <summary>Asigna una nueva clave temporal y desbloquea la cuenta.</summary>
    [HttpPost("{id:int}/restablecer-clave")]
    [ProducesResponseType<DocenteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocenteDto>> RestablecerClave(
        int id, [FromBody] RestablecerClaveRequest request, CancellationToken ct)
    {
        var usuario = await Docentes().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NotFound(new ErrorResponse("Docente no encontrado."));

        var errorClave = ValidarClave(request.ClaveTemporal);
        if (errorClave is not null) return BadRequest(new ErrorResponse(errorClave));

        usuario.PasswordHash = hasher.Hash(request.ClaveTemporal);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.FechaActualizacion = Ahora();
        await db.SaveChangesAsync(ct);

        return Ok(await DtoAsync(usuario, ct));
    }

    private IQueryable<Usuario> Docentes() =>
        db.Usuarios.Where(u => u.Rol.Nombre == RolNombres.Docente);

    private DateTime Ahora() => clock.GetUtcNow().UtcDateTime;

    private static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    /// <summary>Mínimo 8 caracteres con al menos una letra y un número.</summary>
    private static string? ValidarClave(string clave) =>
        clave.Length >= 8 && clave.Any(char.IsLetter) && clave.Any(char.IsDigit)
            ? null
            : "La clave temporal debe tener al menos 8 caracteres, con letras y números.";

    private async Task<DocenteDto> DtoAsync(Usuario usuario, CancellationToken ct)
    {
        var cursos = await db.Cursos.CountAsync(c => c.DocenteId == usuario.Id, ct);
        return ADto(usuario, cursos, Ahora());
    }

    private static DocenteDto ADto(Usuario u, int cursos, DateTime ahora)
    {
        var bloqueado = u.BloqueadoHasta is { } hasta && hasta > ahora;
        return new DocenteDto(u.Id, u.Nombre, u.Correo, u.Activo, bloqueado,
            bloqueado ? u.BloqueadoHasta : null, u.UltimoAcceso, cursos, u.FechaCreacion);
    }
}
