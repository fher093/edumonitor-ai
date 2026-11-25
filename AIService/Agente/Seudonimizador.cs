using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIService.Agente;

/// <summary>
/// Reemplaza los nombres de los estudiantes por seudónimos ("Estudiante A", "Estudiante B"…) antes de enviar texto
/// a un servicio externo (Gemini) y los repone al recibir la respuesta (RN-11: ningún dato personal sale del equipo).
/// Reconoce el nombre completo aunque se escriba sin tildes o en otra capitalización, y también el nombre o el
/// apellido sueltos cuando solo los tiene un estudiante.
/// </summary>
public sealed class Seudonimizador
{
    private readonly List<(Regex Patron, string Alias)> _ocultar = [];
    private readonly Dictionary<string, string> _nombres = new(StringComparer.Ordinal); // alias → nombre real
    private static readonly Regex AliasRegex = new(@"\bEstudiante ([A-Z]{1,2})\b", RegexOptions.CultureInvariant);

    public Seudonimizador(IEnumerable<string> nombres)
    {
        var lista = nombres.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
        var completos = new List<(Regex, string)>();
        var sueltos = new List<(Regex, string)>();

        for (var i = 0; i < lista.Count; i++)
        {
            var alias = $"Estudiante {Letras(i)}";
            _nombres[alias] = lista[i];
            completos.Add((Patron(lista[i]), alias));
        }

        // Nombre o apellido sueltos ("Ana", "Torres"), solo si identifican a un único estudiante.
        var partes = lista
            .SelectMany((n, i) => n.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => (Parte: Base(p), Indice: i))
                .Where(p => p.Parte.Length >= 3 && !Comunes.Contains(p.Parte)))
            .GroupBy(x => x.Parte)
            .Where(g => g.Select(x => x.Indice).Distinct().Count() == 1);
        foreach (var g in partes)
            sueltos.Add((Patron(g.Key), $"Estudiante {Letras(g.First().Indice)}"));

        // Primero los nombres completos (más largos primero) y luego las partes sueltas.
        _ocultar.AddRange(completos.OrderByDescending(c => c.Item1.ToString().Length));
        _ocultar.AddRange(sueltos);
    }

    /// <summary>Partículas de apellidos compuestos: no se reemplazan sueltas.</summary>
    private static readonly HashSet<string> Comunes = ["del", "las", "los", "san", "santa", "van", "von", "dos", "der"];

    public int Cantidad => _nombres.Count;

    public string Ocultar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        foreach (var (patron, alias) in _ocultar) texto = patron.Replace(texto, alias);
        return texto;
    }

    public string Mostrar(string texto) =>
        string.IsNullOrEmpty(texto)
            ? texto
            : AliasRegex.Replace(texto, m => _nombres.TryGetValue(m.Value, out var nombre) ? nombre : m.Value);

    /// <summary>Aplica una transformación a todas las cadenas de un JSON (argumentos de herramientas).</summary>
    public static JsonElement EnCadenas(JsonElement json, Func<string, string> f)
    {
        var nodo = System.Text.Json.Nodes.JsonNode.Parse(json.GetRawText());
        Recorrer(nodo, f);
        return JsonDocument.Parse(nodo?.ToJsonString() ?? "{}").RootElement.Clone();

        static void Recorrer(System.Text.Json.Nodes.JsonNode? n, Func<string, string> f)
        {
            switch (n)
            {
                case System.Text.Json.Nodes.JsonObject o:
                    foreach (var (k, v) in o.ToList())
                    {
                        if (v is System.Text.Json.Nodes.JsonValue val && val.TryGetValue<string>(out var s)) o[k] = f(s);
                        else Recorrer(v, f);
                    }
                    break;
                case System.Text.Json.Nodes.JsonArray a:
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (a[i] is System.Text.Json.Nodes.JsonValue val && val.TryGetValue<string>(out var s)) a[i] = f(s);
                        else Recorrer(a[i], f);
                    }
                    break;
            }
        }
    }

    /// <summary>0 → "A", 25 → "Z", 26 → "AA"…</summary>
    private static string Letras(int i) =>
        i < 26 ? ((char)('A' + i)).ToString() : $"{(char)('A' + i / 26 - 1)}{(char)('A' + i % 26)}";

    /// <summary>Patrón que acepta el texto con o sin tildes, en cualquier capitalización y con espacios flexibles.</summary>
    private static Regex Patron(string texto)
    {
        var sb = new StringBuilder(@"(?<![\p{L}\p{N}])");
        foreach (var c in Base(texto))
        {
            sb.Append(c switch
            {
                'a' => "[aáàäâ]",
                'e' => "[eéèëê]",
                'i' => "[iíìïî]",
                'o' => "[oóòöô]",
                'u' => "[uúùüû]",
                'n' => "[nñ]",
                ' ' => @"\s+",
                _ => Regex.Escape(c.ToString())
            });
        }
        sb.Append(@"(?![\p{L}\p{N}])");
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Base(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}

/// <summary>Envuelve un modelo externo: oculta los nombres al enviar y los repone al recibir.</summary>
public sealed class ModeloSeudonimizado(IModeloLenguaje interno, Seudonimizador seudonimos) : IModeloLenguaje
{
    public string NombreModelo => interno.NombreModelo;

    public async Task<MensajeChat> ChatAsync(
        IReadOnlyList<MensajeChat> mensajes, IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato, CancellationToken ct)
    {
        var ocultos = mensajes.Select(m => m with
        {
            Contenido = seudonimos.Ocultar(m.Contenido),
            LlamadasHerramienta = m.LlamadasHerramienta?
                .Select(l => l with { Argumentos = Seudonimizador.EnCadenas(l.Argumentos, seudonimos.Ocultar) }).ToList()
        }).ToList();

        var r = await interno.ChatAsync(ocultos, herramientas, formato, ct);
        return r with
        {
            Contenido = seudonimos.Mostrar(r.Contenido),
            LlamadasHerramienta = r.LlamadasHerramienta?
                .Select(l => l with { Argumentos = Seudonimizador.EnCadenas(l.Argumentos, seudonimos.Mostrar) }).ToList()
        };
    }
}

/// <summary>
/// Usa el modelo principal (Gemini) y, si no responde (sin internet, sin cuota, sobrecargado), continúa la
/// misma consulta con el modelo de respaldo (Ollama local), para que la demostración no se interrumpa.
/// </summary>
public sealed class ModeloConRespaldo(IModeloLenguaje principal, IModeloLenguaje respaldo, ILogger logger) : IModeloLenguaje
{
    public bool UsoRespaldo { get; private set; }
    public string? Motivo { get; private set; }

    public string NombreModelo => UsoRespaldo ? respaldo.NombreModelo : principal.NombreModelo;

    public async Task<MensajeChat> ChatAsync(
        IReadOnlyList<MensajeChat> mensajes, IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato, CancellationToken ct)
    {
        if (!UsoRespaldo)
        {
            try
            {
                return await principal.ChatAsync(mensajes, herramientas, formato, ct);
            }
            catch (ModeloNoDisponibleException ex)
            {
                logger.LogWarning("{Modelo} no respondió ({Motivo}); se continúa con {Respaldo}.",
                    principal.NombreModelo, ex.Message, respaldo.NombreModelo);
                UsoRespaldo = true;
                Motivo = ex.Message;
            }
        }
        return await respaldo.ChatAsync(mensajes, herramientas, formato, ct);
    }
}
