using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Dtos;

namespace Backend.Tests;

public class CursosEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> ObtenerTokenDocente()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            correo = ApiFactory.DocenteCorreo,
            password = ApiFactory.DocenteClave
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private async Task<string> ObtenerTokenAdmin()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            correo = ApiFactory.AdminCorreo,
            password = ApiFactory.AdminClave
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

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
    public async Task Docente_no_puede_crear_curso_devuelve_403()
    {
        var token = await ObtenerTokenDocente();
        var nuevo = new CrearCursoRequest("Redes", "RED-101", "2026-B", 20.0m);
        var resp = await SendAuth(HttpMethod.Post, "/api/cursos", token, nuevo);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
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
    public async Task Admin_puede_listar_docentes_disponibles()
    {
        var token = await ObtenerTokenAdmin();
        var resp = await SendAuth(HttpMethod.Get, "/api/cursos/docentes-disponibles", token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var docentes = await resp.Content.ReadFromJsonAsync<List<UsuarioDto>>();
        Assert.NotNull(docentes);
        Assert.Contains(docentes, d => d.Correo == ApiFactory.DocenteCorreo);
    }

    [Fact]
    public async Task Administrador_puede_crear_editar_y_eliminar_un_curso_con_paralelo_y_docente()
    {
        var adminToken = await ObtenerTokenAdmin();

        // 1. Obtener lista de docentes
        var docResp = await SendAuth(HttpMethod.Get, "/api/cursos/docentes-disponibles", adminToken);
        var docentes = await docResp.Content.ReadFromJsonAsync<List<UsuarioDto>>();
        Assert.NotEmpty(docentes!);
        var docenteId = docentes![0].Id;

        // 2. Admin crea curso según Mockup HU-02
        var nuevo = new CrearCursoRequest(
            "Bases de Datos",
            "BD-301",
            "Periodo 2026-A",
            20.0m,
            docenteId,
            "Paralelo \"A\" - Tercer Semestre");

        var postResp = await SendAuth(HttpMethod.Post, "/api/cursos", adminToken, nuevo);
        Assert.Equal(HttpStatusCode.Created, postResp.StatusCode);
        var creado = await postResp.Content.ReadFromJsonAsync<CursoDto>();
        Assert.NotNull(creado);
        Assert.Equal("Bases de Datos", creado.Nombre);
        Assert.Equal("BD-301", creado.Codigo);
        Assert.Equal("Paralelo \"A\" - Tercer Semestre", creado.Paralelo);
        Assert.Equal(docenteId, creado.DocenteId);

        // 3. Intentar duplicar código en mismo período para mismo docente -> 400
        var dupResp = await SendAuth(HttpMethod.Post, "/api/cursos", adminToken, nuevo);
        Assert.Equal(HttpStatusCode.BadRequest, dupResp.StatusCode);

        // 4. Editar curso
        var edit = new EditarCursoRequest(
            "Bases de Datos Avanzadas",
            "BD-301",
            "Periodo 2026-A",
            20.0m,
            docenteId,
            "Paralelo \"B\" - Tercer Semestre");

        var putResp = await SendAuth(HttpMethod.Put, $"/api/cursos/{creado.Id}", adminToken, edit);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var editado = await putResp.Content.ReadFromJsonAsync<CursoDto>();
        Assert.Equal("Bases de Datos Avanzadas", editado!.Nombre);
        Assert.Equal("Paralelo \"B\" - Tercer Semestre", editado.Paralelo);

        // 5. Eliminar curso
        var delResp = await SendAuth(HttpMethod.Delete, $"/api/cursos/{creado.Id}", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);
    }
}
