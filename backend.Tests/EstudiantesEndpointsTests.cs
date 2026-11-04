using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Dtos;

namespace Backend.Tests;

public class EstudiantesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private Task<HttpResponseMessage> SendAuth(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    [Fact]
    public async Task Registrar_estudiante_con_identificacion_unica_y_buscar()
    {
        var token = await ObtenerTokenDocente();
        var idUnica = "18" + Random.Shared.Next(10000000, 99999999);
        var req = new CrearEstudianteRequest(idUnica, "Estudiante Prueba", $"test.{idUnica}@uta.edu.ec");

        // 1. Registrar
        var postResp = await SendAuth(HttpMethod.Post, "/api/estudiantes", token, req);
        Assert.Equal(HttpStatusCode.Created, postResp.StatusCode);
        var creado = await postResp.Content.ReadFromJsonAsync<EstudianteDto>();
        Assert.NotNull(creado);
        Assert.Equal(idUnica, creado.Identificacion);

        // 2. Duplicado -> 400
        var dupResp = await SendAuth(HttpMethod.Post, "/api/estudiantes", token, req);
        Assert.Equal(HttpStatusCode.BadRequest, dupResp.StatusCode);

        // 3. Buscar por identificación
        var searchResp = await SendAuth(HttpMethod.Get, $"/api/estudiantes?q={idUnica}", token);
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var encontrados = await searchResp.Content.ReadFromJsonAsync<List<EstudianteDto>>();
        Assert.NotNull(encontrados);
        Assert.Contains(encontrados, e => e.Identificacion == idUnica);
    }

    [Fact]
    public async Task Matricular_y_desmatricular_estudiante_en_curso()
    {
        var token = await ObtenerTokenDocente();

        // 1. Obtener cursos del docente
        var cursosResp = await SendAuth(HttpMethod.Get, "/api/cursos", token);
        var cursos = await cursosResp.Content.ReadFromJsonAsync<List<CursoDto>>();
        Assert.NotEmpty(cursos!);
        var cursoId = cursos![0].Id;

        // 2. Registrar nuevo estudiante
        var idUnica = "18" + Random.Shared.Next(10000000, 99999999);
        var estReq = new CrearEstudianteRequest(idUnica, "Alumno Matricula", $"alumno.{idUnica}@uta.edu.ec");
        var estResp = await SendAuth(HttpMethod.Post, "/api/estudiantes", token, estReq);
        var nuevoEstudiante = await estResp.Content.ReadFromJsonAsync<EstudianteDto>();

        // 3. Matricular en el curso
        var matReq = new MatricularEstudianteRequest(nuevoEstudiante!.Id);
        var matResp = await SendAuth(HttpMethod.Post, $"/api/cursos/{cursoId}/matriculas", token, matReq);
        Assert.Equal(HttpStatusCode.Created, matResp.StatusCode);

        // 4. Intentar duplicar la matrícula -> 400
        var dupMatResp = await SendAuth(HttpMethod.Post, $"/api/cursos/{cursoId}/matriculas", token, matReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupMatResp.StatusCode);

        // 5. Listar matriculados y verificar
        var listaResp = await SendAuth(HttpMethod.Get, $"/api/cursos/{cursoId}/matriculas", token);
        Assert.Equal(HttpStatusCode.OK, listaResp.StatusCode);
        var matriculas = await listaResp.Content.ReadFromJsonAsync<List<MatriculaDto>>();
        Assert.Contains(matriculas!, m => m.EstudianteId == nuevoEstudiante.Id);

        // 6. Desmatricular
        var delResp = await SendAuth(HttpMethod.Delete, $"/api/cursos/{cursoId}/matriculas/{nuevoEstudiante.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // 7. Verificar que ya no está en la lista de activos
        var listaPostDel = await SendAuth(HttpMethod.Get, $"/api/cursos/{cursoId}/matriculas", token);
        var matriculasPost = await listaPostDel.Content.ReadFromJsonAsync<List<MatriculaDto>>();
        Assert.DoesNotContain(matriculasPost!, m => m.EstudianteId == nuevoEstudiante.Id);
    }
}
