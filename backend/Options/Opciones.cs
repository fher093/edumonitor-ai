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

/// <summary>
/// Sincronización automática con Moodle (RF-03): el backend importa periódicamente los cambios
/// hechos en Moodle (tareas, notas, matrículas) sin que el administrador tenga que pulsar un botón.
/// </summary>
public class SincronizacionAutomaticaOptions
{
    public const string Section = "Moodle:SincronizacionAutomatica";
    public bool Habilitada { get; set; }
    public int IntervaloMinutos { get; set; } = 15;
    /// <summary>Espera antes de la primera ejecución (Moodle puede tardar en arrancar).</summary>
    public int RetrasoInicialSegundos { get; set; } = 60;
}

public class AuthOptions
{
    public const string Section = "Auth";
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 5;
}
