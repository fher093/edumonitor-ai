using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIService.Agente;

/// <summary>
/// Defensa contra cifras inventadas (RN-09): una afirmación solo se acepta si todas sus cifras aparecen
/// en los datos devueltos por las herramientas (con tolerancia de redondeo). También se aceptan los
/// conteos de listas (por ejemplo, "3 alertas" cuando la herramienta devolvió 3 alertas).
/// </summary>
public static partial class VerificadorHechos
{
    private const decimal Tolerancia = 0.051m;

    /// <summary>Todas las cifras presentes en los resultados de las herramientas.</summary>
    public static HashSet<decimal> CifrasDe(IEnumerable<string> contenidosJson)
    {
        var cifras = new HashSet<decimal>();
        foreach (var json in contenidosJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                Recorrer(doc.RootElement, cifras);
            }
            catch (JsonException)
            {
                foreach (var n in CifrasDeTexto(json)) cifras.Add(n);
            }
        }
        return cifras;
    }

    /// <summary>¿Todas las cifras del texto están respaldadas por los datos?</summary>
    public static bool EsVerificable(string texto, IReadOnlySet<decimal> cifrasDatos)
    {
        foreach (var (valor, esEntero) in CifrasConFormato(texto))
        {
            var respaldada = cifrasDatos.Any(d =>
                Math.Abs(d - valor) <= Tolerancia ||
                (esEntero && Math.Round(d, MidpointRounding.AwayFromZero) == valor));
            if (!respaldada) return false;
        }
        return true;
    }

    private static void Recorrer(JsonElement e, HashSet<decimal> cifras)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                if (e.TryGetDecimal(out var n)) cifras.Add(n);
                break;
            case JsonValueKind.String:
                foreach (var x in CifrasDeTexto(e.GetString() ?? string.Empty)) cifras.Add(x);
                break;
            case JsonValueKind.Array:
                cifras.Add(e.GetArrayLength());
                foreach (var item in e.EnumerateArray()) Recorrer(item, cifras);
                break;
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) Recorrer(p.Value, cifras);
                break;
        }
    }

    private static IEnumerable<decimal> CifrasDeTexto(string texto) => CifrasConFormato(texto).Select(c => c.Valor);

    private static IEnumerable<(decimal Valor, bool EsEntero)> CifrasConFormato(string texto)
    {
        // Se ignoran códigos de reglas (RN-05), fechas y horas: no son cifras académicas.
        var limpio = CodigoRegex().Replace(texto, " ");
        limpio = FechaRegex().Replace(limpio, " ");
        limpio = HoraRegex().Replace(limpio, " ");
        foreach (Match m in NumeroRegex().Matches(limpio))
        {
            var bruto = m.Value.Replace(',', '.');
            if (decimal.TryParse(bruto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
                yield return (valor, !bruto.Contains('.'));
        }
    }

    [GeneratedRegex(@"\b[A-Z]{2,4}-\d+\b")]
    private static partial Regex CodigoRegex();

    [GeneratedRegex(@"\b\d{1,4}[-/]\d{1,2}[-/]\d{1,4}\b")]
    private static partial Regex FechaRegex();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}\b")]
    private static partial Regex HoraRegex();

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumeroRegex();
}
