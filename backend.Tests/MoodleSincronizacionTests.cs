using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// RF-03: importación idempotente de los datos de Moodle, registro de la sincronización
/// (fecha, contadores y errores) y cursos sincronizados en solo lectura.
/// </summary>
public class MoodleSincronizacionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Datos de Moodle de prueba ----------

    private static UsuarioMoodleDto Docente(string correo = "docente@uta.edu.ec") => new(100, "Docente Moodle", correo, "1700000001");

    private static List<UsuarioMoodleDto> Estudiantes(int n) =>
        Enumerable.Range(1, n).Select(i => new UsuarioMoodleDto(200 + i, $"Estudiante {i}", $"est{i}@uta.edu.ec", $"18000000{i:00}",
            i == 1 ? RutaFoto : null)).ToList();

    private static List<ActividadMoodleDto> Actividades() =>
    [
        new(10, "Lección 1", "Cuestionario", "quiz", "contacto_docente", 1, Ahora.AddDays(-30), 10m, "categoria"),
        new(11, "Taller 1", "Tarea", "assign", "practico_experimental", 1, Ahora.AddDays(-20), 10m, "categoria"),
        new(12, "Tarea 1", "Tarea", "assign", "autonomo", 2, Ahora.AddDays(-10), 10m, "idnumber"),
    ];

    private static CursoMoodleDto Curso(
        long id = 5, string codigo = "BD-301", string periodo = "2026-B", string correoDocente = "docente@uta.edu.ec",
        int estudiantes = 3, List<ActividadMoodleDto>? actividades = null, List<CalificacionMoodleDto>? calificaciones = null) =>
        new(id, codigo, "Bases de Datos", periodo, true, [Docente(correoDocente)], Estudiantes(estudiantes),
            actividades ?? Actividades(),
            calificaciones ??
            [
                new(10, 201, 9m, "calificada", Ahora),
                new(10, 202, 0m, "calificada", Ahora),
                new(11, 201, null, "completada", null),
                new(12, 203, 7.5m, "calificada", Ahora),
            ]);

    private static ExtraccionMoodleDto Extraccion(params CursoMoodleDto[] cursos) =>
        new("Moodle de prueba", "http://moodle.test", "5.3", Ahora, [.. cursos], []);

    private static MoodleImportador Importador(Backend.Data.AppDbContext db) =>
        new(db, new FakeClock(new DateTimeOffset(Ahora)), NullLogger<MoodleImportador>.Instance);

    private static Usuario CrearDocente(Backend.Data.AppDbContext db, string correo = "docente@uta.edu.ec") =>
        TestHelpers.AgregarUsuario(db, new Pbkdf2PasswordHasher(), correo, "Clave.Prueba-2026");

    // ---------- Importador ----------

    [Fact]
    public async Task Importa_curso_estudiantes_matriculas_actividades_y_calificaciones()
    {
        using var db = TestHelpers.NuevaBd();
        var docente = CrearDocente(db);

        var r = await Importador(db).ImportarAsync(Extraccion(Curso()), CancellationToken.None);

        Assert.Equal(1, r.CursosImportados);
        Assert.Equal(3, r.Estudiantes.Count);
        Assert.Equal(3, r.ActividadesImportadas);
        Assert.Equal(4, r.CalificacionesImportadas);

        var curso = await db.Cursos.SingleAsync();
        Assert.Equal(OrigenesDatos.Moodle, curso.Origen);
        Assert.Equal(5, curso.MoodleCourseId);
        Assert.Equal(docente.Id, curso.DocenteId);
        Assert.Equal("2026-B", curso.Periodo);
        Assert.NotNull(curso.FechaSincronizacion);

        Assert.Equal(3, await db.Matriculas.CountAsync(m => m.CursoId == curso.Id && m.Estado == MatriculaEstados.Activa));
        Assert.Equal("1800000001", (await db.Estudiantes.SingleAsync(e => e.MoodleUserId == 201)).Identificacion);

        var cero = await db.Calificaciones.SingleAsync(c => c.Estudiante.MoodleUserId == 202);
        Assert.Equal(EstadosActividad.NotaCero, cero.Estado);
        var completada = await db.Calificaciones.SingleAsync(c => c.Estado == EstadosActividad.Completada);
        Assert.Null(completada.Nota);

        var tarea = await db.Actividades.SingleAsync(a => a.MoodleGradeItemId == 12);
        Assert.Equal(ComponentesEvaluacion.Autonomo, tarea.Componente);
        Assert.Equal(2, tarea.Parcial);
        Assert.Equal("idnumber", tarea.MapeoComponente);
    }

    [Fact]
    public async Task Omite_curso_cuyo_docente_no_tiene_cuenta_y_registra_el_error()
    {
        using var db = TestHelpers.NuevaBd();
        CrearDocente(db);

        var r = await Importador(db).ImportarAsync(
            Extraccion(Curso(), Curso(id: 6, codigo: "SO-301", correoDocente: "otro@uta.edu.ec")), CancellationToken.None);

        Assert.Equal(1, r.CursosImportados);
        Assert.Equal(1, r.CursosOmitidos);
        var error = Assert.Single(r.Detalle, d => d.Tipo == "error");
        Assert.Equal(6, error.CursoMoodleId);
        Assert.Contains("otro@uta.edu.ec", error.Mensaje);
        Assert.False(await db.Cursos.AnyAsync(c => c.MoodleCourseId == 6));
    }

    [Fact]
    public async Task Resincronizar_es_idempotente_y_refleja_bajas_de_Moodle()
    {
        using var db = TestHelpers.NuevaBd();
        CrearDocente(db);
        var importador = Importador(db);

        await importador.ImportarAsync(Extraccion(Curso()), CancellationToken.None);
        await importador.ImportarAsync(Extraccion(Curso()), CancellationToken.None);

        Assert.Equal(1, await db.Cursos.CountAsync());
        Assert.Equal(3, await db.Estudiantes.CountAsync());
        Assert.Equal(3, await db.Actividades.CountAsync());
        Assert.Equal(4, await db.Calificaciones.CountAsync());

        // En Moodle: se retira un estudiante, se elimina una actividad y se borra una nota.
        var cambiado = Curso(estudiantes: 2,
            actividades: Actividades().Where(a => a.GradeItemId != 12).ToList(),
            calificaciones: [new(10, 201, 9m, "calificada", Ahora)]);
        var r = await importador.ImportarAsync(Extraccion(cambiado), CancellationToken.None);

        Assert.Equal(1, r.MatriculasRetiradas);
        Assert.Equal(MatriculaEstados.Retirada, (await db.Matriculas.SingleAsync(m => m.Estudiante.MoodleUserId == 203)).Estado);
        Assert.Equal(2, await db.Actividades.CountAsync());
        Assert.Equal(1, await db.Calificaciones.CountAsync());
        Assert.Contains(r.Detalle, d => d.Tipo == "info" && d.Mensaje.Contains("ya no existen en Moodle"));
    }

    [Fact]
    public async Task Vincula_un_curso_local_con_el_mismo_codigo_periodo_y_docente()
    {
        using var db = TestHelpers.NuevaBd();
        var docente = CrearDocente(db);
        var local = new Curso
        {
            DocenteId = docente.Id, Nombre = "BD local", Codigo = "BD-301", Periodo = "2026-B",
            FechaCreacion = Ahora, FechaActualizacion = Ahora
        };
        db.Cursos.Add(local);
        db.Actividades.Add(new Actividad { Curso = local, Nombre = "Actividad local", Tipo = "Tarea", FechaCreacion = Ahora, FechaActualizacion = Ahora });
        await db.SaveChangesAsync();

        var r = await Importador(db).ImportarAsync(Extraccion(Curso()), CancellationToken.None);

        var curso = await db.Cursos.SingleAsync();
        Assert.Equal(local.Id, curso.Id);
        Assert.Equal(OrigenesDatos.Moodle, curso.Origen);
        Assert.False(await db.Actividades.AnyAsync(a => a.MoodleGradeItemId == null));
        Assert.Contains(r.Detalle, d => d.Tipo == "info" && d.Mensaje.Contains("se vinculó con Moodle"));
    }

    [Fact]
    public async Task Empareja_estudiante_local_existente_por_cedula()
    {
        using var db = TestHelpers.NuevaBd();
        CrearDocente(db);
        db.Estudiantes.Add(new Estudiante
        {
            Identificacion = "1800000001", Nombre = "Nombre antiguo", Correo = "antiguo@uta.edu.ec",
            FechaCreacion = Ahora, FechaActualizacion = Ahora
        });
        await db.SaveChangesAsync();

        await Importador(db).ImportarAsync(Extraccion(Curso()), CancellationToken.None);

        var est = await db.Estudiantes.SingleAsync(e => e.Identificacion == "1800000001");
        Assert.Equal(201, est.MoodleUserId);
        Assert.Equal("Estudiante 1", est.Nombre);
        Assert.Equal(3, await db.Estudiantes.CountAsync());
    }

    // ---------- Endpoints ----------

    private sealed class MoodleServiceFalso(Func<ExtraccionMoodleDto> datos) : IMoodleServiceClient
    {
        public string? UltimoToken { get; private set; }

        public Task<ExtraccionMoodleDto> ExtraerAsync(string bearerToken, IReadOnlyCollection<long>? cursos, CancellationToken ct)
        {
            UltimoToken = bearerToken;
            return Task.FromResult(datos());
        }

        public int FotosPedidas { get; private set; }
        public string? TokenFoto { get; private set; }

        public Task<(byte[] Datos, string Tipo)?> ObtenerFotoAsync(string bearerToken, string ruta, CancellationToken ct)
        {
            FotosPedidas++;
            TokenFoto = bearerToken;
            return Task.FromResult<(byte[], string)?>(ruta == RutaFoto ? (FotoPng, "image/png") : null);
        }
    }

    private sealed class MoodleServiceCaido : IMoodleServiceClient
    {
        public Task<ExtraccionMoodleDto> ExtraerAsync(string bearerToken, IReadOnlyCollection<long>? cursos, CancellationToken ct) =>
            throw new MoodleServiceException("El microservicio de Moodle no está disponible.");

        public Task<(byte[] Datos, string Tipo)?> ObtenerFotoAsync(string bearerToken, string ruta, CancellationToken ct) =>
            Task.FromResult<(byte[], string)?>(null);
    }

    // Foto de perfil del "Estudiante 1" (cabecera PNG mínima, suficiente para la prueba).
    private const string RutaFoto = "30/user/icon/boost/f1?rev=7";
    private static readonly byte[] FotoPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Foto_de_perfil_solo_la_ve_el_docente_del_curso()
    {
        await using var factory = new ApiFactory();
        var moodle = new MoodleServiceFalso(() => Extraccion(Curso(correoDocente: ApiFactory.DocenteCorreo)));
        await using var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMoodleServiceClient>(moodle)));
        using var cliente = app.CreateClient();
        var admin = await LoginAsync(cliente, ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        var docente = await LoginAsync(cliente, ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);
        await Enviar(cliente, HttpMethod.Post, "/api/sincronizacion/moodle", admin, new { });

        var curso = (await (await Enviar(cliente, HttpMethod.Get, "/api/cursos", docente)).Content
            .ReadFromJsonAsync<List<CursoDto>>())!.Single(c => c.Codigo == "BD-301");
        var matriculas = await (await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/matriculas", docente)).Content
            .ReadFromJsonAsync<List<MatriculaDto>>();
        var conFoto = matriculas!.Single(m => m.Nombre == "Estudiante 1").EstudianteId;
        var sinFoto = matriculas!.Single(m => m.Nombre == "Estudiante 2").EstudianteId;

        // Solo el estudiante que subió foto en Moodle aparece con foto.
        var ids = await (await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/fotos", docente)).Content
            .ReadFromJsonAsync<List<int>>();
        Assert.Equal([conFoto], ids);

        var foto = await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/estudiantes/{conFoto}/foto", docente);
        Assert.Equal(HttpStatusCode.OK, foto.StatusCode);
        Assert.Equal("image/png", foto.Content.Headers.ContentType?.MediaType);
        Assert.Equal(FotoPng, await foto.Content.ReadAsByteArrayAsync());
        Assert.Contains("private", foto.Headers.CacheControl!.ToString());

        // Se pide a MoodleService con el token técnico y luego se sirve desde la caché.
        await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/estudiantes/{conFoto}/foto", docente);
        Assert.Equal(1, moodle.FotosPedidas);
        Assert.NotEqual(docente, moodle.TokenFoto);

        Assert.Equal(HttpStatusCode.NotFound,
            (await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/estudiantes/{sinFoto}/foto", docente)).StatusCode);
        // Dato personal: el administrador técnico no accede (mínimo privilegio).
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/estudiantes/{conFoto}/foto", admin)).StatusCode);
    }

    private static HttpClient Cliente(ApiFactory factory, IMoodleServiceClient moodle) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMoodleServiceClient>(moodle))).CreateClient();

    private static async Task<string> LoginAsync(HttpClient cliente, string correo, string clave)
    {
        var resp = await cliente.PostAsJsonAsync("/api/auth/login", new { correo, password = clave });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    private static Task<HttpResponseMessage> Enviar(HttpClient c, HttpMethod m, string url, string token, object? cuerpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        return c.SendAsync(req);
    }

    [Fact]
    public async Task Admin_sincroniza_registra_historial_y_el_docente_ve_cursos_de_solo_lectura()
    {
        await using var factory = new ApiFactory();
        var moodle = new MoodleServiceFalso(() => Extraccion(Curso(correoDocente: ApiFactory.DocenteCorreo)));
        using var cliente = Cliente(factory, moodle);
        var admin = await LoginAsync(cliente, ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        var docente = await LoginAsync(cliente, ApiFactory.DocenteCorreo, ApiFactory.DocenteClave);

        // Solo el administrador técnico puede sincronizar.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Enviar(cliente, HttpMethod.Post, "/api/sincronizacion/moodle", docente, new { })).StatusCode);

        var resp = await Enviar(cliente, HttpMethod.Post, "/api/sincronizacion/moodle", admin, new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var sync = (await resp.Content.ReadFromJsonAsync<SincronizacionMoodleDto>())!;
        Assert.Equal(EstadosSincronizacion.Exitosa, sync.Estado);
        Assert.Equal(1, sync.CursosImportados);
        Assert.Equal(4, sync.CalificacionesImportadas);
        Assert.NotNull(sync.FechaFin);
        Assert.Equal(admin, moodle.UltimoToken); // se reenvía el JWT del administrador a MoodleService

        var historial = await (await Enviar(cliente, HttpMethod.Get, "/api/sincronizacion/moodle/historial", admin))
            .Content.ReadFromJsonAsync<List<SincronizacionMoodleDto>>();
        Assert.Single(historial!);

        var estado = await (await Enviar(cliente, HttpMethod.Get, "/api/sincronizacion/moodle/estado", docente))
            .Content.ReadFromJsonAsync<EstadoSincronizacionDto>();
        Assert.Equal(1, estado!.CursosMoodle);
        Assert.NotNull(estado.UltimaSincronizacion);

        var cursos = await (await Enviar(cliente, HttpMethod.Get, "/api/cursos", docente)).Content.ReadFromJsonAsync<List<CursoDto>>();
        var curso = Assert.Single(cursos!, c => c.Codigo == "BD-301");
        Assert.Equal(OrigenesDatos.Moodle, curso.Origen);
        Assert.Equal(5, curso.MoodleCourseId);
        Assert.Equal("http://moodle.test/course/view.php?id=5", curso.UrlMoodle);

        // Las actividades se consultan con un enlace al ítem en Moodle, donde se califican.
        var actividades = await (await Enviar(cliente, HttpMethod.Get, $"/api/cursos/{curso.Id}/actividades", docente))
            .Content.ReadFromJsonAsync<List<ActividadDto>>();
        var leccion = Assert.Single(actividades!, a => a.MoodleGradeItemId == 10);
        Assert.Equal("http://moodle.test/grade/report/singleview/index.php?id=5&item=grade&itemid=10", leccion.UrlMoodle);

        // EduMonitor no expone escritura de actividades ni notas: se gestionan en Moodle.
        var crear = await Enviar(cliente, HttpMethod.Post, $"/api/cursos/{curso.Id}/actividades", docente,
            new { nombre = "Local", tipo = "Tarea", componente = "autonomo", parcial = 1, puntajeMaximo = 10 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, crear.StatusCode);
        var calificar = await Enviar(cliente, HttpMethod.Post, $"/api/actividades/{leccion.Id}/calificaciones", docente,
            new { calificaciones = new[] { new { estudianteId = 1, nota = 5, estado = "calificada" } } });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, calificar.StatusCode);

        // El dashboard funciona sobre los datos importados.
        Assert.Equal(HttpStatusCode.OK,
            (await Enviar(cliente, HttpMethod.Get, $"/api/indicadores/curso/{curso.Id}", docente)).StatusCode);
    }

    [Fact]
    public async Task Sincronizacion_automatica_importa_sin_intervencion_del_administrador()
    {
        await using var factory = new ApiFactory();
        var moodle = new MoodleServiceFalso(() => Extraccion(Curso(correoDocente: ApiFactory.DocenteCorreo)));
        await using var app = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IMoodleServiceClient>(moodle)));
        using var cliente = app.CreateClient();

        var worker = new SincronizacionAutomaticaWorker(
            app.Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new Backend.Options.SincronizacionAutomaticaOptions
            {
                Habilitada = true, IntervaloMinutos = 60, RetrasoInicialSegundos = 0
            }),
            TimeProvider.System,
            NullLogger<SincronizacionAutomaticaWorker>.Instance);

        var admin = await LoginAsync(cliente, ApiFactory.AdminCorreo, ApiFactory.AdminClave);
        await worker.StartAsync(CancellationToken.None);

        // Espera a que termine la primera ejecución (se lanza sin retraso inicial).
        List<SincronizacionMoodleDto>? historial = null;
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < limite)
        {
            historial = await (await Enviar(cliente, HttpMethod.Get, "/api/sincronizacion/moodle/historial", admin))
                .Content.ReadFromJsonAsync<List<SincronizacionMoodleDto>>();
            if (historial is { Count: > 0 } && historial[0].Estado != EstadosSincronizacion.EnCurso) break;
            await Task.Delay(100);
        }
        await worker.StopAsync(CancellationToken.None);

        // Usa un token técnico con rol de administrador (lo que exige MoodleService), sin usuario.
        Assert.NotNull(moodle.UltimoToken);
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(moodle.UltimoToken);
        Assert.Equal(RolNombres.AdministradorTecnico, token.GetClaim("role").Value);
        Assert.True(token.ValidTo <= DateTime.UtcNow.AddMinutes(6));

        // Queda en el historial como ejecución automática y los datos se importan.
        var registro = Assert.Single(historial!);
        Assert.Equal(EstadosSincronizacion.Exitosa, registro.Estado);
        Assert.Null(registro.UsuarioCorreo);
        Assert.Equal(1, registro.CursosImportados);
    }

    [Fact]
    public async Task Sincronizacion_fallida_queda_registrada_con_el_error()
    {
        await using var factory = new ApiFactory();
        using var cliente = Cliente(factory, new MoodleServiceCaido());
        var admin = await LoginAsync(cliente, ApiFactory.AdminCorreo, ApiFactory.AdminClave);

        var sync = await (await Enviar(cliente, HttpMethod.Post, "/api/sincronizacion/moodle", admin, new { }))
            .Content.ReadFromJsonAsync<SincronizacionMoodleDto>();

        Assert.Equal(EstadosSincronizacion.Fallida, sync!.Estado);
        Assert.Contains("no está disponible", sync.Mensaje);
        Assert.Single(sync.Detalle, d => d.Tipo == "error");

        var historial = await (await Enviar(cliente, HttpMethod.Get, "/api/sincronizacion/moodle/historial", admin))
            .Content.ReadFromJsonAsync<List<SincronizacionMoodleDto>>();
        Assert.Equal(EstadosSincronizacion.Fallida, Assert.Single(historial!).Estado);
    }
}
