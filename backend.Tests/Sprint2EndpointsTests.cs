using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests;

/// <summary>
/// Pruebas de integración del Sprint 2: actividades, calificaciones, permisos por curso,
/// indicadores, alertas y configuración de reglas (HU-05 a HU-11, RN-12).
/// </summary>
public class Sprint2EndpointsTests
{

    private sealed class Escenario : IAsyncDisposable
    {
        public required ApiFactory Factory { get; init; }
        public required HttpClient Cliente { get; init; }
        public required string TokenDocente { get; init; }
        public required string TokenAdmin { get; init; }

        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await Factory.DisposeAsync();
        }
    }

    private static async Task<Escenario> CrearEscenarioAsync()
    {
        var factory = new ApiFactory();
        var cliente = factory.CreateClient();
        var docente = await LoginAsync(cliente, ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);
        var admin = await LoginAsync(cliente, ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        return new Escenario
        {
            Factory = factory,
            Cliente = cliente,
            TokenDocente = docente,
            TokenAdmin = admin
        };
    }

    private static async Task<string> LoginAsync(HttpClient cliente, string correo, string clave)
    {
        var resp = await cliente.PostAsJsonAsync("/api/auth/login", new { correo, password = clave });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private static Task<HttpResponseMessage> Enviar(
        HttpClient cliente, HttpMethod metodo, string url, string token, object? cuerpo = null)
    {
        var req = new HttpRequestMessage(metodo, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        return cliente.SendAsync(req);
    }

    private static async Task<int> CursoPropioAsync(Escenario e)
    {
        var resp = await Enviar(e.Cliente, HttpMethod.Get, "/api/cursos", e.TokenDocente);
        resp.EnsureSuccessStatusCode();
        var cursos = await resp.Content.ReadFromJsonAsync<List<CursoDto>>();
        Assert.NotEmpty(cursos!);
        // El curso SW-401 es el que recibe las actividades y calificaciones de demostración.
        return (cursos!.FirstOrDefault(c => c.Codigo == "SW-401") ?? cursos![0]).Id;
    }

    private static async Task<int> EstudianteMatriculadoAsync(Escenario e, int cursoId)
    {
        var resp = await Enviar(e.Cliente, HttpMethod.Get, $"/api/cursos/{cursoId}/matriculas", e.TokenDocente);
        resp.EnsureSuccessStatusCode();
        var matriculas = await resp.Content.ReadFromJsonAsync<List<MatriculaDto>>();
        Assert.NotEmpty(matriculas!);
        return matriculas![0].EstudianteId;
    }

    private static async Task<int> CrearCursoAjenoAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var ahora = DateTime.UtcNow;

        var rolId = db.Roles.Single(r => r.Nombre == RolNombres.Docente).Id;
        var otro = new Usuario
        {
            Nombre = "Docente Ajeno",
            Correo = $"ajeno.{Guid.NewGuid():N}@test.local",
            PasswordHash = hasher.Hash("Ajeno.Test-2026"),
            RolId = rolId,
            Activo = true,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        db.Usuarios.Add(otro);
        await db.SaveChangesAsync();

        var curso = new Curso
        {
            DocenteId = otro.Id,
            Nombre = "Curso de otro docente",
            Codigo = ("AJ-" + Guid.NewGuid().ToString("N"))[..10],
            Periodo = "2026-B",
            EscalaCalificacion = 20m,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        return curso.Id;
    }

    private static CrearActividadRequest NuevaActividad(
        string nombre = "Actividad de prueba",
        string componente = ComponentesEvaluacion.Autonomo,
        decimal puntajeMaximo = 10m,
        int parcial = 1) =>
        new(nombre, "Tarea", componente, parcial, DateTime.UtcNow.AddDays(-1), puntajeMaximo);

    [Fact]
    public async Task Crear_listar_editar_y_eliminar_actividad()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);

        var crear = await Enviar(e.Cliente, HttpMethod.Post,
            $"/api/cursos/{cursoId}/actividades", e.TokenDocente, NuevaActividad());
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var actividad = await crear.Content.ReadFromJsonAsync<ActividadDto>();
        Assert.NotNull(actividad);
        Assert.Equal(cursoId, actividad!.CursoId);
        Assert.Equal(ComponentesEvaluacion.Autonomo, actividad.Componente);
        Assert.True(actividad.Vencida);
        Assert.Contains("Autónomo", actividad.ComponenteEtiquetaCorta);

        var lista = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/cursos/{cursoId}/actividades", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var actividades = await lista.Content.ReadFromJsonAsync<List<ActividadDto>>();
        Assert.Contains(actividades!, a => a.Id == actividad.Id);

        var editar = await Enviar(e.Cliente, HttpMethod.Put,
            $"/api/actividades/{actividad.Id}", e.TokenDocente,
            NuevaActividad("Actividad editada", ComponentesEvaluacion.ContactoDocente, 5m, 2));
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        var editada = await editar.Content.ReadFromJsonAsync<ActividadDto>();
        Assert.Equal("Actividad editada", editada!.Nombre);
        Assert.Equal(5m, editada.PuntajeMaximo);
        Assert.Equal(2, editada.Parcial);

        var eliminar = await Enviar(e.Cliente, HttpMethod.Delete,
            $"/api/actividades/{actividad.Id}", e.TokenDocente);
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);

        var obtener = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/actividades/{actividad.Id}", e.TokenDocente);
        Assert.Equal(HttpStatusCode.NotFound, obtener.StatusCode);
    }

    [Fact]
    public async Task Crear_actividad_con_componente_invalido_devuelve_400()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);

        var resp = await Enviar(e.Cliente, HttpMethod.Post,
            $"/api/cursos/{cursoId}/actividades", e.TokenDocente,
            NuevaActividad(componente: "examen_final"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Registrar_calificaciones_valida_nota_estado_y_matricula()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);
        var estudianteId = await EstudianteMatriculadoAsync(e, cursoId);

        var crear = await Enviar(e.Cliente, HttpMethod.Post,
            $"/api/cursos/{cursoId}/actividades", e.TokenDocente, NuevaActividad());
        var actividad = await crear.Content.ReadFromJsonAsync<ActividadDto>();
        var url = $"/api/actividades/{actividad!.Id}/calificaciones";

        // Nota fuera de rango
        var fueraRango = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(estudianteId, 11m, EstadosActividad.Calificada)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, fueraRango.StatusCode);

        // Estado inválido
        var estadoInvalido = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(estudianteId, 5m, "entregado")
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, estadoInvalido.StatusCode);

        // Calificada sin nota
        var sinNota = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(estudianteId, null, EstadosActividad.Calificada)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, sinNota.StatusCode);

        // Estudiante no matriculado
        var ajeno = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(999_999, 5m, EstadosActividad.Calificada)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, ajeno.StatusCode);

        // Estudiante repetido en el lote
        var repetido = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(estudianteId, 5m, EstadosActividad.Calificada),
                new RegistrarCalificacionItem(estudianteId, 6m, EstadosActividad.Calificada)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        // Registro válido
        var valido = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(estudianteId, 9m, EstadosActividad.Calificada, "Buen trabajo")
            ]));
        Assert.Equal(HttpStatusCode.OK, valido.StatusCode);
        var calificaciones = await valido.Content.ReadFromJsonAsync<List<CalificacionActividadDto>>();
        var propia = Assert.Single(calificaciones!, c => c.EstudianteId == estudianteId);
        Assert.Equal(9m, propia.Nota);
        Assert.Equal(EstadosActividad.Calificada, propia.Estado);
        Assert.Equal("Buen trabajo", propia.Observacion);

        // Nota 0 se normaliza al estado nota_cero
        var cero = await Enviar(e.Cliente, HttpMethod.Post, url, e.TokenDocente,
            new RegistrarCalificacionesRequest([
                new RegistrarCalificacionItem(estudianteId, 0m, EstadosActividad.Calificada)
            ]));
        Assert.Equal(HttpStatusCode.OK, cero.StatusCode);
        var calificacionesCero = await cero.Content.ReadFromJsonAsync<List<CalificacionActividadDto>>();
        var ceroDto = Assert.Single(calificacionesCero!, c => c.EstudianteId == estudianteId);
        Assert.Equal(0m, ceroDto.Nota);
        Assert.Equal(EstadosActividad.NotaCero, ceroDto.Estado);
    }

    [Fact]
    public async Task Docente_solo_accede_a_sus_propios_cursos_y_admin_a_todos()
    {
        await using var e = await CrearEscenarioAsync();
        var ajeno = await CrearCursoAjenoAsync(e.Factory);

        var endpoints = new[]
        {
            $"/api/indicadores/curso/{ajeno}",
            $"/api/cursos/{ajeno}/actividades",
            $"/api/alertas/curso/{ajeno}",
            $"/api/analisis/curso/{ajeno}",
            $"/api/cursos/{ajeno}/finalizacion"
        };

        foreach (var endpoint in endpoints)
        {
            var resp = await Enviar(e.Cliente, HttpMethod.Get, endpoint, e.TokenDocente);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }

        var crear = await Enviar(e.Cliente, HttpMethod.Post,
            $"/api/cursos/{ajeno}/actividades", e.TokenDocente, NuevaActividad());
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);

        // El administrador técnico puede consultar cualquier curso.
        var admin = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/indicadores/curso/{ajeno}", e.TokenAdmin);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);

        // Curso inexistente -> 404 (no filtra información de otros cursos).
        var inexistente = await Enviar(e.Cliente, HttpMethod.Get,
            "/api/indicadores/curso/999999", e.TokenDocente);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Indicadores_y_ficha_individual_reflejan_las_calificaciones()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);
        var estudianteId = await EstudianteMatriculadoAsync(e, cursoId);

        var dashboardResp = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/indicadores/curso/{cursoId}", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, dashboardResp.StatusCode);
        var dashboard = await dashboardResp.Content.ReadFromJsonAsync<DashboardCursoDto>();
        Assert.NotNull(dashboard);
        Assert.Equal(cursoId, dashboard!.CursoId);
        Assert.NotEmpty(dashboard.Estudiantes);
        Assert.Equal(5, dashboard.Distribucion.Count);
        Assert.Equal(6, dashboard.Componentes.Count);

        var fichaResp = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, fichaResp.StatusCode);
        var ficha = await fichaResp.Content.ReadFromJsonAsync<FichaEstudianteDto>();
        Assert.NotNull(ficha);
        Assert.Equal(estudianteId, ficha!.EstudianteId);
        Assert.NotEmpty(ficha.Actividades);
        Assert.Equal(6, ficha.Componentes.Count);

        // Estudiante no matriculado -> 404
        var noMatriculado = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/indicadores/curso/{cursoId}/estudiantes/999999", e.TokenDocente);
        Assert.Equal(HttpStatusCode.NotFound, noMatriculado.StatusCode);
    }

    [Fact]
    public async Task Alertas_incluyen_evidencia_y_reglas_configurables()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);

        var resp = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/alertas/curso/{cursoId}", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var alertas = await resp.Content.ReadFromJsonAsync<AlertasCursoDto>();
        Assert.NotNull(alertas);
        Assert.NotEmpty(alertas!.Alertas);
        Assert.All(alertas.Alertas, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Evidencia));
            Assert.False(string.IsNullOrWhiteSpace(a.Regla));
            Assert.Contains(a.Tipo, new[] { "descenso", "atraso", "baja_finalizacion" });
        });
        Assert.Equal(alertas.Alertas.Count, alertas.Resumen.Total);
        Assert.Equal(3, alertas.Reglas.Count);

        var reglasResp = await Enviar(e.Cliente, HttpMethod.Get,
            "/api/alertas/reglas", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, reglasResp.StatusCode);
        var reglas = await reglasResp.Content.ReadFromJsonAsync<List<ReglaAlertaDto>>();
        Assert.Equal(3, reglas!.Count);
        Assert.Contains(reglas, r => r.Tipo == "descenso");
    }

    [Fact]
    public async Task Solo_admin_puede_editar_reglas_y_validacion_de_parametros()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);

        var reglasResp = await Enviar(e.Cliente, HttpMethod.Get, "/api/alertas/reglas", e.TokenAdmin);
        var reglas = await reglasResp.Content.ReadFromJsonAsync<List<ReglaAlertaDto>>();
        var descenso = Assert.Single(reglas!, r => r.Tipo == "descenso");

        var docente = await Enviar(e.Cliente, HttpMethod.Put,
            $"/api/alertas/reglas/{descenso.Id}", e.TokenDocente,
            new EditarReglaAlertaRequest(true, "{\"k\":3,\"delta\":1.0}"));
        Assert.Equal(HttpStatusCode.Forbidden, docente.StatusCode);

        var jsonInvalido = await Enviar(e.Cliente, HttpMethod.Put,
            $"/api/alertas/reglas/{descenso.Id}", e.TokenAdmin,
            new EditarReglaAlertaRequest(true, "{no-es-json}"));
        Assert.Equal(HttpStatusCode.BadRequest, jsonInvalido.StatusCode);

        var parametrosInvalidos = await Enviar(e.Cliente, HttpMethod.Put,
            $"/api/alertas/reglas/{descenso.Id}", e.TokenAdmin,
            new EditarReglaAlertaRequest(true, "{\"k\":0,\"delta\":1.0}"));
        Assert.Equal(HttpStatusCode.BadRequest, parametrosInvalidos.StatusCode);

        var desactivar = await Enviar(e.Cliente, HttpMethod.Put,
            $"/api/alertas/reglas/{descenso.Id}", e.TokenAdmin,
            new EditarReglaAlertaRequest(false, "{\"k\":3,\"delta\":1.0}"));
        Assert.Equal(HttpStatusCode.OK, desactivar.StatusCode);
        var actualizada = await desactivar.Content.ReadFromJsonAsync<ReglaAlertaDto>();
        Assert.False(actualizada!.Activa);

        var alertasResp = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/alertas/curso/{cursoId}", e.TokenDocente);
        var alertas = await alertasResp.Content.ReadFromJsonAsync<AlertasCursoDto>();
        Assert.DoesNotContain(alertas!.Alertas, a => a.Tipo == "descenso");
    }

    [Fact]
    public async Task Filtros_de_periodo_y_busqueda_de_cursos()
    {
        await using var e = await CrearEscenarioAsync();

        var periodosResp = await Enviar(e.Cliente, HttpMethod.Get,
            "/api/cursos/periodos", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, periodosResp.StatusCode);
        var periodos = await periodosResp.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotEmpty(periodos!);

        var periodo = periodos![0];
        var porPeriodo = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/cursos?periodo={Uri.EscapeDataString(periodo)}", e.TokenDocente);
        var cursos = await porPeriodo.Content.ReadFromJsonAsync<List<CursoDto>>();
        Assert.NotEmpty(cursos!);
        Assert.All(cursos!, c => Assert.Equal(periodo, c.Periodo));

        var busqueda = await Enviar(e.Cliente, HttpMethod.Get,
            "/api/cursos?q=SW-401", e.TokenDocente);
        var encontrados = await busqueda.Content.ReadFromJsonAsync<List<CursoDto>>();
        Assert.Contains(encontrados!, c => c.Codigo == "SW-401");

        var matriculas = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/cursos/{cursos![0].Id}/matriculas?estado=Todas", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, matriculas.StatusCode);
    }

    [Fact]
    public async Task Finalizacion_del_curso_cuenta_estados_de_actividades()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);

        var resp = await Enviar(e.Cliente, HttpMethod.Get,
            $"/api/cursos/{cursoId}/finalizacion", e.TokenDocente);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var finalizacion = await resp.Content.ReadFromJsonAsync<FinalizacionCursoDto>();
        Assert.NotNull(finalizacion);
        Assert.NotEmpty(finalizacion!.Estudiantes);
        Assert.All(finalizacion.Estudiantes, est =>
        {
            Assert.True(est.TotalActividades >= 7);
            Assert.True(est.Completadas <= est.TotalActividades);
        });
    }

    [Fact]
    public async Task Acceso_sin_token_devuelve_401()
    {
        await using var e = await CrearEscenarioAsync();
        var cursoId = await CursoPropioAsync(e);

        var resp = await e.Cliente.GetAsync($"/api/indicadores/curso/{cursoId}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
