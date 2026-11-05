using Backend.Domain;
using Backend.Options;
using Backend.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Backend.Tests;

public class AuthServiceTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Credenciales_validas_devuelven_token_con_el_rol_docente()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock);

        var result = await service.LoginAsync("docente@uta.edu.ec", "Clave.Docente-1");

        Assert.NotNull(result);
        Assert.Equal(RolNombres.Docente, result!.Usuario.Rol);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);
        Assert.Equal(RolNombres.Docente, jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal("docente@uta.edu.ec", jwt.Claims.Single(c => c.Type == "email").Value);
    }

    [Fact]
    public async Task Credenciales_validas_de_administrador_devuelven_su_rol()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "admin@uta.edu.ec", "Clave.Admin-1", RolNombres.AdministradorTecnico);
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock);

        var result = await service.LoginAsync("admin@uta.edu.ec", "Clave.Admin-1");

        Assert.NotNull(result);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result!.Token);
        Assert.Equal(RolNombres.AdministradorTecnico, jwt.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public async Task El_token_expira_segun_la_configuracion()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var jwtOpts = new JwtOptions { Key = TestHelpers.ClaveJwt, ExpMinutes = 30 };
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock, jwtOptions: jwtOpts);

        var result = await service.LoginAsync("docente@uta.edu.ec", "Clave.Docente-1");

        Assert.NotNull(result);
        var esperado = _clock.GetUtcNow().UtcDateTime.AddMinutes(30);
        Assert.Equal(esperado, result!.ExpiresAt, TimeSpan.FromSeconds(1));
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);
        Assert.Equal(esperado, jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Clave_incorrecta_devuelve_null()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock);

        Assert.Null(await service.LoginAsync("docente@uta.edu.ec", "incorrecta"));
    }

    [Fact]
    public async Task Correo_inexistente_devuelve_null()
    {
        using var db = TestHelpers.NuevaBd();
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock);

        Assert.Null(await service.LoginAsync("nadie@uta.edu.ec", "Clave.Docente-1"));
    }

    [Fact]
    public async Task Usuario_inactivo_no_puede_ingresar()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "baja@uta.edu.ec", "Clave.Docente-1", activo: false);
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock);

        Assert.Null(await service.LoginAsync("baja@uta.edu.ec", "Clave.Docente-1"));
    }

    [Fact]
    public async Task El_correo_no_distingue_mayusculas_ni_espacios()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock);

        Assert.NotNull(await service.LoginAsync("  Docente@UTA.edu.ec ", "Clave.Docente-1"));
    }

    [Fact]
    public async Task Tras_varios_intentos_fallidos_la_cuenta_se_bloquea_aunque_la_clave_sea_correcta()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var opts = new AuthOptions { MaxFailedAttempts = 3, LockoutMinutes = 5 };
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock, opts);

        for (var i = 0; i < 3; i++)
            Assert.Null(await service.LoginAsync("docente@uta.edu.ec", "mala"));

        Assert.Null(await service.LoginAsync("docente@uta.edu.ec", "Clave.Docente-1"));
    }

    [Fact]
    public async Task El_bloqueo_expira_con_el_tiempo()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var opts = new AuthOptions { MaxFailedAttempts = 3, LockoutMinutes = 5 };
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock, opts);

        for (var i = 0; i < 3; i++)
            await service.LoginAsync("docente@uta.edu.ec", "mala");

        _clock.Avanzar(TimeSpan.FromMinutes(6));

        Assert.NotNull(await service.LoginAsync("docente@uta.edu.ec", "Clave.Docente-1"));
    }

    [Fact]
    public async Task Un_login_exitoso_reinicia_el_contador_de_intentos()
    {
        using var db = TestHelpers.NuevaBd();
        TestHelpers.AgregarUsuario(db, _hasher, "docente@uta.edu.ec", "Clave.Docente-1");
        var opts = new AuthOptions { MaxFailedAttempts = 3, LockoutMinutes = 5 };
        var service = TestHelpers.NuevoAuthService(db, _hasher, _clock, opts);

        await service.LoginAsync("docente@uta.edu.ec", "mala");
        await service.LoginAsync("docente@uta.edu.ec", "mala");
        Assert.NotNull(await service.LoginAsync("docente@uta.edu.ec", "Clave.Docente-1"));

        // Si el contador no se hubiera reiniciado, este fallo bloquearía la cuenta
        await service.LoginAsync("docente@uta.edu.ec", "mala");
        Assert.NotNull(await service.LoginAsync("docente@uta.edu.ec", "Clave.Docente-1"));
    }
}
