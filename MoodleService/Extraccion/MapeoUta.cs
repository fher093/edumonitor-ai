using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MoodleService.Extraccion;

/// <summary>Resultado de ubicar un ítem de calificación de Moodle en el modelo de evaluación UTA.</summary>
public record UbicacionUta(string Componente, int Parcial, string Metodo);

/// <summary>
/// Traduce la estructura del libro de calificaciones de Moodle al modelo UTA
/// (parcial 1/2 × contacto con el docente 40 % / práctico-experimental 30 % / autónomo 30 %).
///
/// Prioridad:
///   1. Número de ID del ítem con la convención P{1|2}-{CD|PE|AA} (p. ej. "P1-CD-01").
///   2. Nombre de la categoría de calificación (y sus categorías padre), p. ej. "Parcial 1" › "Contacto con el docente".
///   3. Nombre del ítem.
///   4. Valores inferidos (fecha de entrega respecto a la mitad del curso y tipo de módulo), marcados como "inferido".
/// </summary>
public static partial class MapeoUta
{
    public const string ContactoDocente = "contacto_docente";
    public const string PracticoExperimental = "practico_experimental";
    public const string Autonomo = "autonomo";

    [GeneratedRegex(@"^\s*P\s*([12])\s*[-_ ]\s*(CD|PE|AA)(?![A-Za-z])", RegexOptions.IgnoreCase)]
    private static partial Regex ConvencionIdNumber();

    [GeneratedRegex(@"\b(parcial|p)\s*0?([12])\b|\b(primer|segundo)\s+parcial\b")]
    private static partial Regex PatronParcial();

    public static UbicacionUta Ubicar(
        string? idNumber,
        IReadOnlyList<string> rutaCategorias,
        string? nombreItem,
        string? modulo,
        DateTime? fechaEntrega,
        DateTime? inicioCurso,
        DateTime? finCurso)
    {
        // 1. Convención explícita en el número de ID del ítem.
        if (!string.IsNullOrWhiteSpace(idNumber))
        {
            var m = ConvencionIdNumber().Match(idNumber);
            if (m.Success)
            {
                var comp = m.Groups[2].Value.ToUpperInvariant() switch
                {
                    "CD" => ContactoDocente,
                    "PE" => PracticoExperimental,
                    _ => Autonomo
                };
                return new UbicacionUta(comp, int.Parse(m.Groups[1].Value), "idnumber");
            }
        }

        // 2 y 3. Categorías y nombre del ítem.
        var textoCategorias = Normalizar(string.Join(" / ", rutaCategorias));
        var textoItem = Normalizar(nombreItem);

        var parcial = DetectarParcial(textoCategorias) ?? DetectarParcial(textoItem);
        var componente = DetectarComponente(textoCategorias) ?? DetectarComponente(textoItem);

        if (parcial is not null && componente is not null)
            return new UbicacionUta(componente, parcial.Value, "categoria");

        // 4. Inferencia (se informa como advertencia para que el docente ordene su libro de calificaciones).
        parcial ??= InferirParcial(fechaEntrega, inicioCurso, finCurso);
        componente ??= InferirComponente(modulo);
        return new UbicacionUta(componente, parcial.Value, "inferido");
    }

    public static int? DetectarParcial(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return null;
        var m = PatronParcial().Match(texto);
        if (!m.Success) return null;
        if (m.Groups[2].Success) return int.Parse(m.Groups[2].Value);
        return m.Groups[3].Value == "primer" ? 1 : 2;
    }

    public static string? DetectarComponente(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Contains("contacto") || texto.Contains("docencia")) return ContactoDocente;
        if (texto.Contains("practic") || texto.Contains("experimental")) return PracticoExperimental;
        if (texto.Contains("autonom")) return Autonomo;
        return null;
    }

    private static int InferirParcial(DateTime? fecha, DateTime? inicio, DateTime? fin)
    {
        if (fecha is null || inicio is null || fin is null || fin <= inicio) return 1;
        var mitad = inicio.Value + (fin.Value - inicio.Value) / 2;
        return fecha.Value <= mitad ? 1 : 2;
    }

    private static string InferirComponente(string? modulo) => modulo switch
    {
        "quiz" or "lesson" or "attendance" => ContactoDocente,
        "workshop" or "lti" or "h5pactivity" => PracticoExperimental,
        _ => Autonomo
    };

    public static string TipoDesdeModulo(string? modulo, string? itemType) => modulo switch
    {
        "assign" => "Tarea",
        "quiz" => "Cuestionario",
        "workshop" => "Taller",
        "forum" => "Foro",
        "lesson" => "Lección",
        "h5pactivity" => "Actividad H5P",
        "lti" => "Herramienta externa",
        "glossary" => "Glosario",
        "data" => "Base de datos",
        null when itemType == "manual" => "Calificación manual",
        _ => "Actividad"
    };

    /// <summary>Minúsculas y sin tildes, para comparar nombres escritos de distintas formas.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
