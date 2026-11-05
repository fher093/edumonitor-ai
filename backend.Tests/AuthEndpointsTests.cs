using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Dtos;

namespace Backend.Tests;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> PostLogin(string correo, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { correo, password });

    private async Task<string> ObtenerToken(string correo, string password)
    {
        var resp = await PostLogin(correo, password);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private Task<HttpResponseMessage> Get(string url, string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    [Fact]
    public async Task Login_valido_devuelve_200_con_token_y_rol()
    {
        var resp = await PostLogin(ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Docente", body.Usuario.Rol);
    }

    [Fact]
    public async Task Login_invalido_devuelve_401_con_el_mismo_mensaje_para_usuario_inexistente_y_clave_mala()
    {
        var malaClave = await PostLogin(ApiFactory.DocenteCorreo, "incorrecta");
        var noExiste = await PostLogin("fantasma@test.local", "incorrecta");

        Assert.Equal(HttpStatusCode.Unauthorized, malaClave.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noExiste.StatusCode);
        Assert.Equal(await malaClave.Content.ReadAsStringAsync(), await noExiste.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_con_campos_vacios_devuelve_400()
    {
        var resp = await PostLogin("", "");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Me_sin_token_devuelve_401()
    {
        var resp = await Get("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_con_token_devuelve_los_datos_del_usuario()
    {
        var token = await ObtenerToken(ApiFactory.AdminCorreo, ApiFactory.AdminClave);

        var resp = await Get("/api/auth/me", token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var me = await resp.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal(ApiFactory.AdminCorreo, me!.Correo);
        Assert.Equal("AdministradorTecnico", me.Rol);
    }

    [Fact]
    public async Task Token_manipulado_devuelve_401()
    {
        var token = await ObtenerToken(ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);
        var manipulado = token[..^3] + (token.EndsWith("AAA") ? "BBB" : "AAA");

        var resp = await Get("/api/auth/me", manipulado);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Docente_no_puede_acceder_a_la_zona_del_administrador()
    {
        var token = await ObtenerToken(ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);

        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/acceso/admin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/acceso/docente", token)).StatusCode);
    }

    [Fact]
    public async Task Administrador_no_puede_acceder_a_la_zona_del_docente()
    {
        var token = await ObtenerToken(ApiFactory.AdminCorreo, ApiFactory.AdminClave);

        Assert.Equal(HttpStatusCode.OK, (await Get("/api/acceso/admin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/acceso/docente", token)).StatusCode);
    }

    [Fact]
    public async Task Zonas_protegidas_sin_token_devuelven_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/acceso/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/acceso/docente")).StatusCode);
    }
}
