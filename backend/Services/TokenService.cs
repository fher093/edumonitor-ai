using System.Security.Claims;
using System.Text;
using Backend.Domain;
using Backend.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public record TokenResult(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult Crear(Usuario usuario);

    /// <summary>
    /// Token técnico de corta duración (5 min) para que el backend llame a MoodleService en la
    /// sincronización automática, sin un usuario que haya iniciado sesión. Nunca sale del servidor.
    /// </summary>
    TokenResult CrearTokenSincronizacion();
}

/// <summary>Emite JWT firmados (HS256) con los claims sub, email, name y role.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public TokenResult Crear(Usuario usuario) => Emitir(
        [
            new("sub", usuario.Id.ToString()),
            new("email", usuario.Correo),
            new("name", usuario.Nombre),
            new("role", usuario.Rol.Nombre)
        ],
        TimeSpan.FromMinutes(_opt.ExpMinutes));

    public TokenResult CrearTokenSincronizacion() => Emitir(
        [
            new("sub", "sistema"),
            new("name", "Sincronización automática"),
            new("role", RolNombres.AdministradorTecnico)
        ],
        TimeSpan.FromMinutes(5));

    private TokenResult Emitir(List<Claim> claims, TimeSpan duracion)
    {
        var ahora = clock.GetUtcNow().UtcDateTime;
        var expira = ahora.Add(duracion);
        claims.Add(new Claim("jti", Guid.NewGuid().ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenResult(token, expira);
    }
}
