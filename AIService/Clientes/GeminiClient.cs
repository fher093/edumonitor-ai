using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIService.Agente;
using Microsoft.Extensions.Options;

namespace AIService.Clientes;

public sealed class GeminiOptions
{
    public const string Section = "Gemini";
    public string Url { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    /// <summary>Clave de Google AI Studio (variable GEMINI_API_KEY). Sin clave, la opción Gemini queda desactivada.</summary>
    public string? ApiKey { get; set; }
    public string Modelo { get; set; } = "gemini-flash-lite-latest";
    /// <summary>Modelo a probar si el principal está sobrecargado (503) o sin cuota (429); vacío para no usarlo.</summary>
    public string? ModeloAlternativo { get; set; } = "gemini-3.5-flash";
    /// <summary>Espera antes de reintentar un 503/429 (la capa gratuita tiene picos de demanda breves).</summary>
    public int EsperaReintentoMs { get; set; } = 1200;
    /// <summary>Nivel de razonamiento ("minimal", "low"…); vacío para el valor del modelo. Menos razonamiento = más rápido.</summary>
    public string? NivelPensamiento { get; set; } = "minimal";
    public double Temperatura { get; set; } = 0.2;
    public int MaxTokensRespuesta { get; set; } = 2048;
    public int TimeoutSegundos { get; set; } = 45;

    public bool Configurado => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Cliente de la API de Gemini (generateContent) para la demostración: mismo contrato que Ollama
/// (<see cref="IModeloLenguaje"/>), con function calling y salida JSON con esquema. Opcional: Ollama sigue siendo el
/// modelo por defecto. Los nombres de los estudiantes se seudonimizan antes de llegar aquí (ModeloSeudonimizado).
/// </summary>
public sealed class GeminiClient(HttpClient http, IOptions<GeminiOptions> opciones, ILogger<GeminiClient> logger) : IModeloLenguaje
{
    private readonly GeminiOptions _o = opciones.Value;

    private string? _ultimoModelo;

    /// <summary>El modelo que respondió la última vez (puede ser el alternativo si el principal estaba saturado).</summary>
    public string NombreModelo => _ultimoModelo ?? _o.Modelo;
    public bool Configurado => _o.Configurado;

    public async Task<MensajeChat> ChatAsync(
        IReadOnlyList<MensajeChat> mensajes,
        IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato,
        CancellationToken ct)
    {
        if (!_o.Configurado)
            throw new ModeloNoDisponibleException("Gemini no está configurado (falta GEMINI_API_KEY).");

        var conPensamiento = !string.IsNullOrWhiteSpace(_o.NivelPensamiento);
        var conEsquema = formato is not null;
        // Ante 503/429: un reintento con el mismo modelo y luego el modelo alternativo.
        var modelos = new Queue<string>([_o.Modelo, _o.Modelo]);
        if (!string.IsNullOrWhiteSpace(_o.ModeloAlternativo) && _o.ModeloAlternativo != _o.Modelo) modelos.Enqueue(_o.ModeloAlternativo);
        var modelo = modelos.Dequeue();
        for (var intento = 0; intento < 6; intento++)
        {
            var cuerpo = ConstruirCuerpo(mensajes, herramientas, formato, conPensamiento, conEsquema);
            var reloj = Stopwatch.StartNew();
            HttpResponseMessage resp;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"models/{modelo}:generateContent")
                {
                    Content = JsonContent.Create(cuerpo)
                };
                req.Headers.Add("x-goog-api-key", _o.ApiKey);
                resp = await http.SendAsync(req, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new ModeloNoDisponibleException("No se pudo conectar con Gemini (¿hay internet?) o tardó demasiado.", ex);
            }

            using (resp)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (resp.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Algunos modelos no aceptan el nivel de razonamiento o el esquema JSON: se reintenta sin ellos.
                    if (conPensamiento && texto.Contains("hinking", StringComparison.Ordinal)) { conPensamiento = false; continue; }
                    if (conEsquema && texto.Contains("chema", StringComparison.Ordinal)) { conEsquema = false; continue; }
                }
                if (resp.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests && modelos.Count > 0)
                {
                    var siguiente = modelos.Dequeue();
                    logger.LogWarning("Gemini {Modelo} respondió {Codigo}; se reintenta con {Siguiente}.", modelo, (int)resp.StatusCode, siguiente);
                    if (siguiente == modelo) await Task.Delay(_o.EsperaReintentoMs, ct);
                    modelo = siguiente;
                    // El modelo alternativo puede no aceptar el mismo nivel de razonamiento: se vuelve a intentar con él.
                    conPensamiento = !string.IsNullOrWhiteSpace(_o.NivelPensamiento);
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                    throw new ModeloNoDisponibleException($"Gemini respondió {(int)resp.StatusCode} ({Motivo(resp.StatusCode)}).");

                using var doc = JsonDocument.Parse(texto);
                _ultimoModelo = modelo;
                RegistrarMetricas(doc.RootElement, modelo, reloj.ElapsedMilliseconds, herramientas is { Count: > 0 } ? "herramientas" : "respuesta");
                return Interpretar(doc.RootElement);
            }
        }
        throw new ModeloNoDisponibleException("Gemini rechazó la petición.");
    }

    private static string Motivo(HttpStatusCode codigo) => codigo switch
    {
        HttpStatusCode.TooManyRequests => "se agotó la cuota gratuita, intenta en un minuto",
        HttpStatusCode.ServiceUnavailable => "el modelo está sobrecargado",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "clave no válida",
        HttpStatusCode.NotFound => "modelo no disponible para esta cuenta",
        _ => "error del servicio"
    };

    /// <summary>Convierte la conversación al formato de Gemini (contents + systemInstruction + tools).</summary>
    public JsonObject ConstruirCuerpo(
        IReadOnlyList<MensajeChat> mensajes, IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato, bool conPensamiento, bool conEsquema)
    {
        var sistema = string.Join("\n\n", mensajes.Where(m => m.Rol == "system").Select(m => m.Contenido));
        var contenidos = new List<(string Rol, JsonArray Partes)>();

        foreach (var m in mensajes.Where(m => m.Rol != "system"))
        {
            var rol = m.Rol == "assistant" ? "model" : "user";
            var partes = new JsonArray();
            if (m.Rol == "tool")
            {
                JsonNode? resultado;
                try { resultado = JsonNode.Parse(m.Contenido); }
                catch (JsonException) { resultado = JsonValue.Create(m.Contenido); }
                partes.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = m.NombreHerramienta ?? "herramienta",
                        ["response"] = new JsonObject { ["resultado"] = resultado }
                    }
                });
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(m.Contenido)) partes.Add(new JsonObject { ["text"] = m.Contenido });
                foreach (var l in m.LlamadasHerramienta ?? [])
                    partes.Add(new JsonObject
                    {
                        ["functionCall"] = new JsonObject { ["name"] = l.Nombre, ["args"] = JsonNode.Parse(l.Argumentos.GetRawText()) }
                    });
            }
            if (partes.Count == 0) continue;

            // Gemini exige alternar turnos: los mensajes seguidos del mismo rol se unen en uno.
            if (contenidos.Count > 0 && contenidos[^1].Rol == rol)
                foreach (var p in partes.ToList()) { partes.Remove(p); contenidos[^1].Partes.Add(p); }
            else
                contenidos.Add((rol, partes));
        }

        var config = new JsonObject
        {
            ["temperature"] = _o.Temperatura,
            ["maxOutputTokens"] = _o.MaxTokensRespuesta
        };
        if (formato is not null)
        {
            config["responseMimeType"] = "application/json";
            if (conEsquema) config["responseJsonSchema"] = JsonNode.Parse(formato.Value.GetRawText());
        }
        if (conPensamiento) config["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = _o.NivelPensamiento };

        var cuerpo = new JsonObject
        {
            ["contents"] = new JsonArray(contenidos.Select(c => (JsonNode?)new JsonObject { ["role"] = c.Rol, ["parts"] = c.Partes }).ToArray()),
            ["generationConfig"] = config
        };
        if (sistema.Length > 0) cuerpo["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = sistema }) };
        if (herramientas is { Count: > 0 })
        {
            cuerpo["tools"] = new JsonArray(new JsonObject
            {
                ["functionDeclarations"] = new JsonArray(herramientas.Select(h => (JsonNode?)new JsonObject
                {
                    ["name"] = h.Nombre,
                    ["description"] = h.Descripcion,
                    ["parameters"] = SinAdicionales(JsonNode.Parse(h.Parametros.GetRawText()))
                }).ToArray())
            });
        }
        return cuerpo;
    }

    /// <summary>Lee el texto y las llamadas a herramientas del primer candidato.</summary>
    public static MensajeChat Interpretar(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("candidates", out var candidatos) || candidatos.GetArrayLength() == 0 ||
            !candidatos[0].TryGetProperty("content", out var contenido) || !contenido.TryGetProperty("parts", out var partes))
            throw new ModeloNoDisponibleException("Gemini no devolvió una respuesta (posible bloqueo de seguridad).");

        var texto = new System.Text.StringBuilder();
        var llamadas = new List<LlamadaHerramienta>();
        foreach (var p in partes.EnumerateArray())
        {
            // Las partes de razonamiento ("thought") no forman parte de la respuesta.
            if (p.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.True) continue;
            if (p.TryGetProperty("text", out var tx)) texto.Append(tx.GetString());
            if (p.TryGetProperty("functionCall", out var fc) && fc.TryGetProperty("name", out var n))
                llamadas.Add(new LlamadaHerramienta(n.GetString() ?? string.Empty,
                    fc.TryGetProperty("args", out var a) ? a.Clone() : JsonDocument.Parse("{}").RootElement.Clone()));
        }
        return new MensajeChat("assistant", texto.ToString(), llamadas);
    }

    /// <summary>Gemini usa un subconjunto de OpenAPI en "parameters": no admite additionalProperties.</summary>
    private static JsonNode? SinAdicionales(JsonNode? nodo)
    {
        if (nodo is JsonObject o)
        {
            o.Remove("additionalProperties");
            foreach (var (_, v) in o.ToList()) SinAdicionales(v);
        }
        else if (nodo is JsonArray a)
        {
            foreach (var v in a) SinAdicionales(v);
        }
        return nodo;
    }

    private void RegistrarMetricas(JsonElement r, string modelo, long ms, string fase)
    {
        static long Num(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt64(out var n) ? n : 0;
        var uso = r.TryGetProperty("usageMetadata", out var u) ? u : default;
        logger.LogInformation(
            "Gemini {Fase} ({Modelo}): {Entrada} tokens de entrada, {Salida} generados, {Pensamiento} de razonamiento, total {Ms} ms",
            fase, modelo,
            uso.ValueKind == JsonValueKind.Object ? Num(uso, "promptTokenCount") : 0,
            uso.ValueKind == JsonValueKind.Object ? Num(uso, "candidatesTokenCount") : 0,
            uso.ValueKind == JsonValueKind.Object ? Num(uso, "thoughtsTokenCount") : 0,
            ms);
    }
}
