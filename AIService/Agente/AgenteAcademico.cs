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

        var ocultos = new HashSet<ResultadoHerramienta>(ReferenceEqualityComparer.Instance);
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
            if (!resultados.Any(r => r.SinAcceso))
                await AgregarFichaDelEstudianteAsync(peticion, plan[0].Curso, contexto, resultados, ocultos, ct);
        }
        else
        {
            await FaseHerramientasAsync(peticion, contexto, resultados, advertencias, ct);
        }

        return await ResponderConDatosAsync(peticion, contexto, resultados, ocultos, advertencias, reloj, ct);
    }

    /// <summary>
    /// Si la pregunta (o la conversación reciente) nombra a un estudiante del curso, consulta su ficha individual:
    /// así el agente responde con sus notas por actividad y componente, no con datos del grupo. La nómina del curso
    /// se usa para reconocer el nombre; si la pregunta no trata de estudiantes, no se envía al modelo.
    /// </summary>
    private async Task AgregarFichaDelEstudianteAsync(
        PeticionChat peticion, int curso, ContextoConsulta contexto, List<ResultadoHerramienta> resultados,
        HashSet<ResultadoHerramienta> ocultos, CancellationToken ct)
    {
        var nomina = resultados.FirstOrDefault(r => r.Exito && r.Nombre == HerramientasAcademicas.ObtenerCalificaciones &&
                                                    Entero(r.Argumentos, "estudiante_id") is null);
        var preguntaPorEstudiantes = PideEstudiantes.IsMatch(peticion.Mensaje);
        if (nomina is null)
        {
            nomina = await EjecutarAutomaticaAsync(HerramientasAcademicas.ObtenerCalificaciones, curso, contexto, ct);
            resultados.Add(nomina);
        }
        if (!nomina.Exito) return;

        var textos = new List<string> { peticion.Mensaje };
        textos.AddRange((peticion.Historial ?? []).Where(m => m.Rol is "docente" or "user").Select(m => m.Texto).Reverse().Take(3));
        var estudiante = EstudianteMencionado(textos, nomina.Contenido);

        if (estudiante is { } id)
        {
            var args = $$"""{"curso_id":{{curso}},"estudiante_id":{{id}}}""";
            var ficha = await herramientas.EjecutarAsync(
                HerramientasAcademicas.ObtenerCalificaciones, JsonDocument.Parse(args).RootElement, contexto, ct);
            resultados.Add(ficha with { Origen = "orquestador" });
        }
        // La nómina completa solo se envía al modelo si la pregunta es sobre los estudiantes en general.
        if (!preguntaPorEstudiantes || estudiante is not null) ocultos.Add(nomina);
    }

    /// <summary>
    /// Deja en el resultado de una herramienta solo lo que corresponde al estudiante: sus alertas, sus entregas
    /// vencidas y sus recomendaciones. Se quitan los datos por actividad del grupo, que el modelo confundía con
    /// los del estudiante; su ficha (obtener_calificaciones) ya trae sus notas por actividad y componente.
    /// </summary>
    private static string SoloDelEstudiante(ResultadoHerramienta r, int estudianteId)
    {
        if (JsonNode.Parse(r.Contenido) is not JsonObject nodo) return r.Contenido;
        bool DeOtro(JsonNode? x) => x?["estudiante_id"] is { } id && id.GetValue<int>() != estudianteId;
        void Filtrar(string lista, bool quitarDelGrupo = false)
        {
            if (nodo[lista] is not JsonArray arreglo) return;
            foreach (var x in arreglo.ToList())
                if (DeOtro(x) || (quitarDelGrupo && x?["estudiante_id"] is null)) arreglo.Remove(x);
        }

        switch (r.Nombre)
        {
            case HerramientasAcademicas.IdentificarAlertas:
                Filtrar("alertas");
                nodo.Remove("total_alertas");
                nodo.Remove("estudiantes_afectados");
                break;
            case HerramientasAcademicas.ObtenerProgreso:
                Filtrar("estudiantes_con_entregas_vencidas");
                break;
            case HerramientasAcademicas.GenerarResumen:
                Filtrar("recomendaciones_base", quitarDelGrupo: true);
                nodo.Remove("actividades_bajo_60");
                nodo.Remove("hallazgos");
                nodo.Remove("distribucion_porcentaje");
                break;
        }
        return nodo.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>
    /// Recomendación determinista de tutoría a partir de la ficha (sus 3 notas más bajas, entregas pendientes y
    /// componente más bajo). Se agrega como recomendación base #1 para que el modelo pueda elegirla.
    /// </summary>
    private static List<ResultadoHerramienta> AgregarRecomendacionDeTutoria(List<ResultadoHerramienta> exitosos, int estudianteId)
    {
        var ficha = exitosos.FirstOrDefault(r => r.Nombre == HerramientasAcademicas.ObtenerCalificaciones &&
                                                 Entero(r.Argumentos, "estudiante_id") == estudianteId);
        var resumen = exitosos.FirstOrDefault(r => r.Nombre == HerramientasAcademicas.GenerarResumen);
        if (ficha is null || resumen is null || JsonNode.Parse(ficha.Contenido) is not JsonObject f ||
            JsonNode.Parse(resumen.Contenido) is not JsonObject res) return exitosos;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var nombre = f["nombre"]?.GetValue<string>() ?? "el estudiante";
        var bajas = (f["actividades_mas_bajas"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var pendientes = (f["sin_entrega"] as JsonArray ?? []).Select(x => x!.GetValue<string>()).ToList();
        if (bajas.Count == 0 && pendientes.Count == 0) return exitosos;

        static string Corto(string n) => n.Split(':')[0].Trim();
        string Nota(JsonObject a) => string.Format(inv, "{0:0.#}/{1:0.#}",
            a["nota"]!.GetValue<decimal>(), a["puntaje_maximo"]!.GetValue<decimal>());

        var temas = bajas.Select(a => a["nombre"]!.GetValue<string>()).ToList();
        var accion = temas.Count > 0
            ? $"Preparar una tutoría con {nombre} centrada en {Lista(temas)}"
            : $"Acordar con {nombre} un plan de entrega";
        if (pendientes.Count > 0)
            accion += temas.Count > 0 ? $", y acordar la entrega de {Lista(pendientes.Select(Corto).ToList())}." : $" de {Lista(pendientes.Select(Corto).ToList())}.";
        else accion += ".";

        var justificacion = new List<string>();
        if (bajas.Count > 0)
            justificacion.Add("Sus notas más bajas: " + string.Join(", ", bajas.Select(a => $"{Corto(a["nombre"]!.GetValue<string>())} {Nota(a)}")) + ".");
        if (pendientes.Count > 0)
            justificacion.Add($"Sin entregar: {Lista(pendientes.Select(Corto).ToList())}.");

        var recs = res["recomendaciones_base"] as JsonArray ?? [];
        res["recomendaciones_base"] = recs;
        recs.Insert(0, new JsonObject
        {
            ["id"] = IdTutoria,
            ["prioridad"] = "alta",
            ["accion"] = accion,
            ["justificacion"] = string.Join(" ", justificacion),
            ["estudiante_id"] = estudianteId
        });
        var contenido = res.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return exitosos.Select(r => ReferenceEquals(r, resumen) ? r with { Contenido = contenido } : r).ToList();
    }

    /// <summary>
    /// Si un hecho copia la evidencia de una alerta sin decir de quién es ("Sus notas bajaron…"), se le antepone el
    /// nombre del estudiante de esa alerta. Los modelos pequeños suelen recortar el nombre al copiar.
    /// </summary>
    public static string ConSujeto(string hecho, IEnumerable<ResultadoHerramienta> exitosos)
    {
        var alertas = exitosos
            .Where(r => r.Nombre == HerramientasAcademicas.IdentificarAlertas)
            .SelectMany(r => JsonNode.Parse(r.Contenido)?["alertas"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(a => (Nombre: a["nombre"]?.GetValue<string>() ?? string.Empty, Evidencia: a["evidencia"]?.GetValue<string>() ?? string.Empty))
            .Where(a => a.Nombre.Length > 0)
            .ToList();
        if (alertas.Any(a => hecho.Contains(a.Nombre, StringComparison.OrdinalIgnoreCase))) return hecho;

        var clave = Normalizar(hecho).Trim();
        if (clave.Length < 15) return hecho;
        var duenos = alertas.Where(a => Normalizar(a.Evidencia).Contains(clave)).Select(a => a.Nombre).Distinct().ToList();
        return duenos.Count == 1 ? $"{duenos[0]}: {hecho}" : hecho;
    }

    /// <summary>Número de la recomendación de tutoría (fuera del rango de las del backend).</summary>
    public const int IdTutoria = 100;

    private static string Lista(IReadOnlyList<string> nombres) =>
        nombres.Count switch
        {
            0 => string.Empty,
            1 => nombres[0],
            _ => $"{string.Join(", ", nombres.Take(nombres.Count - 1))} y {nombres[^1]}"
        };

    /// <summary>Id del estudiante de la nómina nombrado en los textos (el primero que nombre a alguien).</summary>
    public static int? EstudianteMencionado(IEnumerable<string> textos, string nominaJson)
    {
        if (JsonNode.Parse(nominaJson)?["calificaciones"] is not JsonArray lista) return null;
        var estudiantes = lista
            .Where(e => e?["estudiante_id"] is not null && e["nombre"] is not null)
            .Select(e => (Id: e!["estudiante_id"]!.GetValue<int>(), Partes: Normalizar(e["nombre"]!.GetValue<string>())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(e => e.Partes.Length > 0)
            .ToList();

        foreach (var texto in textos)
        {
            var palabras = Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            // Nombre y apellido completos, luego un nombre o apellido que solo tenga un estudiante.
            var completos = estudiantes.Where(e => e.Partes.Count(palabras.Contains) >= Math.Min(2, e.Partes.Length)).ToList();
            if (completos.Count == 1) return completos[0].Id;
            if (completos.Count > 1) return completos.OrderByDescending(e => e.Partes.Count(palabras.Contains)).First().Id;
            foreach (var palabra in palabras.Where(p => p.Length >= 3))
            {
                var coinciden = estudiantes.Where(e => e.Partes.Contains(palabra)).ToList();
                if (coinciden.Count == 1) return coinciden[0].Id;
            }
        }
        return null;
    }

    private static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return sb.ToString();
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
        IReadOnlySet<ResultadoHerramienta> ocultos, List<string> advertencias, Stopwatch reloj, CancellationToken ct)
    {
        // Los resultados "ocultos" (p. ej. la nómina usada solo para reconocer un nombre) aparecen en la traza,
        // pero no se envían al modelo.
        var exitosos = resultados.Where(r => r.Exito && !ocultos.Contains(r)).ToList();
        // Pregunta sobre un estudiante: el modelo recibe solo sus datos, para que no le atribuya los de otros.
        var estudianteFicha = exitosos
            .Where(r => r.Nombre == HerramientasAcademicas.ObtenerCalificaciones)
            .Select(r => Entero(r.Argumentos, "estudiante_id"))
            .FirstOrDefault(id => id is not null);
        if (estudianteFicha is { } idEstudiante)
        {
            exitosos = exitosos.Select(r => r with { Contenido = SoloDelEstudiante(r, idEstudiante) }).ToList();
            exitosos = AgregarRecomendacionDeTutoria(exitosos, idEstudiante);
        }
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
        var pideMaterial = PideMaterial(peticion.Mensaje);
        var final = new List<MensajeChat>
        {
            new("system", pideMaterial ? Prompts.Material : conBase ? Prompts.RespuestaFinalConBase : Prompts.RespuestaFinal),
            new("user", MensajeFinal(peticion, exitosos.Concat(resultados.Where(r => !r.Exito))))
        };

        Borrador? borrador = null;
        for (var intento = 0; intento < 2 && borrador is null; intento++)
        {
            var respuesta = await modelo.ChatAsync(
                final, null, pideMaterial ? Prompts.EsquemaMaterial : conBase ? Prompts.EsquemaRespuestaConBase : Prompts.EsquemaRespuesta, ct);
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
            .Select(h => h with { Texto = ConSujeto(h.Texto, exitosos) })
            .ToList();
        var inferencias = borrador.Inferencias.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        var inferenciasOk = inferencias.Where(i => VerificadorHechos.EsVerificable(i, cifras)).ToList();
        // Recomendaciones propias del modelo (específicas de la pregunta): solo si sus cifras están en los datos.
        var recomendaciones = borrador.Recomendaciones.Where(r => !string.IsNullOrWhiteSpace(r.Accion)).ToList();
        var propias = recomendaciones
            .Where(r => VerificadorHechos.EsVerificable($"{r.Accion} {r.Justificacion}", cifras))
            .ToList();
        var recomendacionesOk = propias.Take(conBase ? 2 : 4).ToList();
        if (pideMaterial)
        {
            // Al crear material, a lo sumo una recomendación y solo si el modelo la eligió (sin las de relleno).
            recomendacionesOk = borrador.IdsRecomendacion.Where(recomendacionesBase.ContainsKey)
                .Select(id => recomendacionesBase[id]).Concat(propias).Take(1).ToList();
        }
        else if (conBase)
        {
            // Las elegidas por número se copian tal cual del backend (no hay nada que verificar). Si el modelo no
            // eligió ninguna ni propuso las suyas, se muestran las dos de mayor prioridad.
            var elegidas = borrador.IdsRecomendacion.Distinct().Where(recomendacionesBase.ContainsKey).Take(3).ToList();
            if (elegidas.Count == 0 && recomendacionesOk.Count == 0)
                elegidas = recomendacionesBase.Keys.OrderBy(k => k == IdTutoria ? 0 : k).Take(2).ToList();
            recomendacionesOk.AddRange(elegidas.Select(id => recomendacionesBase[id])
                .Where(b => !recomendacionesOk.Any(p => p.Accion == b.Accion)));
        }

        var respuestaDirecta = string.IsNullOrWhiteSpace(borrador.Respuesta) ? null : borrador.Respuesta.Trim();
        // En modo material la respuesta solo presenta el material ("5 preguntas sobre…"): no lleva datos que verificar.
        var respuestaDescartada = respuestaDirecta is not null && !pideMaterial && !VerificadorHechos.EsVerificable(respuestaDirecta, cifras);
        if (respuestaDescartada) respuestaDirecta = null;

        var descartadas = hechos.Count - hechosOk.Count + inferencias.Count - inferenciasOk.Count +
                          recomendaciones.Count - propias.Count + (respuestaDescartada ? 1 : 0);
        if (descartadas > 0)
            advertencias.Add($"Se descartaron {descartadas} afirmación(es) con cifras que no coinciden con los datos de las herramientas.");

        var soloInsuficientes = exitosos.All(r => EsMensaje(r.Contenido, out var m) && m.StartsWith("Datos insuficientes", StringComparison.Ordinal));
        var estado = soloInsuficientes ? "sin_datos" : "ok";
        string? mensaje = soloInsuficientes && EsMensaje(exitosos[0].Contenido, out var primero) ? primero : null;

        return Crear(estado, mensaje, hechosOk, inferenciasOk, recomendacionesOk, fuentes,
            exitosos.Max(r => r.FechaCorte), advertencias, traza, contexto, reloj) with
        {
            Respuesta = respuestaDirecta,
            // El material es contenido generado, no datos: no pasa por el verificador de cifras y se etiqueta aparte.
            Material = pideMaterial && borrador.Material is { Items.Count: > 0 } m
                ? m with { Items = LimpiarItems(m.Items, CantidadPedida(peticion.Mensaje)) }
                : null
        };
    }

    // ------------------------------------------------------------------ Auxiliares

    private sealed record Borrador(
        List<HechoDto> Hechos, List<string> Inferencias, List<RecomendacionAgenteDto> Recomendaciones, List<int> IdsRecomendacion,
        string? Respuesta = null, MaterialDto? Material = null);

    public const string NotaMaterial =
        "Material generado por IA a partir del nombre de la actividad y del curso. Revísalo y ajústalo antes de entregarlo.";

    private static readonly Regex VerboCrear = new(
        @"\b(cr[eé]a(me|r|lo|la)?|genera(me|r)?|elabora(me|r)?|haz(me)?|hacer|dise[nñ]a(me|r)?|redacta(me|r)?|prepara(me|r)?|prop[oó]n(me)?|proponer|escr[ií]be(me)?|arma(me|r)?|dame)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ObjetoMaterial = new(
        @"\b(deber(es)?|tareas?|preguntas?|ejercicios?|cuestionarios?|quiz|prueba|examen|gu[ií]a|plan\s+de\s+estudio|r[uú]brica|problemas?|lecturas?|resumen\s+de\s+estudio)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Cantidad = new(
        @"\b(\d{1,2}|una?|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez)\s+(preguntas?|ejercicios?|problemas?|items?|ítems?|actividades|lecturas?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NumeracionInicial = new(@"^\s*(\d{1,2}|[a-zA-Z])[\.\)\-:]\s+", RegexOptions.CultureInvariant);

    /// <summary>"5 preguntas" → 5; "tres ejercicios" → 3; null si el docente no indica cuántos.</summary>
    public static int? CantidadPedida(string mensaje)
    {
        var m = Cantidad.Match(mensaje);
        if (!m.Success) return null;
        var palabra = m.Groups[1].Value.ToLowerInvariant();
        var n = int.TryParse(palabra, out var num) ? num : palabra switch
        {
            "un" or "una" => 1, "dos" => 2, "tres" => 3, "cuatro" => 4, "cinco" => 5,
            "seis" => 6, "siete" => 7, "ocho" => 8, "nueve" => 9, _ => 10
        };
        return n is > 0 and <= 20 ? n : null;
    }

    /// <summary>Quita la numeración que el modelo agrega ("1. ¿Qué…?") y respeta la cantidad pedida.</summary>
    public static List<string> LimpiarItems(IEnumerable<string> items, int? cantidad) =>
        items.Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => NumeracionInicial.Replace(i.Trim(), string.Empty))
            .Select(i => i.Length <= 600 ? i : i[..600])
            .Take(cantidad ?? 15)
            .ToList();

    /// <summary>¿El docente pide crear material de apoyo? ("créame un deber de 5 preguntas…").</summary>
    public static bool PideMaterial(string mensaje) => VerboCrear.IsMatch(mensaje) && ObjetoMaterial.IsMatch(mensaje);

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

    private static string MensajeFinal(PeticionChat peticion, IEnumerable<ResultadoHerramienta> resultados)
    {
        var sb = new StringBuilder();
        // Las preguntas anteriores permiten entender un seguimiento ("¿y qué temas?"). Solo se envían las preguntas
        // del docente: las respuestas previas del asistente podían arrastrar un error de una respuesta a la siguiente.
        var previas = (peticion.Historial ?? [])
            .Where(m => m.Rol is "docente" or "user" && !string.IsNullOrWhiteSpace(m.Texto))
            .TakeLast(3).ToList();
        if (previas.Count > 0)
        {
            sb.AppendLine("PREGUNTAS ANTERIORES DEL DOCENTE (solo contexto):");
            foreach (var m in previas)
                sb.AppendLine($"- {(m.Texto.Length <= 300 ? m.Texto : m.Texto[..300] + "…")}");
            sb.AppendLine();
        }
        sb.AppendLine($"Pregunta del docente: {peticion.Mensaje.Trim()}");
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
            var recomendaciones = Arreglo(raiz, "recomendaciones").Concat(Arreglo(raiz, "recomendaciones_nuevas"))
                .Select(r => r.ValueKind == JsonValueKind.String
                    ? new RecomendacionAgenteDto(r.GetString()!, string.Empty)
                    : new RecomendacionAgenteDto(Cadena(r, "accion") ?? string.Empty, Cadena(r, "justificacion") ?? string.Empty))
                .ToList();

            var ids = Arreglo(raiz, "recomendaciones_ids")
                .Select(i => i.ValueKind == JsonValueKind.Number && i.TryGetInt32(out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList();

            if (!raiz.TryGetProperty("hechos", out _) && !raiz.TryGetProperty("recomendaciones", out _) &&
                !raiz.TryGetProperty("recomendaciones_ids", out _) && !raiz.TryGetProperty("material", out _)) return null;
            MaterialDto? material = null;
            if (raiz.TryGetProperty("material", out var mat) && mat.ValueKind == JsonValueKind.Object)
            {
                var items = Arreglo(mat, "items")
                    .Select(i => i.ValueKind == JsonValueKind.String ? i.GetString()! : Cadena(i, "pregunta") ?? Cadena(i, "texto") ?? string.Empty)
                    .ToList();
                material = new MaterialDto(Cadena(mat, "titulo") ?? "Material de apoyo", items, NotaMaterial);
            }

            if (material is not null && !raiz.TryGetProperty("hechos", out _)) hechos = [];
            return new Borrador(hechos, inferencias, recomendaciones, ids, Cadena(raiz, "respuesta"), material);
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
                    a!["evidencia"]!.GetValue<string>(), r.Nombre)));
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
