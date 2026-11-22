using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

namespace AIService.Agente;

/// <summary>Resultado de ejecutar una herramienta: lo que ve el modelo y lo que se muestra en la traza.</summary>
public sealed record ResultadoHerramienta(
    string Nombre,
    string Argumentos,
    bool Exito,
    int? CodigoHttp,
    string Contenido,
    string? Error,
    long DuracionMs,
    DateTime? FechaCorte,
    string Origen = "modelo")
{
    public bool SinAcceso => CodigoHttp is 403 or 404;
}

/// <summary>
/// Las 5 herramientas de consulta controladas del agente (contratos en docs/agent-tools-contracts.json).
/// Todas leen la API del backend con el JWT del docente; ninguna escribe datos (RN-10) ni calcula
/// indicadores por su cuenta (RN-03). Al modelo solo se envían el id y el nombre de los estudiantes,
/// nunca cédula ni correo (minimización de datos, RNF-02 / RN-11).
/// </summary>
public sealed class HerramientasAcademicas(IBackendAcademico backend)
{
    public const string ListarCursos = "listar_cursos_autorizados";
    public const string ObtenerCalificaciones = "obtener_calificaciones";
    public const string ObtenerProgreso = "obtener_progreso";
    public const string IdentificarAlertas = "identificar_alertas";
    public const string GenerarResumen = "generar_resumen";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Conserva tildes y eñes legibles para el modelo (por defecto se escaparían como XXXX).
    private static readonly JsonSerializerOptions JsonLegible = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static readonly IReadOnlyList<DefinicionHerramienta> Definiciones =
    [
        Def(ListarCursos,
            "Devuelve la lista de cursos asignados al docente autenticado, con código, nombre, período y total de matriculados.",
            """{"type":"object","properties":{"periodo":{"type":"string","description":"Opcional. Filtrar por período académico (ej. '2026-B')."}},"additionalProperties":false}"""),
        Def(ObtenerCalificaciones,
            "Consulta las calificaciones de los estudiantes matriculados en un curso autorizado, con opción de filtrar por estudiante.",
            """{"type":"object","properties":{"curso_id":{"type":"integer","description":"ID del curso autorizado."},"estudiante_id":{"type":"integer","description":"Opcional. ID de un estudiante específico."}},"required":["curso_id"],"additionalProperties":false}"""),
        Def(ObtenerProgreso,
            "Calcula el porcentaje de finalización de actividades y entregas por curso o estudiante.",
            """{"type":"object","properties":{"curso_id":{"type":"integer","description":"ID del curso."},"estudiante_id":{"type":"integer","description":"Opcional. ID del estudiante."}},"required":["curso_id"],"additionalProperties":false}"""),
        Def(IdentificarAlertas,
            "Aplica las reglas deterministas de alerta para listar estudiantes que requieren atención (descensos, atrasos reiterados o baja finalización), con su evidencia.",
            """{"type":"object","properties":{"curso_id":{"type":"integer","description":"ID del curso."},"tipo_alerta":{"type":"string","enum":["descenso","atraso","baja_finalizacion","todas"]}},"required":["curso_id"],"additionalProperties":false}"""),
        Def(GenerarResumen,
            "Sintetiza los indicadores agregados del curso (promedio, distribución, aprobados proyectados, actividades con bajo logro, tendencia, hallazgos y recomendaciones base justificadas) para fundamentar recomendaciones.",
            """{"type":"object","properties":{"curso_id":{"type":"integer","description":"ID del curso."}},"required":["curso_id"],"additionalProperties":false}""")
    ];

    public static readonly IReadOnlySet<string> Nombres = Definiciones.Select(d => d.Nombre).ToHashSet();

    private static DefinicionHerramienta Def(string nombre, string descripcion, string esquema) =>
        new(nombre, descripcion, JsonDocument.Parse(esquema).RootElement.Clone());

    public async Task<ResultadoHerramienta> EjecutarAsync(
        string nombre, JsonElement argumentos, ContextoConsulta contexto, CancellationToken ct)
    {
        var textoArgs = argumentos.ValueKind == JsonValueKind.Object ? argumentos.GetRawText() : "{}";
        var reloj = Stopwatch.StartNew();
        try
        {
            if (!Nombres.Contains(nombre))
                return Fallo(nombre, textoArgs, null, $"La herramienta «{nombre}» no existe.", reloj);

            var cursoId = Entero(argumentos, "curso_id");
            var estudianteId = Entero(argumentos, "estudiante_id");
            if (nombre != ListarCursos && cursoId is null)
                return Fallo(nombre, textoArgs, null, "Falta el parámetro obligatorio curso_id.", reloj);

            return nombre switch
            {
                ListarCursos => await ListarCursosAsync(argumentos, textoArgs, contexto, reloj, ct),
                ObtenerCalificaciones => estudianteId is null
                    ? await CalificacionesCursoAsync(cursoId!.Value, textoArgs, contexto, reloj, ct)
                    : await CalificacionesEstudianteAsync(cursoId!.Value, estudianteId.Value, textoArgs, contexto, reloj, ct),
                ObtenerProgreso => estudianteId is null
                    ? await ProgresoCursoAsync(cursoId!.Value, textoArgs, contexto, reloj, ct)
                    : await ProgresoEstudianteAsync(cursoId!.Value, estudianteId.Value, textoArgs, contexto, reloj, ct),
                IdentificarAlertas => await AlertasAsync(cursoId!.Value, Texto(argumentos, "tipo_alerta"), textoArgs, contexto, reloj, ct),
                _ => await ResumenAsync(cursoId!.Value, textoArgs, contexto, reloj, ct)
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return Fallo(nombre, textoArgs, null, "La respuesta del backend no tiene el formato esperado.", reloj);
        }
    }

    // ------------------------------------------------------------------ Herramientas

    private async Task<ResultadoHerramienta> ListarCursosAsync(
        JsonElement args, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var periodo = Texto(args, "periodo");
        var ruta = string.IsNullOrWhiteSpace(periodo) ? "api/cursos" : $"api/cursos?periodo={Uri.EscapeDataString(periodo)}";
        var r = await backend.GetAsync(ruta, ListarCursos, ctx, ct);
        if (!r.Exito) return Fallo(ListarCursos, textoArgs, r.CodigoHttp, null, reloj);

        var cursos = new JsonArray(r.Cuerpo!.Value.EnumerateArray().Select(c => (JsonNode?)new JsonObject
        {
            ["id"] = c.GetProperty("id").GetInt32(),
            ["codigo"] = c.GetProperty("codigo").GetString(),
            ["nombre"] = c.GetProperty("nombre").GetString(),
            ["periodo"] = c.GetProperty("periodo").GetString(),
            ["total_estudiantes"] = c.GetProperty("totalEstudiantes").GetInt32()
        }).ToArray());

        if (cursos.Count == 0)
            return Exito(ListarCursos, textoArgs, r.CodigoHttp, Mensaje("No se encontraron cursos asignados al docente en este período."), null, reloj);
        return Exito(ListarCursos, textoArgs, r.CodigoHttp, new JsonObject { ["cursos"] = cursos }, null, reloj);
    }

    private async Task<ResultadoHerramienta> CalificacionesCursoAsync(
        int cursoId, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var r = await backend.GetAsync($"api/indicadores/curso/{cursoId}", ObtenerCalificaciones, ctx, ct);
        if (!r.Exito) return Fallo(ObtenerCalificaciones, textoArgs, r.CodigoHttp, null, reloj);
        var d = r.Cuerpo!.Value;

        var estudiantes = d.GetProperty("estudiantes").EnumerateArray().ToList();
        if (estudiantes.All(e => e.GetProperty("promedioActividades").ValueKind == JsonValueKind.Null))
            return Exito(ObtenerCalificaciones, textoArgs, r.CodigoHttp,
                Mensaje("Datos insuficientes: No hay calificaciones registradas para el curso indicado."), Fecha(d), reloj);

        var resultado = new JsonObject
        {
            ["curso_id"] = cursoId,
            ["curso"] = d.GetProperty("cursoNombre").GetString(),
            ["escala_promedio"] = Num(d, "escalaCalificacion"),
            ["escala_nota_final"] = Num(d, "escalaNotaFinal"),
            ["nota_minima_aprobacion"] = 14,
            ["promedio_general"] = Num(d, "promedioCurso"),
            ["calificaciones"] = new JsonArray(estudiantes.Select(e => (JsonNode?)new JsonObject
            {
                ["estudiante_id"] = e.GetProperty("estudianteId").GetInt32(),
                ["nombre"] = e.GetProperty("nombre").GetString(),
                ["promedio"] = Num(e, "promedioActividades"),
                ["nota_parcial_1"] = Num(e, "notaParcial1"),
                ["nota_parcial_2"] = Num(e, "notaParcial2"),
                ["nota_final"] = Num(e, "notaFinal"),
                ["calificadas"] = e.GetProperty("calificadas").GetInt32(),
                ["nota_cero"] = e.GetProperty("notaCero").GetInt32(),
                ["sin_entrega"] = e.GetProperty("sinEntrega").GetInt32(),
                ["pendientes"] = e.GetProperty("pendientes").GetInt32(),
                ["alertas"] = e.GetProperty("alertas").GetInt32()
            }).ToArray()),
            ["advertencias"] = Lista(d, "advertencias")
        };
        return Exito(ObtenerCalificaciones, textoArgs, r.CodigoHttp, resultado, Fecha(d), reloj);
    }

    private async Task<ResultadoHerramienta> CalificacionesEstudianteAsync(
        int cursoId, int estudianteId, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var r = await backend.GetAsync($"api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}", ObtenerCalificaciones, ctx, ct);
        if (!r.Exito) return Fallo(ObtenerCalificaciones, textoArgs, r.CodigoHttp, null, reloj);
        var d = r.Cuerpo!.Value;

        var resultado = new JsonObject
        {
            ["curso_id"] = cursoId,
            ["estudiante_id"] = estudianteId,
            ["nombre"] = d.GetProperty("nombre").GetString(),
            ["escala_promedio"] = Num(d, "escalaCalificacion"),
            ["promedio"] = Num(d, "promedioActividades"),
            ["nota_parcial_1"] = Num(d, "notaParcial1"),
            ["nota_parcial_2"] = Num(d, "notaParcial2"),
            ["nota_final"] = Num(d, "notaFinal"),
            ["actividades"] = new JsonArray(d.GetProperty("actividades").EnumerateArray().Select(a => (JsonNode?)new JsonObject
            {
                ["nombre"] = a.GetProperty("nombre").GetString(),
                ["componente"] = a.GetProperty("componenteEtiquetaCorta").GetString(),
                ["parcial"] = a.GetProperty("parcial").GetInt32(),
                ["nota"] = Num(a, "nota"),
                ["puntaje_maximo"] = Num(a, "puntajeMaximo"),
                ["estado"] = a.GetProperty("estado").GetString()
            }).ToArray()),
            ["alertas"] = new JsonArray(d.GetProperty("alertas").EnumerateArray().Select(a => (JsonNode?)new JsonObject
            {
                ["tipo_alerta"] = a.GetProperty("tipo").GetString(),
                ["evidencia"] = a.GetProperty("evidencia").GetString(),
                ["regla_aplicada"] = a.GetProperty("regla").GetString()
            }).ToArray())
        };
        return Exito(ObtenerCalificaciones, textoArgs, r.CodigoHttp, resultado, FechaAhora(), reloj);
    }

    private async Task<ResultadoHerramienta> ProgresoCursoAsync(
        int cursoId, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var r = await backend.GetAsync($"api/indicadores/curso/{cursoId}", ObtenerProgreso, ctx, ct);
        if (!r.Exito) return Fallo(ObtenerProgreso, textoArgs, r.CodigoHttp, null, reloj);
        var d = r.Cuerpo!.Value;
        var p = d.GetProperty("progreso");

        if (d.GetProperty("actividadesTotales").GetInt32() == 0)
            return Exito(ObtenerProgreso, textoArgs, r.CodigoHttp,
                Mensaje("Datos insuficientes: No existen actividades evaluables configuradas."), Fecha(d), reloj);

        var resultado = new JsonObject
        {
            ["curso_id"] = cursoId,
            ["total_actividades"] = d.GetProperty("actividadesTotales").GetInt32(),
            ["actividades_evaluadas"] = d.GetProperty("actividadesEvaluadas").GetInt32(),
            ["total_estudiantes"] = d.GetProperty("totalMatriculados").GetInt32(),
            ["porcentaje_finalizacion"] = Num(p, "porcentajeFinalizacionVencidas"),
            ["porcentaje_avance"] = Num(p, "porcentajeAvance"),
            ["entregas_esperadas"] = p.GetProperty("totalEsperado").GetInt32(),
            ["entregas_completadas"] = p.GetProperty("completadas").GetInt32(),
            ["sin_entrega"] = p.GetProperty("sinEntrega").GetInt32(),
            ["pendientes_de_calificar"] = p.GetProperty("pendientes").GetInt32(),
            ["nota_cero"] = p.GetProperty("notaCero").GetInt32(),
            ["estudiantes_con_entregas_vencidas"] = new JsonArray(d.GetProperty("estudiantes").EnumerateArray()
                .Where(e => e.GetProperty("sinEntrega").GetInt32() > 0)
                .Select(e => (JsonNode?)new JsonObject
                {
                    ["estudiante_id"] = e.GetProperty("estudianteId").GetInt32(),
                    ["nombre"] = e.GetProperty("nombre").GetString(),
                    ["sin_entrega"] = e.GetProperty("sinEntrega").GetInt32(),
                    ["porcentaje_finalizacion"] = Num(e, "porcentajeFinalizacion")
                }).ToArray())
        };
        return Exito(ObtenerProgreso, textoArgs, r.CodigoHttp, resultado, Fecha(d), reloj);
    }

    private async Task<ResultadoHerramienta> ProgresoEstudianteAsync(
        int cursoId, int estudianteId, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var r = await backend.GetAsync($"api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}", ObtenerProgreso, ctx, ct);
        if (!r.Exito) return Fallo(ObtenerProgreso, textoArgs, r.CodigoHttp, null, reloj);
        var d = r.Cuerpo!.Value;

        var resultado = new JsonObject
        {
            ["curso_id"] = cursoId,
            ["estudiante_id"] = estudianteId,
            ["nombre"] = d.GetProperty("nombre").GetString(),
            ["total_actividades"] = d.GetProperty("actividadesTotales").GetInt32(),
            ["porcentaje_finalizacion"] = Num(d, "porcentajeFinalizacion"),
            ["calificadas"] = d.GetProperty("calificadas").GetInt32(),
            ["completadas_sin_nota"] = d.GetProperty("completadas").GetInt32(),
            ["sin_entrega"] = d.GetProperty("sinEntrega").GetInt32(),
            ["pendientes"] = d.GetProperty("pendientes").GetInt32(),
            ["nota_cero"] = d.GetProperty("notaCero").GetInt32()
        };
        return Exito(ObtenerProgreso, textoArgs, r.CodigoHttp, resultado, FechaAhora(), reloj);
    }

    private async Task<ResultadoHerramienta> AlertasAsync(
        int cursoId, string? tipo, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var r = await backend.GetAsync($"api/alertas/curso/{cursoId}", IdentificarAlertas, ctx, ct);
        if (!r.Exito) return Fallo(IdentificarAlertas, textoArgs, r.CodigoHttp, null, reloj);
        var d = r.Cuerpo!.Value;

        var filtrar = !string.IsNullOrWhiteSpace(tipo) && tipo != "todas";
        var alertas = d.GetProperty("alertas").EnumerateArray()
            .Where(a => !filtrar || a.GetProperty("tipo").GetString() == tipo)
            .Select(a => (JsonNode?)new JsonObject
            {
                ["estudiante_id"] = a.GetProperty("estudianteId").GetInt32(),
                ["nombre"] = a.GetProperty("nombre").GetString(),
                ["tipo_alerta"] = a.GetProperty("tipo").GetString(),
                ["severidad"] = a.GetProperty("severidad").GetString(),
                ["evidencia"] = a.GetProperty("evidencia").GetString(),
                ["regla_aplicada"] = a.GetProperty("regla").GetString()
            }).ToArray();

        if (alertas.Length == 0)
            return Exito(IdentificarAlertas, textoArgs, r.CodigoHttp,
                Mensaje("No se detectaron alertas académicas activas bajo las reglas configuradas."), Fecha(d), reloj);

        var resultado = new JsonObject
        {
            ["curso_id"] = cursoId,
            ["total_alertas"] = alertas.Length,
            ["estudiantes_afectados"] = alertas.Select(a => a!["estudiante_id"]!.GetValue<int>()).Distinct().Count(),
            ["alertas"] = new JsonArray(alertas),
            ["nota"] = "Las alertas son señales de seguimiento, no diagnósticos."
        };
        return Exito(IdentificarAlertas, textoArgs, r.CodigoHttp, resultado, Fecha(d), reloj);
    }

    private async Task<ResultadoHerramienta> ResumenAsync(
        int cursoId, string textoArgs, ContextoConsulta ctx, Stopwatch reloj, CancellationToken ct)
    {
        var dash = await backend.GetAsync($"api/indicadores/curso/{cursoId}", GenerarResumen, ctx, ct);
        if (!dash.Exito) return Fallo(GenerarResumen, textoArgs, dash.CodigoHttp, null, reloj);
        var analisis = await backend.GetAsync($"api/analisis/curso/{cursoId}", GenerarResumen, ctx, ct);
        if (!analisis.Exito) return Fallo(GenerarResumen, textoArgs, analisis.CodigoHttp, null, reloj);
        var recs = await backend.GetAsync($"api/analisis/curso/{cursoId}/recomendaciones", GenerarResumen, ctx, ct);
        if (!recs.Exito) return Fallo(GenerarResumen, textoArgs, recs.CodigoHttp, null, reloj);

        var d = dash.Cuerpo!.Value;
        var a = analisis.Cuerpo!.Value;
        var rc = recs.Cuerpo!.Value;

        if (d.GetProperty("estudiantesConNota").GetInt32() == 0)
            return Exito(GenerarResumen, textoArgs, dash.CodigoHttp,
                Mensaje("Datos insuficientes para generar el resumen estadístico del curso."), Fecha(d), reloj);

        var resultado = new JsonObject
        {
            ["curso_id"] = cursoId,
            ["curso"] = $"{d.GetProperty("cursoNombre").GetString()} ({d.GetProperty("cursoCodigo").GetString()})",
            ["periodo"] = d.GetProperty("periodo").GetString(),
            ["total_matriculados"] = d.GetProperty("totalMatriculados").GetInt32(),
            ["escala_promedio"] = Num(d, "escalaCalificacion"),
            ["promedio_curso"] = Num(d, "promedioCurso"),
            ["desviacion_estandar"] = Num(d, "desviacionEstandar"),
            ["aprobados_proyectados"] = d.GetProperty("aprobadosProyectados").GetInt32(),
            ["en_riesgo"] = d.GetProperty("estudiantesEnRiesgo").GetInt32(),
            ["actividades_totales"] = d.GetProperty("actividadesTotales").GetInt32(),
            ["actividades_evaluadas"] = d.GetProperty("actividadesEvaluadas").GetInt32(),
            ["distribucion"] = new JsonArray(d.GetProperty("distribucion").EnumerateArray().Select(x => (JsonNode?)new JsonObject
            {
                ["rango"] = x.GetProperty("etiqueta").GetString(),
                ["estudiantes"] = x.GetProperty("cantidad").GetInt32()
            }).ToArray()),
            ["actividades_bajo_60"] = new JsonArray(a.GetProperty("detalleActividades").EnumerateArray()
                .Where(x => x.GetProperty("resultadoBajo").GetBoolean())
                .Select(x => (JsonNode?)new JsonObject
                {
                    ["actividad_id"] = x.GetProperty("actividadId").GetInt32(),
                    ["nombre"] = x.GetProperty("nombre").GetString(),
                    ["parcial"] = x.GetProperty("parcial").GetInt32(),
                    ["logro_porcentaje"] = Num(x, "promedioPorcentaje")
                }).ToArray()),
            ["tendencia_general"] = a.GetProperty("tendenciaGeneral").GetString(),
            ["hallazgos"] = Lista(a, "hallazgos"),
            ["recomendaciones_base"] = new JsonArray(rc.GetProperty("recomendaciones").EnumerateArray().Select(x => (JsonNode?)new JsonObject
            {
                ["prioridad"] = x.GetProperty("prioridad").GetString(),
                ["accion"] = x.GetProperty("accion").GetString(),
                ["justificacion"] = x.GetProperty("justificacion").GetString(),
                ["indicador"] = x.GetProperty("indicador").GetString()
            }).ToArray()),
            ["advertencias"] = Lista(d, "advertencias")
        };
        return Exito(GenerarResumen, textoArgs, dash.CodigoHttp, resultado, Fecha(d), reloj);
    }

    // ------------------------------------------------------------------ Utilidades

    private static ResultadoHerramienta Exito(
        string nombre, string args, int? codigo, JsonNode contenido, DateTime? fecha, Stopwatch reloj) =>
        new(nombre, args, true, codigo, contenido.ToJsonString(JsonLegible), null, reloj.ElapsedMilliseconds, fecha);

    private static ResultadoHerramienta Fallo(string nombre, string args, int? codigo, string? error, Stopwatch reloj)
    {
        var mensaje = error ?? codigo switch
        {
            403 => "No tienes acceso a ese curso.",
            404 => "No se encontró el curso o el estudiante indicado entre tus cursos autorizados.",
            401 => "La sesión no es válida o expiró.",
            _ => "La herramienta no está disponible en este momento. Intenta de nuevo."
        };
        return new ResultadoHerramienta(nombre, args, false, codigo,
            new JsonObject { ["error"] = mensaje }.ToJsonString(JsonLegible), mensaje, reloj.ElapsedMilliseconds, null);
    }

    private static JsonObject Mensaje(string texto) => new() { ["mensaje"] = texto };

    private static JsonNode? Num(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.Number
            ? JsonValue.Create(Math.Round(v.GetDecimal(), 2))
            : null;

    private static JsonArray Lista(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.Array
            ? new JsonArray(v.EnumerateArray().Select(x => (JsonNode?)JsonValue.Create(x.GetString())).ToArray())
            : [];

    private static DateTime? Fecha(JsonElement e) =>
        e.TryGetProperty("fechaCalculo", out var v) && v.TryGetDateTime(out var f) ? f : null;

    private static DateTime FechaAhora() => DateTime.UtcNow;

    private static int? Entero(JsonElement args, string nombre)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(nombre, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, Inv, out n)) return n;
        return null;
    }

    private static string? Texto(JsonElement args, string nombre) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
