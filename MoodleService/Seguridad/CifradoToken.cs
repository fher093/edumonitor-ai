using System.Security.Cryptography;
using System.Text;

namespace MoodleService.Seguridad;

/// <summary>
/// Cifra el token técnico de Moodle antes de guardarlo (RF-02 / RNF-01).
/// AES-256-GCM con clave derivada de Moodle:ClaveCifrado (variable de entorno MOODLE_SECRET_KEY).
/// Formato almacenado: base64(nonce[12] | tag[16] | cifrado).
/// </summary>
public class CifradoToken
{
    private readonly byte[] _clave;

    public CifradoToken(string claveSecreta)
    {
        if (string.IsNullOrWhiteSpace(claveSecreta) || claveSecreta.Length < 32)
            throw new InvalidOperationException(
                "Moodle:ClaveCifrado no está configurada o tiene menos de 32 caracteres (variable MOODLE_SECRET_KEY).");
        _clave = SHA256.HashData(Encoding.UTF8.GetBytes(claveSecreta));
    }

    public string Cifrar(string textoPlano)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var datos = Encoding.UTF8.GetBytes(textoPlano);
        var cifrado = new byte[datos.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_clave, tag.Length);
        aes.Encrypt(nonce, datos, cifrado, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cifrado]);
    }

    public string Descifrar(string almacenado)
    {
        var bytes = Convert.FromBase64String(almacenado);
        var nonce = bytes.AsSpan(0, 12);
        var tag = bytes.AsSpan(12, 16);
        var cifrado = bytes.AsSpan(28);
        var plano = new byte[cifrado.Length];
        using var aes = new AesGcm(_clave, tag.Length);
        aes.Decrypt(nonce, cifrado, tag, plano);
        return Encoding.UTF8.GetString(plano);
    }

    /// <summary>Muestra solo los últimos 4 caracteres del token.</summary>
    public static string Enmascarar(string? token) =>
        string.IsNullOrEmpty(token) ? "" : token.Length <= 4 ? "****" : new string('•', 8) + token[^4..];
}
