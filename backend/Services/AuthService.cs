using Backend.Data;
using Backend.Dtos;
using Backend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public interface IAuthService
{
    /// <summary>Devuelve null si las credenciales no son válidas (sin indicar qué dato falló).</summary>
    Task<LoginResponse?> LoginAsync(string correo, string password, CancellationToken ct = default);
}

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IOptions<AuthOptions> authOptions,
    TimeProvider clock) : IAuthService
{
    // Hash válido de una clave inexistente: se verifica cuando el usuario no existe o está
    // bloqueado para que el tiempo de respuesta no permita descubrir qué correos existen.
    private const string DummyHash =
        "v1.210000.vxHyyPRDgk0ZdslCz2okfw==.O/hm1Z27mTjI5q3bQtlvRubNk2IWVWjdT/jgZJ781bQ=";

    private readonly AuthOptions _opt = authOptions.Value;

    public async Task<LoginResponse?> LoginAsync(string correo, string password, CancellationToken ct = default)
    {
        var email = (correo ?? string.Empty).Trim().ToLowerInvariant();
        var clave = password ?? string.Empty;
        var ahora = clock.GetUtcNow().UtcDateTime;

        var usuario = await db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Correo == email, ct);

        if (usuario is null || !usuario.Activo || (usuario.BloqueadoHasta is { } hasta && hasta > ahora))
        {
            hasher.Verify(clave, DummyHash);
            return null;
        }

        if (!hasher.Verify(clave, usuario.PasswordHash))
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= _opt.MaxFailedAttempts)
            {
                usuario.BloqueadoHasta = ahora.AddMinutes(_opt.LockoutMinutes);
                usuario.IntentosFallidos = 0;
            }
            usuario.FechaActualizacion = ahora;
            await db.SaveChangesAsync(ct);
            return null;
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoAcceso = ahora;
        usuario.FechaActualizacion = ahora;
        await db.SaveChangesAsync(ct);

        var token = tokens.Crear(usuario);
        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            new UsuarioDto(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Rol.Nombre));
    }
}
