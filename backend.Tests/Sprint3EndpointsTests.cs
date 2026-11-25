using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Controllers;
using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests;

/// <summary>
/// Pruebas de integración del Sprint 3 (TT-10): recomendaciones (HU-15), informe (HU-16),
/// auditoría (HU-17), permisos (RN-12) y rendimiento (RNF-07).
/// </summary>
public class Sprint3EndpointsTests
{
    private sealed class Escenario : IAsyncDisposable
    {
        public required ApiFactory Factory { get; init; }
        public required HttpClient Cliente { get; init; }
        public required string TokenDocente { get; init; }
        public required string TokenAdmin { get; init; }
        public required int CursoId { get; init; }

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

        var resp = await Enviar(cliente, HttpMethod.Get, "/api/cursos", docente);
        resp.EnsureSuccessStatusCode();
        var cursos = await resp.Content.ReadFromJsonAsync<List<CursoDto>>();
        var cursoId = (cursos!.FirstOrDefault(c => c.Codigo == "SW-401") ?? cursos![0]).Id;

        return new Escenario
        {
            Factory = factory, Cliente = cliente, TokenDocente = docente, TokenAdmin = admin, CursoId = cursoId
        };
    }

    private static async Task<string> LoginAsync(HttpClient cliente, string correo, string clave)
    {
        var resp = await cliente.PostAsJsonAsync("/api/auth/login", new { correo, password = clave });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    private static Task<HttpResponseMessage> Enviar(
        HttpClient cliente, HttpMethod metodo, string url, string? token, object? cuerpo = null,
        Dictionary<string, string>? cabeceras = null)
    {
        var req = new HttpRequestMessage(metodo, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        foreach (var (k, v) in cabeceras ?? []) req.Headers.Add(k, v);
        return cliente.SendAsync(req);
    }

    private static async Task<int> CrearCursoAjenoAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ahora = DateTime.UtcNow;
        var otro = new Usuario
        {
            Nombre = "Docente Ajeno",
            Correo = $"ajeno.{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
            RolId = db.Roles.Single(r => r.Nombre == RolNombres.Docente).Id,
            Activo = true,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        db.Usuarios.Add(otro);
        await db.SaveChangesAsync();
        var curso = new Curso
        {
            DocenteId = otro.Id, Nombre = "Curso ajeno", Codigo = ("AJ-" + Guid.NewGuid().ToString("N"))[..10],
            Periodo = "2026-B", EscalaCalificacion = 20m, FechaCreacion = ahora, FechaActualizacion = ahora
        };
        db.Cursos.Add(curso);
        await db.SaveChangesAsync();
        return curso.Id;
    }

    private static async Task<List<RegistroAuditoriaDto>> AuditoriaAsync(Escenario e, string filtros = "")
    {
        var resp = await Enviar(e.Cliente, HttpMethod.Get, $"/api/auditoria?tamano=200{filtros}", e.TokenAdmin);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PaginaAuditoriaDto>())!.Registros;
    }

    // ------------------------------------------------------------------ HU-15

    [Fact]
    public async Task Recomendaciones_del_curso_tienen_accion_e_indicador()
    {
        await using var e = await CrearEscenarioAsync();

        var resp = await Enviar(e.Cliente, HttpMethod.Get, $"/api/analisis/curso/{e.CursoId}/recomendaciones", e.TokenDocente);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<RecomendacionesCursoDto>();
        Assert.NotEmpty(dto!.Recomendaciones); // la semilla incluye estudiantes con atraso y descenso
        Assert.All(dto.Recomendaciones, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Accion));
            Assert.False(string.IsNullOrWhiteSpace(r.Justificacion));
            Assert.False(string.IsNullOrWhiteSpace(r.Indicador));
            Assert.Contains(r.Prioridad, new[] { "alta", "media" });
        });
    }

    [Fact]
    public async Task Recomendaciones_de_curso_ajeno_devuelven_403()
    {
        await using var e = await CrearEscenarioAsync();
        var ajeno = await CrearCursoAjenoAsync(e.Factory);

        var resp = await Enviar(e.Cliente, HttpMethod.Get, $"/api/analisis/curso/{ajeno}/recomendaciones", e.TokenDocente);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ------------------------------------------------------------------ HU-16

    [Fact]
    public async Task Informe_incluye_encabezado_fecha_de_corte_y_todas_las_secciones()
    {
        await using var e = await CrearEscenarioAsync();

        var resp = await Enviar(e.Cliente, HttpMethod.Get, $"/api/informes/curso/{e.CursoId}", e.TokenDocente);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var informe = await resp.Content.ReadFromJsonAsync<InformeCursoDto>();
        Assert.Equal(e.CursoId, informe!.CursoId);
        Assert.False(string.IsNullOrWhiteSpace(informe.Periodo));
        Assert.False(string.IsNullOrWhiteSpace(informe.DocenteNombre));
        Assert.False(string.IsNullOrWhiteSpace(informe.FuenteDatos));
        Assert.True(informe.FechaCorte > DateTime.MinValue);
        // Todas las secciones usan la misma fecha de corte: el informe no recalcula por separado.
        Assert.Equal(informe.FechaCorte, informe.Metricas.FechaCalculo);
        Assert.Equal(informe.FechaCorte, informe.Analisis.FechaCalculo);
        Assert.Equal(informe.FechaCorte, informe.Alertas.FechaCalculo);
        Assert.Equal(informe.FechaCorte, informe.Recomendaciones.FechaCalculo);
        Assert.NotEmpty(informe.Analisis.Hallazgos);
        Assert.NotEmpty(informe.Alertas.Alertas);
    }

    [Fact]
    public async Task Informe_respeta_los_permisos_por_curso()
    {
        await using var e = await CrearEscenarioAsync();
        var ajeno = await CrearCursoAjenoAsync(e.Factory);

        var otroDocente = await Enviar(e.Cliente, HttpMethod.Get, $"/api/informes/curso/{ajeno}", e.TokenDocente);
        var admin = await Enviar(e.Cliente, HttpMethod.Get, $"/api/informes/curso/{e.CursoId}", e.TokenAdmin);
        var sinToken = await Enviar(e.Cliente, HttpMethod.Get, $"/api/informes/curso/{e.CursoId}", null);
        var inexistente = await Enviar(e.Cliente, HttpMethod.Get, "/api/informes/curso/999999", e.TokenDocente);

        Assert.Equal(HttpStatusCode.Forbidden, otroDocente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    // ------------------------------------------------------------------ HU-17

    [Fact]
    public async Task Auditoria_solo_es_accesible_para_el_administrador()
    {
        await using var e = await CrearEscenarioAsync();

        var docente = await Enviar(e.Cliente, HttpMethod.Get, "/api/auditoria", e.TokenDocente);
        var sinToken = await Enviar(e.Cliente, HttpMethod.Get, "/api/auditoria", null);

        Assert.Equal(HttpStatusCode.Forbidden, docente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
    }

    [Fact]
    public async Task Consulta_academica_queda_registrada_con_usuario_recurso_y_resultado()
    {
        await using var e = await CrearEscenarioAsync();

        await Enviar(e.Cliente, HttpMethod.Get, $"/api/indicadores/curso/{e.CursoId}", e.TokenDocente);

        var registros = await AuditoriaAsync(e, "&categoria=consulta");
        var r = Assert.Single(registros, x => x.Accion == "indicadores.dashboard");
        Assert.Equal(ApiFactory.DocenteCorreo, r.UsuarioCorreo);
        Assert.Equal(RolNombres.Docente, r.Rol);
        Assert.Equal($"curso/{e.CursoId}", r.Recurso);
        Assert.Equal("exito", r.Resultado);
        Assert.Equal(200, r.CodigoHttp);
    }

    [Fact]
    public async Task Herramienta_del_agente_queda_registrada_en_la_categoria_agente()
    {
        await using var e = await CrearEscenarioAsync();
        var cabeceras = new Dictionary<string, string>
        {
            [AuditarAttribute.CabeceraHerramienta] = "identificar_alertas",
            [AuditarAttribute.CabeceraConsulta] = "c0ffee12"
        };

        await Enviar(e.Cliente, HttpMethod.Get, $"/api/alertas/curso/{e.CursoId}", e.TokenDocente, cabeceras: cabeceras);
        // Un nombre de herramienta no válido no se registra como agente.
        await Enviar(e.Cliente, HttpMethod.Get, $"/api/alertas/curso/{e.CursoId}", e.TokenDocente,
            cabeceras: new() { [AuditarAttribute.CabeceraHerramienta] = "<script>" });

        var agente = await AuditoriaAsync(e, "&categoria=agente");
        var r = Assert.Single(agente);
        Assert.Equal("alertas.porcurso", r.Accion);
        Assert.Equal("herramienta=identificar_alertas; consulta=c0ffee12", r.Detalle);
        Assert.Equal(ApiFactory.DocenteCorreo, r.UsuarioCorreo);
    }

    [Fact]
    public async Task Acceso_denegado_a_curso_ajeno_queda_registrado()
    {
        await using var e = await CrearEscenarioAsync();
        var ajeno = await CrearCursoAjenoAsync(e.Factory);

        await Enviar(e.Cliente, HttpMethod.Get, $"/api/indicadores/curso/{ajeno}", e.TokenDocente);

        var denegados = await AuditoriaAsync(e, "&resultado=denegado");
        var r = Assert.Single(denegados);
        Assert.Equal($"curso/{ajeno}", r.Recurso);
        Assert.Equal(403, r.CodigoHttp);
    }

    [Fact]
    public async Task Cambio_de_regla_queda_registrado_con_sus_parametros()
    {
        await using var e = await CrearEscenarioAsync();
        var reglas = await (await Enviar(e.Cliente, HttpMethod.Get, "/api/alertas/reglas", e.TokenAdmin))
            .Content.ReadFromJsonAsync<List<ReglaAlertaDto>>();
        var atraso = reglas!.Single(r => r.Tipo == "atraso");

        var resp = await Enviar(e.Cliente, HttpMethod.Put, $"/api/alertas/reglas/{atraso.Id}", e.TokenAdmin,
            new { activa = true, parametrosJson = "{\"n\":3}" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var cambios = await AuditoriaAsync(e, "&categoria=cambio");
        var r = Assert.Single(cambios, x => x.Accion == "regla.editar");
        Assert.Equal(ApiFactory.AdminCorreo, r.UsuarioCorreo);
        Assert.Equal($"alertas/{atraso.Id}", r.Recurso);
        Assert.Contains("regla=atraso", r.Detalle);
        Assert.Contains("{\"n\":3}", r.Detalle);
    }

    [Fact]
    public async Task Login_se_audita_sin_guardar_contrasenas_ni_tokens()
    {
        await using var e = await CrearEscenarioAsync();
        const string claveErronea = "Clave-Secreta-Que-No-Debe-Guardarse-99";
        await e.Cliente.PostAsJsonAsync("/api/auth/login",
            new { correo = ApiFactory.DocenteCorreo, password = claveErronea });

        var accesos = await AuditoriaAsync(e, "&categoria=acceso");
        Assert.Contains(accesos, r => r.Resultado == "fallido" && r.UsuarioCorreo == ApiFactory.DocenteCorreo);
        Assert.Contains(accesos, r => r.Resultado == "exito" && r.UsuarioCorreo == ApiFactory.AdminCorreo);

        var todo = await AuditoriaAsync(e);
        var texto = string.Join("|", todo.Select(r => $"{r.Accion}{r.Recurso}{r.Detalle}{r.UsuarioCorreo}"));
        Assert.DoesNotContain(claveErronea, texto);
        Assert.DoesNotContain(ApiFactory.DocenteClave, texto);
        Assert.DoesNotContain(e.TokenDocente, texto);
        Assert.DoesNotContain("eyJ", texto);
    }

    [Fact]
    public async Task Auditoria_esta_paginada_y_ordenada_de_la_mas_reciente()
    {
        await using var e = await CrearEscenarioAsync();
        for (var i = 0; i < 5; i++)
            await Enviar(e.Cliente, HttpMethod.Get, $"/api/alertas/curso/{e.CursoId}", e.TokenDocente);

        var resp = await Enviar(e.Cliente, HttpMethod.Get, "/api/auditoria?pagina=2&tamano=2", e.TokenAdmin);
        var pagina = await resp.Content.ReadFromJsonAsync<PaginaAuditoriaDto>();

        Assert.Equal(2, pagina!.Pagina);
        Assert.Equal(2, pagina.Registros.Count);
        Assert.True(pagina.Total >= 7); // 2 logins + 5 consultas
        var todos = await AuditoriaAsync(e);
        Assert.Equal(todos.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).Select(r => r.Id), todos.Select(r => r.Id));
    }

    [Theory]
    [InlineData("token=abc123", "token=[omitido]")]
    [InlineData("password: Secreta1", "password=[omitido]")]
    [InlineData("Authorization Bearer xyz.abc", "Authorization Bearer [omitido]")]
    [InlineData("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.firma", "jwt [token omitido]")]
    [InlineData("regla=atraso; activa=true", "regla=atraso; activa=true")]
    public void Sanitizar_elimina_secretos_del_detalle(string entrada, string esperado)
    {
        Assert.Equal(esperado, AuditoriaService.Sanitizar(entrada));
    }

    // ------------------------------------------------------------------ RNF-07

    [Fact]
    public async Task Indicadores_alertas_e_informe_responden_en_tiempo_acotado()
    {
        await using var e = await CrearEscenarioAsync();
        var rutas = new[]
        {
            $"/api/indicadores/curso/{e.CursoId}",
            $"/api/alertas/curso/{e.CursoId}",
            $"/api/analisis/curso/{e.CursoId}/recomendaciones",
            $"/api/informes/curso/{e.CursoId}"
        };
        foreach (var ruta in rutas) // calentamiento (compilación JIT y primera consulta)
            (await Enviar(e.Cliente, HttpMethod.Get, ruta, e.TokenDocente)).EnsureSuccessStatusCode();

        const int repeticiones = 10;
        foreach (var ruta in rutas)
        {
            var reloj = Stopwatch.StartNew();
            for (var i = 0; i < repeticiones; i++)
                (await Enviar(e.Cliente, HttpMethod.Get, ruta, e.TokenDocente)).EnsureSuccessStatusCode();
            var promedioMs = reloj.Elapsed.TotalMilliseconds / repeticiones;
            Assert.True(promedioMs < 500, $"{ruta} tardó {promedioMs:0} ms de media (límite 500 ms).");
        }
    }
}
