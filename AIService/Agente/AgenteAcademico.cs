using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AIService.Agente;

/// <summary>
/// Orquestador del agente académico (HU-12 a HU-15, TT-09).
///
/// 1. Fase de herramientas: el modelo decide qué herramientas usar (máx. <see cref="MaxRondas"/> rondas).
/// 2. Consulta mínima garantizada: si el modelo no usó ninguna herramienta, se consultan el resumen y las
///    alertas del curso activo, para que la respuesta nunca salga "de memoria" (RN-09).
/// 3. Fase de respuesta: el modelo redacta un JSON con hechos, inferencias y recomendaciones (HU-14),
///    usando solo los datos de las herramientas.
/// 4. Verificación: se descartan las afirmaciones con cifras que no aparecen en los datos.
/// Si el modelo no devuelve un JSON válido, se responde con los datos de las herramientas sin redactar.
///
/// Ruta rápida (por defecto en producción): en las preguntas sobre un curso, el orquestador ejecuta
/// directamente las herramientas del análisis completo y el modelo se usa una sola vez, para redactar.
/// Con un modelo local, cada token generado cuesta ~65 ms: evitar la fase de herramientas ahorra 10-25 s.
/// </summary>
public sealed class AgenteAcademico(
    IModeloLenguaje modelo,
    HerramientasAcademicas herramientas,
    ILogger<AgenteAcademico> logger,
    IOptions<AgenteOptions>? opciones = null)
{
    // Sin opciones (pruebas unitarias del ciclo clásico) se usa la ruta en la que el modelo elige las herramientas.
    private readonly bool _rutaRapida = opciones?.Value.RutaRapida ?? false;

    public const int MaxRondas = 4;
    public const int MaxLlamadasPorRonda = 5;
    private const int MaxHistorial = 6;

    public async Task<RespuestaAgenteDto> ResponderAsync(PeticionChat peticion, ContextoConsulta contexto, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        var advertencias = new List<string>();
        var resultados = new List<ResultadoHerramienta>();

        var plan = _rutaRapida ? PlanRapido(peticion) : null;
        if (plan is not null)
        {
            // ------------------------------------------------ Ruta rápida: herramientas sin consultar al modelo
            foreach (var (nombre, curso) in plan)
            {
                var resultado = await EjecutarAutomaticaAsync(nombre, curso, contexto, ct);
                resultados.Add(resultado);
                if (resultado.SinAcceso) break; // curso ajeno o inexistente: no se insiste
            }
        }
        else
        {
            await FaseHerramientasAsync(peticion, contexto, resultados, advertencias, ct);
        }

        return await ResponderConDatosAsync(peticion, contexto, resultados, advertencias, reloj, ct);
    }

    /// <summary>Ciclo clásico: el modelo elige las herramientas; luego consulta mínima y análisis completo.</summary>
    private async Task FaseHerramientasAsync(
        PeticionChat peticion, ContextoConsulta contexto, List<ResultadoHerramienta> resultados,
        List<string> advertencias, CancellationToken ct)
    {
        var cache = new Dictionary<string, ResultadoHerramienta>();

        // ------------------------------------------------ 1. Fase de herramientas
        var mensajes = new List<MensajeChat> { new("system", Prompts.Herramientas(peticion.CursoId)) };
        mensajes.AddRange(Historial(peticion.Historial));
        mensajes.Add(new MensajeChat("user", peticion.Mensaje.Trim()));

        for (var ronda = 0; ronda < MaxRondas; ronda++)
        {
            var respuesta = await modelo.ChatAsync(mensajes, HerramientasAcademicas.Definiciones, null, ct);
            var llamadas = (respuesta.LlamadasHerramienta ?? []).Take(MaxLlamadasPorRonda).ToList();
            if (llamadas.Count == 0) break;

            mensajes.Add(new MensajeChat("assistant", respuesta.Contenido, llamadas));
            foreach (var llamada in llamadas)
            {
                var clave = $"{llamada.Nombre}|{llamada.Argumentos.GetRawText()}";
                if (!cache.TryGetValue(clave, out var resultado))
                {
                    resultado = await herramientas.EjecutarAsync(llamada.Nombre, llamada.Argumentos, contexto, ct);
                    cache[clave] = resultado;
                    resultados.Add(resultado);
                }
                mensajes.Add(new MensajeChat("tool", resultado.Contenido, NombreHerramienta: llamada.Nombre));
            }
        }

        // ------------------------------------------------ 2. Consulta mínima garantizada
        if (resultados.Count == 0)
        {
            advertencias.Add("El modelo no pidió herramientas; se consultaron automáticamente los datos del curso antes de responder.");
            // Si el docente nombra otro curso ("curso 999"), se consulta ese: el backend decide si tiene acceso.
            var cursoConsultado = CursoMencionado(peticion.Mensaje) ?? peticion.CursoId;
            if (cursoConsultado is { } id)
            {
                // Primero el resumen: si no hay acceso al curso, no se insiste con las demás herramientas.
                var primera = await EjecutarAutomaticaAsync(HerramientasAcademicas.GenerarResumen, id, contexto, ct);
                resultados.Add(primera);
                if (!primera.SinAcceso)
                    foreach (var nombre in HerramientasBase.Skip(1))
                        resultados.Add(await EjecutarAutomaticaAsync(nombre, id, contexto, ct));
            }
            else
            {
                resultados.Add(await EjecutarAutomaticaAsync(HerramientasAcademicas.ListarCursos, null, contexto, ct));
            }
        }
        else
        {
            // ------------------------------------------------ 2b. Análisis completo del curso (HU-13)
            // Si el modelo analizó un curso con alguna herramienta base, se completan las demás (resumen,
            // alertas y progreso) para que la respuesta no quede parcial. La traza indica quién las pidió.
            var cursosAnalizados = resultados
                .Where(r => r.Exito && HerramientasBase.Contains(r.Nombre) && Entero(r.Argumentos, "estudiante_id") is null)
                .Select(r => Entero(r.Argumentos, "curso_id"))
                .OfType<int>()
                .Distinct()
                .ToList();
            foreach (var curso in cursosAnalizados)
                foreach (var nombre in HerramientasBase.Where(n => !resultados.Any(r =>
                             r.Nombre == n && Entero(r.Argumentos, "curso_id") == curso && Entero(r.Argumentos, "estudiante_id") is null)))
                    resultados.Add(await EjecutarAutomaticaAsync(nombre, curso, contexto, ct));
        }
    }

    private async Task<RespuestaAgenteDto> ResponderConDatosAsync(
        PeticionChat peticion, ContextoConsulta contexto, List<ResultadoHerramienta> resultados,
        List<string> advertencias, Stopwatch reloj, CancellationToken ct)
    {
        var exitosos = resultados.Where(r => r.Exito).ToList();
        var traza = resultados
            .Select(r => new PasoTrazaDto(r.Nombre, r.Argumentos, r.Exito, r.CodigoHttp, r.DuracionMs, r.Error, r.Origen))
            .ToList();

        // Sin datos no hay respuesta: nunca se responde de memoria (RN-08, RN-09).
        if (exitosos.Count == 0)
        {
            var sinAcceso = resultados.FirstOrDefault(r => r.SinAcceso);
            return Crear(
                sinAcceso is not null ? "sin_acceso" : "error_herramientas",
                sinAcceso?.Error ?? "No pude consultar los datos académicos en este momento. Intenta de nuevo; no respondo sin datos.",
                [], [], [], [], null, advertencias, traza, contexto, reloj);
        }

        foreach (var fallida in resultados.Where(r => !r.Exito))
            advertencias.Add($"{fallida.Nombre}: {fallida.Error}");

        // ------------------------------------------------ 3. Fase de respuesta estructurada
        var fuentes = exitosos.Select(r => r.Nombre).Distinct().ToList();
        // Si hay recomendaciones deterministas (HU-15), el modelo solo elige cuáles mostrar por su número:
        // escribe muchos menos tokens y las recomendaciones quedan exactas.
        var recomendacionesBase = RecomendacionesBase(exitosos);
        var conBase = recomendacionesBase.Count > 0;
        var final = new List<MensajeChat>
        {
            new("system", conBase ? Prompts.RespuestaFinalConBase : Prompts.RespuestaFinal),
            new("user", MensajeFinal(peticion.Mensaje, resultados))
        };

        Borrador? borrador = null;
        for (var intento = 0; intento < 2 && borrador is null; intento++)
        {
            var respuesta = await modelo.ChatAsync(
                final, null, conBase ? Prompts.EsquemaRespuestaConBase : Prompts.EsquemaRespuesta, ct);
            borrador = Interpretar(respuesta.Contenido);
        }
        if (borrador is null)
        {
            logger.LogWarning("El modelo no devolvió un JSON válido; se usa la respuesta determinista.");
            advertencias.Add("El modelo no devolvió una respuesta estructurada; se muestran los datos de las herramientas sin redacción.");
            borrador = Determinista(exitosos);
        }

        // ------------------------------------------------ 4. Verificación de cifras (RN-09)
        var cifras = VerificadorHechos.CifrasDe(exitosos.Select(r => r.Contenido));
        var hechos = borrador.Hechos
            .Where(h => !string.IsNullOrWhiteSpace(h.Texto))
            .ToList();
        // Fuente inválida: si solo se consultó una herramienta, el hecho sale de ella; si no, se deja sin fuente.
        var hechosOk = hechos.Where(h => VerificadorHechos.EsVerificable(h.Texto, cifras))
            .Select(h => h with
            {
                Fuente = h.Fuente is not null && fuentes.Contains(h.Fuente) ? h.Fuente : fuentes.Count == 1 ? fuentes[0] : null
            })
            .ToList();
        var inferencias = borrador.Inferencias.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        var inferenciasOk = inferencias.Where(i => VerificadorHechos.EsVerificable(i, cifras)).ToList();
        var recomendaciones = borrador.Recomendaciones.Where(r => !string.IsNullOrWhiteSpace(r.Accion)).ToList();
        var recomendacionesOk = recomendaciones
            .Where(r => VerificadorHechos.EsVerificable($"{r.Accion} {r.Justificacion}", cifras))
            .ToList();
        if (conBase && recomendacionesOk.Count == 0)
        {
            // Las elegidas por número se copian tal cual del backend (no hay nada que verificar). Si el modelo
            // no eligió ninguna válida, se muestran las dos de mayor prioridad.
            var elegidas = borrador.IdsRecomendacion.Distinct().Where(recomendacionesBase.ContainsKey).Take(3).ToList();
            if (elegidas.Count == 0) elegidas = recomendacionesBase.Keys.Order().Take(2).ToList();
            recomendacionesOk = elegidas.Select(id => recomendacionesBase[id]).ToList();
        }

        var descartadas = hechos.Count - hechosOk.Count + inferencias.Count - inferenciasOk.Count +
                          recomendaciones.Count - recomendacionesOk.Count;
        if (descartadas > 0)
            advertencias.Add($"Se descartaron {descartadas} afirmación(es) con cifras que no coinciden con los datos de las herramientas.");

        var soloInsuficientes = exitosos.All(r => EsMensaje(r.Contenido, out var m) && m.StartsWith("Datos insuficientes", StringComparison.Ordinal));
        var estado = soloInsuficientes ? "sin_datos" : "ok";
        string? mensaje = soloInsuficientes && EsMensaje(exitosos[0].Contenido, out var primero) ? primero : null;

        return Crear(estado, mensaje, hechosOk, inferenciasOk, recomendacionesOk, fuentes,
            exitosos.Max(r => r.FechaCorte), advertencias, traza, contexto, reloj);
    }

    // ------------------------------------------------------------------ Auxiliares

    private sealed record Borrador(
        List<HechoDto> Hechos, List<string> Inferencias, List<RecomendacionAgenteDto> Recomendaciones, List<int> IdsRecomendacion);

    private static readonly Regex PideCursos = new(
        @"\b(mis\s+cursos|qu[eé]\s+cursos|otros\s+cursos|todos\s+(mis|los)\s+cursos|cu[aá]ntos\s+cursos|per[ií]odos?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PideEstudiantes = new(
        @"\b(notas?|calificaci\w*|estudiantes?|alumn\w*|qui[eé]n(es)?|aprob\w*|reprob\w*|promedios?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Herramientas de la ruta rápida, o null si la pregunta necesita que el modelo elija (listado de cursos,
    /// períodos o sin curso activo). Si el docente nombra otro curso, se consulta ese: el backend decide si
    /// tiene acceso (RN-12).
    /// </summary>
    public static IReadOnlyList<(string Nombre, int Curso)>? PlanRapido(PeticionChat peticion)
    {
        var curso = CursoMencionado(peticion.Mensaje) ?? peticion.CursoId;
        if (curso is null || PideCursos.IsMatch(peticion.Mensaje)) return null;
        var plan = HerramientasBase.Select(n => (n, curso.Value)).ToList();
        if (PideEstudiantes.IsMatch(peticion.Mensaje)) plan.Add((HerramientasAcademicas.ObtenerCalificaciones, curso.Value));
        return plan;
    }

    /// <summary>Recomendaciones deterministas devueltas por generar_resumen, por su número ("id").</summary>
    private static Dictionary<int, RecomendacionAgenteDto> RecomendacionesBase(IEnumerable<ResultadoHerramienta> exitosos)
    {
        var resultado = new Dictionary<int, RecomendacionAgenteDto>();
        foreach (var r in exitosos.Where(r => r.Nombre == HerramientasAcademicas.GenerarResumen))
        {
            if (JsonNode.Parse(r.Contenido)?["recomendaciones_base"] is not JsonArray lista) continue;
            foreach (var x in lista)
            {
                if (x?["id"] is null || x["accion"] is null) continue;
                resultado[x["id"]!.GetValue<int>()] = new RecomendacionAgenteDto(
                    x["accion"]!.GetValue<string>(), x["justificacion"]?.GetValue<string>() ?? string.Empty);
            }
        }
        return resultado;
    }

    private RespuestaAgenteDto Crear(
        string estado, string? mensaje, List<HechoDto> hechos, List<string> inferencias,
        List<RecomendacionAgenteDto> recomendaciones, List<string> fuentes, DateTime? fechaCorte,
        List<string> advertencias, List<PasoTrazaDto> traza, ContextoConsulta contexto, Stopwatch reloj) =>
        new(estado, mensaje, hechos, inferencias, recomendaciones, fuentes, fechaCorte, advertencias, traza,
            modelo.NombreModelo, contexto.ConsultaId, reloj.ElapsedMilliseconds);

    /// <summary>Herramientas de un análisis completo del curso (HU-13: progreso, alertas y resumen).</summary>
    public static readonly IReadOnlyList<string> HerramientasBase =
    [
        HerramientasAcademicas.GenerarResumen,
        HerramientasAcademicas.IdentificarAlertas,
        HerramientasAcademicas.ObtenerProgreso
    ];

    private async Task<ResultadoHerramienta> EjecutarAutomaticaAsync(string nombre, int? cursoId, ContextoConsulta contexto, CancellationToken ct)
    {
        var args = cursoId is null ? "{}" : $$"""{"curso_id":{{cursoId}}}""";
        var resultado = await herramientas.EjecutarAsync(nombre, JsonDocument.Parse(args).RootElement, contexto, ct);
        return resultado with { Origen = "orquestador" };
    }

    private static int? Entero(string argumentosJson, string nombre)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentosJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty(nombre, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
            return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out n) ? n : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"curso 9999", "curso con id 12", "curso_id=5" → número del curso mencionado.</summary>
    public static int? CursoMencionado(string mensaje)
    {
        var m = System.Text.RegularExpressions.Regex.Match(mensaje,
            @"\bcurso(?:_id)?\s*(?:=|:)?\s*(?:con\s+)?(?:el\s+)?(?:id|n[úu]mero|no\.?|nro\.?)?\s*[:=#]?\s*(\d{1,9})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    private static IEnumerable<MensajeChat> Historial(List<MensajeHistorial>? historial) =>
        (historial ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.Texto))
            .TakeLast(MaxHistorial)
            .Select(m => new MensajeChat(
                m.Rol is "docente" or "user" ? "user" : "assistant",
                m.Texto.Length <= 1000 ? m.Texto : m.Texto[..1000]));

    private static string MensajeFinal(string pregunta, IEnumerable<ResultadoHerramienta> resultados)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pregunta del docente: {pregunta.Trim()}");
        sb.AppendLine();
        sb.AppendLine("DATOS DE LAS HERRAMIENTAS:");
        foreach (var r in resultados)
            sb.AppendLine(r.Exito ? $"[{r.Nombre}] {r.Contenido}" : $"[{r.Nombre}] ERROR: {r.Error}");
        return sb.ToString();
    }

    private static Borrador? Interpretar(string contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return null;
        var texto = contenido.Trim();
        // Tolera bloques ```json ... ``` y texto alrededor del objeto.
        var inicio = texto.IndexOf('{');
        var fin = texto.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio) return null;
        try
        {
            using var doc = JsonDocument.Parse(texto[inicio..(fin + 1)]);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;

            var hechos = Arreglo(raiz, "hechos").Select(h => h.ValueKind == JsonValueKind.String
                    ? new HechoDto(h.GetString()!, null)
                    : new HechoDto(Cadena(h, "texto") ?? string.Empty, Cadena(h, "fuente")))
                .ToList();
            var inferencias = Arreglo(raiz, "inferencias")
                .Select(i => i.ValueKind == JsonValueKind.String ? i.GetString()! : Cadena(i, "texto") ?? string.Empty)
                .ToList();
            var recomendaciones = Arreglo(raiz, "recomendaciones").Select(r => r.ValueKind == JsonValueKind.String
                    ? new RecomendacionAgenteDto(r.GetString()!, string.Empty)
                    : new RecomendacionAgenteDto(Cadena(r, "accion") ?? string.Empty, Cadena(r, "justificacion") ?? string.Empty))
                .ToList();

            var ids = Arreglo(raiz, "recomendaciones_ids")
                .Select(i => i.ValueKind == JsonValueKind.Number && i.TryGetInt32(out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList();

            if (!raiz.TryGetProperty("hechos", out _) && !raiz.TryGetProperty("recomendaciones", out _) &&
                !raiz.TryGetProperty("recomendaciones_ids", out _)) return null;
            return new Borrador(hechos, inferencias, recomendaciones, ids);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Respuesta sin modelo: los hallazgos, alertas y recomendaciones base tal cual (deterministas).</summary>
    private static Borrador Determinista(IEnumerable<ResultadoHerramienta> exitosos)
    {
        var hechos = new List<HechoDto>();
        var recomendaciones = new List<RecomendacionAgenteDto>();
        foreach (var r in exitosos)
        {
            var nodo = JsonNode.Parse(r.Contenido)!.AsObject();
            if (nodo["mensaje"] is { } m) hechos.Add(new HechoDto(m.GetValue<string>(), r.Nombre));
            if (nodo["hallazgos"] is JsonArray hallazgos)
                hechos.AddRange(hallazgos.Select(h => new HechoDto(h!.GetValue<string>(), r.Nombre)));
            if (nodo["alertas"] is JsonArray alertas && r.Nombre == HerramientasAcademicas.IdentificarAlertas)
                hechos.AddRange(alertas.Take(5).Select(a => new HechoDto(
                    $"{a!["nombre"]}: {a["evidencia"]}", r.Nombre)));
            if (nodo["cursos"] is JsonArray cursos)
                hechos.AddRange(cursos.Select(c => new HechoDto(
                    $"{c!["nombre"]} ({c["codigo"]}, {c["periodo"]}): {c["total_estudiantes"]} estudiantes.", r.Nombre)));
            if (nodo["recomendaciones_base"] is JsonArray recs)
                recomendaciones.AddRange(recs.Take(4).Select(x => new RecomendacionAgenteDto(
                    x!["accion"]!.GetValue<string>(), $"{x["justificacion"]} ({x["indicador"]})")));
        }
        return new Borrador(hechos.Take(8).ToList(), [], recomendaciones, []);
    }

    private static bool EsMensaje(string contenido, out string mensaje)
    {
        mensaje = string.Empty;
        try
        {
            var nodo = JsonNode.Parse(contenido) as JsonObject;
            if (nodo is { Count: 1 } && nodo["mensaje"] is { } m)
            {
                mensaje = m.GetValue<string>();
                return true;
            }
        }
        catch (JsonException) { }
        return false;
    }

    private static IEnumerable<JsonElement> Arreglo(JsonElement raiz, string nombre) =>
        raiz.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : [];

    private static string? Cadena(JsonElement e, string nombre) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
