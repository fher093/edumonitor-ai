using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Dtos;

namespace Backend.Tests;

/// <summary>
/// Los cursos se crean en Moodle y se sincronizan: EduMonitor solo los consulta.
/// El administrador técnico ve los datos de los cursos (para verificar la sincronización) pero no su nómina.
/// </summary>
public class CursosEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> Login(string correo, string clave)
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { correo, password = clave });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private Task<string> ObtenerTokenDocente() => Login(ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);

    private Task<string> ObtenerTokenAdmin() => Login(ApiFactory.AdminCorreo, ApiFactory.AdminClave);

    private Task<HttpResponseMessage> SendAuth(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    [Fact]
    public async Task Listar_cursos_sin_token_devuelve_401()
    {
        var resp = await _client.GetAsync("/api/cursos");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Docente_puede_listar_sus_cursos_asignados_con_matriculados()
    {
        var token = await ObtenerTokenDocente();
        var resp = await SendAuth(HttpMethod.Get, "/api/cursos", token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cursos = await resp.Content.ReadFromJsonAsync<List<CursoDto>>();
        Assert.NotNull(cursos);
        Assert.NotEmpty(cursos);
        Assert.All(cursos, c => Assert.True(c.TotalEstudiantes >= 0));
    }

    [Fact]
    public async Task Admin_ve_los_cursos_pero_no_la_nomina()
    {
        var token = await ObtenerTokenAdmin();
        var cursos = await (await SendAuth(HttpMethod.Get, "/api/cursos", token))
            .Content.ReadFromJsonAsync<List<CursoDto>>();
        Assert.NotEmpty(cursos!);

        var nomina = await SendAuth(HttpMethod.Get, $"/api/cursos/{cursos![0].Id}/matriculas", token);
        Assert.Equal(HttpStatusCode.Forbidden, nomina.StatusCode);
    }

    [Fact]
    public async Task Cursos_matriculas_y_estudiantes_no_se_crean_en_EduMonitor()
    {
        var token = await ObtenerTokenAdmin();
        var curso = new { nombre = "Redes", codigo = "RED-101", periodo = "2026-B", escalaCalificacion = 20.0m };

        // Sin ruta de escritura: los cursos y la matrícula vienen de Moodle.
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await SendAuth(HttpMethod.Post, "/api/cursos", token, curso)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await SendAuth(HttpMethod.Put, "/api/cursos/1", token, curso)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await SendAuth(HttpMethod.Delete, "/api/cursos/1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await SendAuth(HttpMethod.Post, "/api/cursos/1/matriculas", token, new { estudianteId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAuth(HttpMethod.Post, "/api/estudiantes", token,
                new { identificacion = "1800000000", nombre = "X", correo = "x@uta.edu.ec" })).StatusCode);
    }
}
