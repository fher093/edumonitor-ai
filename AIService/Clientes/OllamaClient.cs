using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIService.Agente;
using Microsoft.Extensions.Options;

namespace AIService.Clientes;

public sealed class OllamaOptions
{
    public const string Section = "Ollama";
    public string Url { get; set; } = "http://localhost:11434";
    public string Modelo { get; set; } = "qwen2.5:7b";
    public int TimeoutSegundos { get; set; } = 180;
    public double Temperatura { get; set; } = 0.1;
    public int ContextoTokens { get; set; } = 6144;
    /// <summary>Tope de tokens al elegir herramientas: una llamada ocupa pocos tokens y evita redactar texto que se descarta.</summary>
    public int MaxTokensHerramientas { get; set; } = 300;
    /// <summary>Tope de tokens de la respuesta final estructurada.</summary>
    public int MaxTokensRespuesta { get; set; } = 900;
    /// <summary>Tiempo que Ollama mantiene el modelo en memoria tras la última petición (evita recargarlo: 10-20 s).</summary>
    public string KeepAlive { get; set; } = "60m";
}

/// <summary>
/// Cliente de Ollama (/api/chat) con function calling y salida estructurada (format = JSON Schema).
/// El modelo corre en el propio equipo: los datos no salen a servicios externos (RN-11).
/// </summary>
public sealed class OllamaClient(HttpClient http, IOptions<OllamaOptions> opciones, ILogger<OllamaClient> logger) : IModeloLenguaje
{
    private readonly OllamaOptions _o = opciones.Value;

    public string NombreModelo => _o.Modelo;

    public async Task<MensajeChat> ChatAsync(
        IReadOnlyList<MensajeChat> mensajes,
        IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato,
        CancellationToken ct)
    {
        var cuerpo = new JsonObject
        {
            ["model"] = _o.Modelo,
            ["stream"] = false,
            ["keep_alive"] = _o.KeepAlive,
            ["messages"] = new JsonArray(mensajes.Select(ConvertirMensaje).ToArray<JsonNode?>()),
            ["options"] = new JsonObject
            {
                ["temperature"] = _o.Temperatura,
                ["num_ctx"] = _o.ContextoTokens,
                ["num_predict"] = herramientas is { Count: > 0 } ? _o.MaxTokensHerramientas : _o.MaxTokensRespuesta
            }
        };
        if (herramientas is { Count: > 0 })
        {
            cuerpo["tools"] = new JsonArray(herramientas.Select(h => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = h.Nombre,
                    ["description"] = h.Descripcion,
                    ["parameters"] = JsonNode.Parse(h.Parametros.GetRawText())
                }
            }).ToArray());
        }
        if (formato is not null) cuerpo["format"] = JsonNode.Parse(formato.Value.GetRawText());

        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsJsonAsync("api/chat", cuerpo, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new ModeloNoDisponibleException("No se pudo conectar con Ollama o la respuesta tardó demasiado.", ex);
        }

        if (!resp.IsSuccessStatusCode)
        {
            var detalle = await resp.Content.ReadAsStringAsync(ct);
            throw new ModeloNoDisponibleException(
                $"Ollama respondió {(int)resp.StatusCode}. ¿Está descargado el modelo {_o.Modelo}? {Recortar(detalle, 200)}");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("message", out var mensaje))
            throw new ModeloNoDisponibleException("Ollama devolvió una respuesta sin mensaje.");
        RegistrarMetricas(doc.RootElement, herramientas is { Count: > 0 } ? "herramientas" : "respuesta");

        var contenido = mensaje.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        var llamadas = new List<LlamadaHerramienta>();
        if (mensaje.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
        {
            foreach (var llamada in tc.EnumerateArray())
            {
                if (!llamada.TryGetProperty("function", out var f) || !f.TryGetProperty("name", out var n)) continue;
                var args = f.TryGetProperty("arguments", out var a) ? a.Clone() : JsonDocument.Parse("{}").RootElement.Clone();
                // Algunos modelos devuelven los argumentos como texto JSON.
                if (args.ValueKind == JsonValueKind.String)
                {
                    try { args = JsonDocument.Parse(args.GetString() ?? "{}").RootElement.Clone(); }
                    catch (JsonException) { args = JsonDocument.Parse("{}").RootElement.Clone(); }
                }
                llamadas.Add(new LlamadaHerramienta(n.GetString() ?? string.Empty, args));
            }
        }

        // Algunos modelos (p. ej. qwen2.5-coder) escriben la llamada como JSON en el texto en lugar de usar tool_calls.
        if (llamadas.Count == 0 && herramientas is { Count: > 0 })
        {
            llamadas = ExtraerLlamadasDeTexto(contenido, herramientas.Select(h => h.Nombre).ToHashSet());
            if (llamadas.Count > 0) contenido = string.Empty;
        }

        return new MensajeChat("assistant", contenido, llamadas);
    }

    /// <summary>Tokens y tiempos de cada llamada (para medir el rendimiento del agente, TT-11).</summary>
    private void RegistrarMetricas(JsonElement r, string fase)
    {
        static long Num(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt64(out var n) ? n : 0;
        logger.LogInformation(
            "Ollama {Fase}: {Entrada} tokens de entrada en {MsEntrada} ms, {Salida} tokens generados en {MsSalida} ms, carga {MsCarga} ms, total {MsTotal} ms",
            fase, Num(r, "prompt_eval_count"), Num(r, "prompt_eval_duration") / 1_000_000,
            Num(r, "eval_count"), Num(r, "eval_duration") / 1_000_000,
            Num(r, "load_duration") / 1_000_000, Num(r, "total_duration") / 1_000_000);
    }

    /// <summary>
    /// Reconoce llamadas a herramientas escritas como texto: un objeto {"name":..,"arguments":{..}}, una lista
    /// de ellos, o los mismos dentro de bloques ```json``` o &lt;tool_call&gt;. Solo acepta herramientas conocidas.
    /// </summary>
    public static List<LlamadaHerramienta> ExtraerLlamadasDeTexto(string contenido, IReadOnlySet<string> nombresValidos)
    {
        var llamadas = new List<LlamadaHerramienta>();
        if (string.IsNullOrWhiteSpace(contenido)) return llamadas;

        var texto = contenido.Replace("<tool_call>", "\n").Replace("</tool_call>", "\n").Replace("```json", "\n").Replace("```", "\n");
        foreach (var candidato in FragmentosJson(texto))
        {
            try
            {
                using var doc = JsonDocument.Parse(candidato);
                var elementos = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.EnumerateArray().ToList()
                    : [doc.RootElement];
                foreach (var e in elementos)
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    var cuerpo = e.TryGetProperty("function", out var f) && f.ValueKind == JsonValueKind.Object ? f : e;
                    if (!cuerpo.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;
                    var nombre = n.GetString()!;
                    if (!nombresValidos.Contains(nombre)) continue;
                    var args = cuerpo.TryGetProperty("arguments", out var a) ? a
                        : cuerpo.TryGetProperty("parameters", out var p) ? p
                        : JsonDocument.Parse("{}").RootElement;
                    if (args.ValueKind == JsonValueKind.String)
                    {
                        try { args = JsonDocument.Parse(args.GetString() ?? "{}").RootElement; }
                        catch (JsonException) { args = JsonDocument.Parse("{}").RootElement; }
                    }
                    llamadas.Add(new LlamadaHerramienta(nombre, args.Clone()));
                }
            }
            catch (JsonException)
            {
                // No era una llamada: el modelo respondió texto normal.
            }
        }
        return llamadas;
    }

    /// <summary>Fragmentos de nivel superior delimitados por llaves o corchetes equilibrados.</summary>
    private static IEnumerable<string> FragmentosJson(string texto)
    {
        var i = 0;
        while (i < texto.Length)
        {
            var inicio = texto.IndexOfAny(['{', '['], i);
            if (inicio < 0) yield break;
            var abre = texto[inicio];
            var cierra = abre == '{' ? '}' : ']';
            int nivel = 0, j = inicio;
            var enCadena = false;
            for (; j < texto.Length; j++)
            {
                var c = texto[j];
                if (enCadena)
                {
                    if (c == '\\') j++;
                    else if (c == '"') enCadena = false;
                    continue;
                }
                if (c == '"') enCadena = true;
                else if (c == abre) nivel++;
                else if (c == cierra && --nivel == 0) break;
            }
            if (j >= texto.Length) yield break;
            yield return texto[inicio..(j + 1)];
            i = j + 1;
        }
    }

    /// <summary>Comprueba si Ollama responde y si el modelo configurado está descargado.</summary>
    public async Task<(bool Disponible, bool ModeloDescargado, string? Error)> EstadoAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var doc = JsonDocument.Parse(await http.GetStringAsync("api/tags", cts.Token));
            var modelos = doc.RootElement.GetProperty("models").EnumerateArray()
                .Select(m => m.GetProperty("name").GetString() ?? string.Empty)
                .ToList();
            var descargado = modelos.Any(m => m.Equals(_o.Modelo, StringComparison.OrdinalIgnoreCase) ||
                                              m.Equals(_o.Modelo + ":latest", StringComparison.OrdinalIgnoreCase));
            return (true, descargado, null);
        }
        catch (Exception ex)
        {
            return (false, false, ex.Message);
        }
    }

    private static JsonNode ConvertirMensaje(MensajeChat m)
    {
        var nodo = new JsonObject { ["role"] = m.Rol, ["content"] = m.Contenido };
        if (m.NombreHerramienta is not null) nodo["tool_name"] = m.NombreHerramienta;
        if (m.LlamadasHerramienta is { Count: > 0 })
        {
            nodo["tool_calls"] = new JsonArray(m.LlamadasHerramienta.Select(l => (JsonNode?)new JsonObject
            {
                ["function"] = new JsonObject
                {
                    ["name"] = l.Nombre,
                    ["arguments"] = JsonNode.Parse(l.Argumentos.GetRawText())
                }
            }).ToArray());
        }
        return nodo;
    }

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
