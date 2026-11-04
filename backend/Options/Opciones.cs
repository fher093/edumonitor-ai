namespace Backend.Options;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "edumonitor-auth";
    public string Audience { get; set; } = "edumonitor-clients";
    /// <summary>Clave de firma HMAC. Siempre desde variable de entorno / user-secrets (mín. 32 caracteres).</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpMinutes { get; set; } = 60;
}

public class AuthOptions
{
    public const string Section = "Auth";
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 5;
}
