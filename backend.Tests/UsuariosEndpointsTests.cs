using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Dtos;

namespace Backend.Tests;

/// <summary>
/// HU-02: el administrador técnico gestiona las cuentas de los docentes
/// (crear, editar, activar/desactivar, desbloquear y restablecer la clave).
/// </summary>
public class UsuariosEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Ruta = "/api/usuarios/docentes";
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> Login(string correo, string clave) =>
        _client.PostAsJsonAsync("/api/auth/login", new { correo, password = clave });

    private async Task<string> Token(string correo, string clave)
    {
        var resp = await Login(correo, clave);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string url, string token, object? cuerpo = null)
    {
        var req = new HttpRequestMessage(metodo, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        return _client.SendAsync(req);
    }

    private static string CorreoUnico() => $"docente.{Guid.NewGuid():N}"[..20] + "@uta.edu.ec";

    private async Task<DocenteDto> CrearDocenteAsync(string admin, string correo, string clave = "Temporal2026")
    {
        var resp = await Enviar(HttpMethod.Post, Ruta, admin, new CrearDocenteRequest("Docente Nuevo", correo, clave));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<DocenteDto>())!;
    }

    [Fact]
    public async Task Admin_crea_un_docente_que_puede_iniciar_sesion()
    {
        var admin = await Token(ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        var correo = CorreoUnico();

        var creado = await CrearDocenteAsync(admin, correo.ToUpperInvariant());
        Assert.Equal(correo, creado.Correo); // se normaliza a minúsculas, igual que en el login
        Assert.True(creado.Activo);
        Assert.Equal(0, creado.CursosAsignados);

        Assert.Equal(HttpStatusCode.OK, (await Login(correo, "Temporal2026")).StatusCode);

        var lista = await (await Enviar(HttpMethod.Get, $"{Ruta}?q={Uri.EscapeDataString(correo)}", admin))
            .Content.ReadFromJsonAsync<List<DocenteDto>>();
        Assert.Single(lista!, d => d.Id == creado.Id);
    }

    [Fact]
    public async Task Rechaza_correo_duplicado_y_clave_debil()
    {
        var admin = await Token(ApiFactory.AdminCorreo, ApiFactory.AdminClave);

        var duplicado = await Enviar(HttpMethod.Post, Ruta, admin,
            new CrearDocenteRequest("Repetido", ApiFactory.DocenteCorreo, "Temporal2026"));
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);

        var sinNumeros = await Enviar(HttpMethod.Post, Ruta, admin,
            new CrearDocenteRequest("Clave Débil", CorreoUnico(), "solamenteletras"));
        Assert.Equal(HttpStatusCode.BadRequest, sinNumeros.StatusCode);

        var corta = await Enviar(HttpMethod.Post, Ruta, admin,
            new CrearDocenteRequest("Clave Corta", CorreoUnico(), "abc123"));
        Assert.Equal(HttpStatusCode.BadRequest, corta.StatusCode);
    }

    [Fact]
    public async Task Docente_no_puede_gestionar_cuentas()
    {
        var docente = await Token(ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, Ruta, docente)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, Ruta, docente,
            new CrearDocenteRequest("Intruso", CorreoUnico(), "Temporal2026"))).StatusCode);
    }

    [Fact]
    public async Task Desactivar_impide_el_login_y_reactivar_lo_permite()
    {
        var admin = await Token(ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        var correo = CorreoUnico();
        var docente = await CrearDocenteAsync(admin, correo);

        var desactivar = await Enviar(HttpMethod.Put, $"{Ruta}/{docente.Id}/estado", admin, new CambiarEstadoDocenteRequest(false));
        Assert.False((await desactivar.Content.ReadFromJsonAsync<DocenteDto>())!.Activo);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(correo, "Temporal2026")).StatusCode);

        await Enviar(HttpMethod.Put, $"{Ruta}/{docente.Id}/estado", admin, new CambiarEstadoDocenteRequest(true));
        Assert.Equal(HttpStatusCode.OK, (await Login(correo, "Temporal2026")).StatusCode);
    }

    [Fact]
    public async Task Desbloquear_y_restablecer_clave_tras_intentos_fallidos()
    {
        var admin = await Token(ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        var correo = CorreoUnico();
        var docente = await CrearDocenteAsync(admin, correo);

        for (var i = 0; i < 5; i++) await Login(correo, "ClaveIncorrecta1");
        var bloqueado = (await (await Enviar(HttpMethod.Get, $"{Ruta}?q={Uri.EscapeDataString(correo)}", admin))
            .Content.ReadFromJsonAsync<List<DocenteDto>>())!.Single();
        Assert.True(bloqueado.Bloqueado);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(correo, "Temporal2026")).StatusCode);

        var desbloqueo = await Enviar(HttpMethod.Post, $"{Ruta}/{docente.Id}/desbloquear", admin);
        Assert.False((await desbloqueo.Content.ReadFromJsonAsync<DocenteDto>())!.Bloqueado);
        Assert.Equal(HttpStatusCode.OK, (await Login(correo, "Temporal2026")).StatusCode);

        var debil = await Enviar(HttpMethod.Post, $"{Ruta}/{docente.Id}/restablecer-clave", admin, new RestablecerClaveRequest("corta1"));
        Assert.Equal(HttpStatusCode.BadRequest, debil.StatusCode);

        var reset = await Enviar(HttpMethod.Post, $"{Ruta}/{docente.Id}/restablecer-clave", admin, new RestablecerClaveRequest("NuevaClave2026"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(correo, "Temporal2026")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(correo, "NuevaClave2026")).StatusCode);
    }

    [Fact]
    public async Task Editar_correo_valida_duplicados_y_no_toca_administradores()
    {
        var admin = await Token(ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        var docente = await CrearDocenteAsync(admin, CorreoUnico());

        var nuevoCorreo = CorreoUnico();
        var editar = await Enviar(HttpMethod.Put, $"{Ruta}/{docente.Id}", admin, new EditarDocenteRequest("Nombre Editado", nuevoCorreo));
        var editado = (await editar.Content.ReadFromJsonAsync<DocenteDto>())!;
        Assert.Equal("Nombre Editado", editado.Nombre);
        Assert.Equal(nuevoCorreo, editado.Correo);

        var duplicado = await Enviar(HttpMethod.Put, $"{Ruta}/{docente.Id}", admin,
            new EditarDocenteRequest("Nombre Editado", ApiFactory.DocenteCorreo));
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);

        // Las cuentas de administrador no se gestionan desde esta ruta.
        var me = await (await Enviar(HttpMethod.Get, "/api/auth/me", admin)).Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal(HttpStatusCode.NotFound,
            (await Enviar(HttpMethod.Put, $"{Ruta}/{me!.Id}/estado", admin, new CambiarEstadoDocenteRequest(false))).StatusCode);
    }
}
