using System.Text.Json;

namespace MoodleService.Moodle;

/// <summary>Error devuelto por Moodle (token inválido, función no habilitada, permiso faltante...).</summary>
public class MoodleWsException(string funcion, string codigo, string mensaje)
    : Exception($"Moodle rechazó {funcion}: {mensaje} ({codigo})")
{
    public string Funcion { get; } = funcion;
    public string Codigo { get; } = codigo;
    public string MensajeMoodle { get; } = mensaje;
}

/// <summary>
/// Cliente mínimo del protocolo REST de servicios web de Moodle
/// (POST /webservice/rest/server.php con wstoken, wsfunction y moodlewsrestformat=json).
/// </summary>
public class MoodleWsClient(HttpClient http, string urlBase, string token)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<JsonElement> LlamarAsync(
        string funcion, IEnumerable<KeyValuePair<string, string>>? parametros, CancellationToken ct)
    {
        var campos = new List<KeyValuePair<string, string>>
        {
            new("wstoken", token),
            new("wsfunction", funcion),
            new("moodlewsrestformat", "json"),
        };
        if (parametros is not null) campos.AddRange(parametros);

        using var respuesta = await http.PostAsync(
            $"{urlBase.TrimEnd('/')}/webservice/rest/server.php", new FormUrlEncodedContent(campos), ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Moodle respondió HTTP {(int)respuesta.StatusCode} al invocar {funcion}.");

        JsonElement raiz;
        try
        {
            raiz = JsonDocument.Parse(cuerpo).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new HttpRequestException(
                $"La respuesta de {funcion} no es JSON. Verifica que la URL apunte a la raíz de Moodle y que REST esté habilitado.");
        }

        if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("exception", out _))
        {
            var codigo = raiz.TryGetProperty("errorcode", out var c) ? c.GetString() ?? "" : "";
            var mensaje = raiz.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            throw new MoodleWsException(funcion, codigo, mensaje);
        }

        return raiz;
    }

    public async Task<T> LlamarAsync<T>(
        string funcion, IEnumerable<KeyValuePair<string, string>>? parametros, CancellationToken ct)
    {
        var raiz = await LlamarAsync(funcion, parametros, ct);
        return raiz.Deserialize<T>(Json)
               ?? throw new HttpRequestException($"Respuesta vacía de {funcion}.");
    }

    /// <summary>Construye parámetros de arreglo: nombre[0]=a, nombre[1]=b...</summary>
    public static IEnumerable<KeyValuePair<string, string>> Arreglo(string nombre, IEnumerable<long> valores) =>
        valores.Select((v, i) => new KeyValuePair<string, string>($"{nombre}[{i}]", v.ToString()));

    public static KeyValuePair<string, string> P(string nombre, object valor) => new(nombre, valor.ToString() ?? "");
}
