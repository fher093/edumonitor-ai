using Backend.Data;
using Backend.Domain;
using Backend.Options;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

public sealed class FakeClock(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _ahora = inicio;
    public override DateTimeOffset GetUtcNow() => _ahora;
    public void Avanzar(TimeSpan t) => _ahora += t;
}

public static class TestHelpers
{
    public const string ClaveJwt = "clave-de-pruebas-solo-para-tests-1234567890-abcdef";

    public static AppDbContext NuevaBd()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(opciones);
        db.Database.EnsureCreated(); // aplica los roles sembrados con HasData
        return db;
    }

    public static Usuario AgregarUsuario(AppDbContext db, IPasswordHasher hasher, string correo, string password,
        string rol = RolNombres.Docente, bool activo = true)
    {
        var rolId = db.Roles.Single(r => r.Nombre == rol).Id;
        var u = new Usuario
        {
            Nombre = "Usuario de prueba",
            Correo = correo.ToLowerInvariant(),
            PasswordHash = hasher.Hash(password),
            RolId = rolId,
            Activo = activo,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };
        db.Usuarios.Add(u);
        db.SaveChanges();
        return u;
    }

    public static AuthService NuevoAuthService(AppDbContext db, IPasswordHasher hasher, TimeProvider clock,
        AuthOptions? authOptions = null, JwtOptions? jwtOptions = null)
    {
        var jwt = jwtOptions ?? new JwtOptions { Key = ClaveJwt };
        var tokens = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(jwt), clock);
        return new AuthService(db, hasher, tokens,
            Microsoft.Extensions.Options.Options.Create(authOptions ?? new AuthOptions()), clock);
    }
}
