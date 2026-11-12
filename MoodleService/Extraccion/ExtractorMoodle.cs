using System.Diagnostics;
using System.Text.Json;
using MoodleService.Moodle;
using static MoodleService.Moodle.MoodleWsClient;

namespace MoodleService.Extraccion;

/// <summary>
/// Lee cursos, participantes, ítems de calificación, notas y entregas desde Moodle y los
/// normaliza al modelo de EduMonitor (RF-03). No escribe nada en Moodle.
/// </summary>
public class ExtractorMoodle(ILogger<ExtractorMoodle> logger)
{
    /// <summary>Funciones del servicio web que necesita la integración.</summary>
    public static readonly string[] FuncionesRequeridas =
    [
        "core_webservice_get_site_info",
        "core_course_get_courses",
        "core_course_get_categories",
        "core_enrol_get_enrolled_users",
        "core_grades_get_grade_tree",
        "gradereport_user_get_grade_items",
        "mod_assign_get_assignments",
        "mod_assign_get_submissions",
        "mod_quiz_get_quizzes_by_courses",
    ];

    private static readonly string[] RolesDocente = ["editingteacher", "teacher"];
    private const string RolEstudiante = "student";

    public async Task<PruebaConexionDto> ProbarAsync(MoodleWsClient cliente, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        try
        {
            var info = await cliente.LlamarAsync("core_webservice_get_site_info", null, ct);
            reloj.Stop();
            var disponibles = info.TryGetProperty("functions", out var fs)
                ? fs.EnumerateArray().Select(f => Texto(f, "name")).ToHashSet()
                : [];
            var faltantes = FuncionesRequeridas.Where(f => !disponibles.Contains(f)).ToList();
            var mensaje = faltantes.Count == 0
                ? "Conexión exitosa. El token tiene acceso a todas las funciones necesarias."
                : $"Conexión exitosa, pero faltan {faltantes.Count} funciones en el servicio externo de Moodle.";
            return new PruebaConexionDto(faltantes.Count == 0, mensaje, Texto(info, "sitename"),
                Texto(info, "release"), Texto(info, "username"), (int)reloj.ElapsedMilliseconds, faltantes, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is MoodleWsException or HttpRequestException or TaskCanceledException)
        {
            reloj.Stop();
            var mensaje = ex switch
            {
                MoodleWsException { Codigo: "invalidtoken" } => "Token inválido o revocado en Moodle.",
                MoodleWsException { Codigo: "webservicesnotenabled" or "enablewsdescription" } =>
                    "Los servicios web no están habilitados en Moodle.",
                MoodleWsException m => $"Moodle rechazó la solicitud: {m.MensajeMoodle}",
                TaskCanceledException => "Moodle no respondió a tiempo.",
                _ => $"No se pudo conectar con Moodle: {ex.Message}"
            };
            return new PruebaConexionDto(false, mensaje, null, null, null, (int)reloj.ElapsedMilliseconds,
                [], DateTime.UtcNow);
        }
    }

    public async Task<ExtraccionMoodleDto> ExtraerAsync(
        MoodleWsClient cliente, string urlMoodle, IReadOnlyCollection<long>? soloCursos, CancellationToken ct)
    {
        var advertencias = new List<AdvertenciaMoodleDto>();

        var info = await cliente.LlamarAsync("core_webservice_get_site_info", null, ct);
        var categorias = (await cliente.LlamarAsync("core_course_get_categories", null, ct))
            .EnumerateArray()
            .ToDictionary(c => Entero(c, "id"), c => Texto(c, "name"));

        var cursosMoodle = (await cliente.LlamarAsync("core_course_get_courses", null, ct))
            .EnumerateArray()
            .Where(c => Texto(c, "format") != "site" && Entero(c, "id") != 1)
            .Where(c => soloCursos is null || soloCursos.Count == 0 || soloCursos.Contains(Entero(c, "id")))
            .ToList();

        var idsCursos = cursosMoodle.Select(c => Entero(c, "id")).ToList();
        var fechasEntrega = await ObtenerFechasEntregaAsync(cliente, idsCursos, advertencias, ct);
        var entregas = await ObtenerEntregasAsync(cliente, fechasEntrega, advertencias, ct);

        var cursos = new List<CursoMoodleDto>();
        foreach (var c in cursosMoodle)
        {
            var id = Entero(c, "id");
            try
            {
                cursos.Add(await ExtraerCursoAsync(cliente, c, categorias, fechasEntrega, entregas, advertencias, ct));
            }
            catch (Exception ex) when (ex is MoodleWsException or HttpRequestException or JsonException)
            {
                logger.LogWarning(ex, "No se pudo leer el curso Moodle {CursoId}", id);
                advertencias.Add(new AdvertenciaMoodleDto(id,
                    $"Curso \"{Texto(c, "fullname")}\" omitido: {ex.Message}"));
            }
        }

        // URL pública del sitio (wwwroot): sirve para enlazar desde el navegador. La URL de conexión
        // puede ser interna (p. ej. http://moodle:8080 dentro de Docker).
        var urlPublica = NuloSiVacio(Texto(info, "siteurl")) ?? urlMoodle;
        return new ExtraccionMoodleDto(Texto(info, "sitename"), urlPublica, Texto(info, "release"),
            DateTime.UtcNow, cursos, advertencias);
    }

    private async Task<CursoMoodleDto> ExtraerCursoAsync(
        MoodleWsClient cliente,
        JsonElement curso,
        Dictionary<long, string> categorias,
        Dictionary<(string Modulo, long Instancia), (long Id, DateTime? Fecha)> fechasEntrega,
        HashSet<(long AsignacionId, long UsuarioId)> entregas,
        List<AdvertenciaMoodleDto> advertencias,
        CancellationToken ct)
    {
        var cursoId = Entero(curso, "id");
        var nombreCurso = Texto(curso, "fullname");
        var inicio = Fecha(curso, "startdate");
        var fin = Fecha(curso, "enddate");

        // Participantes.
        var participantes = await cliente.LlamarAsync("core_enrol_get_enrolled_users", [P("courseid", cursoId)], ct);
        var docentes = new List<UsuarioMoodleDto>();
        var estudiantes = new List<UsuarioMoodleDto>();
        foreach (var u in participantes.EnumerateArray())
        {
            var roles = u.TryGetProperty("roles", out var rs)
                ? rs.EnumerateArray().Select(r => Texto(r, "shortname")).ToList()
                : [];
            var usuario = new UsuarioMoodleDto(Entero(u, "id"), Texto(u, "fullname"), Texto(u, "email"),
                NuloSiVacio(Texto(u, "idnumber")), FotosMoodle.RutaDesdeUrl(Texto(u, "profileimageurl")));
            if (roles.Any(r => RolesDocente.Contains(r))) docentes.Add(usuario);
            else if (roles.Contains(RolEstudiante)) estudiantes.Add(usuario);
        }

        var sinCorreo = estudiantes.Count(e => string.IsNullOrWhiteSpace(e.Correo));
        if (sinCorreo > 0)
            advertencias.Add(new AdvertenciaMoodleDto(cursoId,
                $"{nombreCurso}: {sinCorreo} estudiante(s) sin correo visible para el usuario técnico."));

        // Estructura del libro de calificaciones: ítem -> ruta de categorías.
        var rutas = new Dictionary<long, List<string>>();
        var arbolTexto = (await cliente.LlamarAsync("core_grades_get_grade_tree", [P("courseid", cursoId)], ct)).GetString();
        if (!string.IsNullOrEmpty(arbolTexto))
            RecorrerArbol(JsonDocument.Parse(arbolTexto).RootElement, [], esRaiz: true, rutas);

        // Ítems y notas de todos los estudiantes.
        var reporte = await cliente.LlamarAsync("gradereport_user_get_grade_items", [P("courseid", cursoId)], ct);
        var actividades = new Dictionary<long, ActividadMoodleDto>();
        var calificaciones = new List<CalificacionMoodleDto>();
        var idsEstudiantes = estudiantes.Select(e => e.MoodleId).ToHashSet();

        foreach (var ug in reporte.GetProperty("usergrades").EnumerateArray())
        {
            var usuarioId = Entero(ug, "userid");
            foreach (var gi in ug.GetProperty("gradeitems").EnumerateArray())
            {
                var tipoItem = Texto(gi, "itemtype");
                if (tipoItem is not ("mod" or "manual")) continue;

                var itemId = Entero(gi, "id");
                var modulo = NuloSiVacio(Texto(gi, "itemmodule"));
                var instancia = Entero(gi, "iteminstance");
                fechasEntrega.TryGetValue((modulo ?? "", instancia), out var entregaInfo);

                if (!actividades.ContainsKey(itemId))
                {
                    var ubicacion = MapeoUta.Ubicar(
                        NuloSiVacio(Texto(gi, "idnumber")),
                        rutas.GetValueOrDefault(itemId) ?? [],
                        Texto(gi, "itemname"),
                        modulo, entregaInfo.Fecha, inicio, fin);

                    actividades[itemId] = new ActividadMoodleDto(
                        itemId,
                        Recortar(Texto(gi, "itemname"), 150),
                        MapeoUta.TipoDesdeModulo(modulo, tipoItem),
                        modulo,
                        ubicacion.Componente,
                        ubicacion.Parcial,
                        entregaInfo.Fecha,
                        Decimal(gi, "grademax") ?? 10m,
                        ubicacion.Metodo);
                }

                if (!idsEstudiantes.Contains(usuarioId)) continue;

                var nota = Decimal(gi, "graderaw");
                if (nota is not null)
                {
                    calificaciones.Add(new CalificacionMoodleDto(itemId, usuarioId, nota,
                        nota == 0 ? "nota_cero" : "calificada", Fecha(gi, "gradedategraded")));
                }
                else if (modulo == "assign" && entregas.Contains((entregaInfo.Id, usuarioId)))
                {
                    calificaciones.Add(new CalificacionMoodleDto(itemId, usuarioId, null, "completada", null));
                }
            }
        }

        var inferidas = actividades.Values.Where(a => a.Mapeo == "inferido").Select(a => a.Nombre).ToList();
        if (inferidas.Count > 0)
            advertencias.Add(new AdvertenciaMoodleDto(cursoId,
                $"{nombreCurso}: {inferidas.Count} ítem(s) fuera de las categorías Parcial/Componente UTA; " +
                $"su componente se infirió ({string.Join(", ", inferidas.Take(5))}{(inferidas.Count > 5 ? "…" : "")})."));

        var categoriaId = Entero(curso, "categoryid");
        return new CursoMoodleDto(
            cursoId,
            Recortar(Texto(curso, "shortname"), 50),
            Recortar(nombreCurso, 150),
            Recortar(categorias.GetValueOrDefault(categoriaId, "Sin período"), 50),
            !curso.TryGetProperty("visible", out var vis) || vis.ValueKind != JsonValueKind.Number || vis.GetInt32() == 1,
            docentes,
            estudiantes,
            actividades.Values.OrderBy(a => a.Parcial).ThenBy(a => a.FechaEntrega ?? DateTime.MaxValue).ToList(),
            calificaciones);
    }

    /// <summary>Fechas de entrega: tareas (duedate) y cuestionarios (timeclose), indexadas por (módulo, instancia).</summary>
    private async Task<Dictionary<(string, long), (long Id, DateTime? Fecha)>> ObtenerFechasEntregaAsync(
        MoodleWsClient cliente, List<long> cursos, List<AdvertenciaMoodleDto> advertencias, CancellationToken ct)
    {
        var resultado = new Dictionary<(string, long), (long, DateTime?)>();
        if (cursos.Count == 0) return resultado;

        try
        {
            var tareas = await cliente.LlamarAsync("mod_assign_get_assignments",
                [.. Arreglo("courseids", cursos), P("includenotenrolledcourses", 1)], ct);
            foreach (var c in tareas.GetProperty("courses").EnumerateArray())
            foreach (var a in c.GetProperty("assignments").EnumerateArray())
                resultado[("assign", Entero(a, "id"))] = (Entero(a, "id"), Fecha(a, "duedate"));
        }
        catch (MoodleWsException ex)
        {
            advertencias.Add(new AdvertenciaMoodleDto(null, $"No se pudieron leer las fechas de las tareas: {ex.MensajeMoodle}"));
        }

        try
        {
            var cuestionarios = await cliente.LlamarAsync("mod_quiz_get_quizzes_by_courses", [.. Arreglo("courseids", cursos)], ct);
            foreach (var q in cuestionarios.GetProperty("quizzes").EnumerateArray())
                resultado[("quiz", Entero(q, "id"))] = (Entero(q, "id"), Fecha(q, "timeclose"));
        }
        catch (MoodleWsException ex)
        {
            advertencias.Add(new AdvertenciaMoodleDto(null, $"No se pudieron leer las fechas de los cuestionarios: {ex.MensajeMoodle}"));
        }

        return resultado;
    }

    /// <summary>Entregas enviadas (status = submitted) de cada tarea, para el estado "completada".</summary>
    private static async Task<HashSet<(long, long)>> ObtenerEntregasAsync(
        MoodleWsClient cliente,
        Dictionary<(string Modulo, long Instancia), (long Id, DateTime? Fecha)> fechas,
        List<AdvertenciaMoodleDto> advertencias,
        CancellationToken ct)
    {
        var resultado = new HashSet<(long, long)>();
        var tareas = fechas.Keys.Where(k => k.Modulo == "assign").Select(k => k.Instancia).ToList();
        if (tareas.Count == 0) return resultado;

        try
        {
            var respuesta = await cliente.LlamarAsync("mod_assign_get_submissions", [.. Arreglo("assignmentids", tareas)], ct);
            foreach (var a in respuesta.GetProperty("assignments").EnumerateArray())
            {
                var tareaId = Entero(a, "assignmentid");
                foreach (var s in a.GetProperty("submissions").EnumerateArray())
                    if (Texto(s, "status") == "submitted")
                        resultado.Add((tareaId, Entero(s, "userid")));
            }
        }
        catch (MoodleWsException ex)
        {
            advertencias.Add(new AdvertenciaMoodleDto(null, $"No se pudieron leer las entregas de tareas: {ex.MensajeMoodle}"));
        }

        return resultado;
    }

    private static void RecorrerArbol(JsonElement nodo, List<string> ruta, bool esRaiz, Dictionary<long, List<string>> rutas)
    {
        var esCategoria = nodo.TryGetProperty("iscategory", out var ic) && ic.ValueKind == JsonValueKind.True;
        if (!esCategoria)
        {
            rutas[Entero(nodo, "id")] = ruta;
            return;
        }

        // La categoría raíz es el propio curso: no aporta información de parcial/componente.
        var nuevaRuta = esRaiz ? ruta : [.. ruta, Texto(nodo, "name")];
        if (nodo.TryGetProperty("children", out var hijos) && hijos.ValueKind == JsonValueKind.Array)
            foreach (var h in hijos.EnumerateArray())
                RecorrerArbol(h, nuevaRuta, esRaiz: false, rutas);
    }

    // ---------- utilidades de lectura JSON (Moodle mezcla números y cadenas) ----------

    private static string Texto(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            _ => ""
        } : "";

    private static long Entero(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) ? v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt64(),
            JsonValueKind.String when long.TryParse(v.GetString(), out var n) => n,
            _ => 0
        } : 0;

    private static decimal? Decimal(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? Math.Round(v.GetDecimal(), 2)
            : null;

    private static DateTime? Fecha(JsonElement e, string prop)
    {
        var segundos = Entero(e, prop);
        return segundos > 0 ? DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime : null;
    }

    private static string? NuloSiVacio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
