using System.Text.RegularExpressions;

namespace MoodleService.Extraccion;

/// <summary>
/// Fotos de perfil de Moodle. Moodle devuelve en <c>profileimageurl</c> una URL con su dirección
/// pública (que el contenedor puede no alcanzar) y protegida; aquí se guarda solo la ruta relativa
/// y la imagen se descarga por la conexión configurada, autenticada con el token técnico.
/// </summary>
public static partial class FotosMoodle
{
    public const long TamanoMaximo = 2 * 1024 * 1024;

    // Solo iconos de usuario: <contextid>/user/icon/<tema>/<f1|f2|f3>[?rev=N]. Impide pedir cualquier
    // otro archivo de Moodle (archivos de cursos, entregas...) a través de este servicio.
    [GeneratedRegex(@"^\d{1,10}/user/icon/[a-z0-9_]{1,40}/f[123](\?rev=\d{1,12})?$")]
    private static partial Regex PatronRuta();

    /// <summary>
    /// Extrae la ruta de la foto desde <c>profileimageurl</c>. Devuelve null si es la imagen
    /// genérica (<c>/theme/image.php/.../u/f1</c>), es decir, si el usuario no subió foto.
    /// </summary>
    public static string? RutaDesdeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        const string marcador = "/pluginfile.php/";
        var i = url.IndexOf(marcador, StringComparison.Ordinal);
        if (i < 0) return null;
        var ruta = url[(i + marcador.Length)..];
        return EsRutaValida(ruta) ? ruta : null;
    }

    public static bool EsRutaValida(string? ruta) => ruta is not null && PatronRuta().IsMatch(ruta);

    /// <summary>
    /// Descarga la foto mediante webservice/pluginfile.php (acepta el token del servicio web).
    /// Devuelve null si Moodle no entrega una imagen.
    /// </summary>
    public static async Task<(byte[] Datos, string Tipo)?> DescargarAsync(
        HttpClient http, string urlBase, string token, string ruta, CancellationToken ct)
    {
        var separador = ruta.Contains('?') ? '&' : '?';
        var url = $"{urlBase.TrimEnd('/')}/webservice/pluginfile.php/{ruta}{separador}token={Uri.EscapeDataString(token)}";

        using var respuesta = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        var tipo = respuesta.Content.Headers.ContentType?.MediaType ?? "";
        if (!respuesta.IsSuccessStatusCode || !tipo.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return null;
        if (respuesta.Content.Headers.ContentLength is > TamanoMaximo) return null;

        var datos = await respuesta.Content.ReadAsByteArrayAsync(ct);
        return datos.Length is 0 or > (int)TamanoMaximo ? null : (datos, tipo);
    }
}
