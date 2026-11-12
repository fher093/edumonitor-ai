using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.Tests;

/// <summary>
/// Levanta la API completa en memoria con base de datos InMemory.
/// La configuración se pasa por variables de entorno porque Program.cs la lee antes de Build().
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminCorreo = "admin@test.local";
    public const string AdminClave = "Admin.Test-2026";
    public const string DocenteCorreo = "docente@test.local";
    public const string DocenteClave = "Docente.Test-2026";

    public ApiFactory()
    {
        Set("ASPNETCORE_ENVIRONMENT", "Development");
        Set("Jwt__Key", TestHelpers.ClaveJwt);
        Set("Database__Provider", "InMemory");
        Set("Database__InMemoryName", Guid.NewGuid().ToString());
        Set("RateLimit__LoginPerMinute", "1000");
        Set("Seed__Admin__Nombre", "Admin de prueba");
        Set("Seed__Admin__Correo", AdminCorreo);
        Set("Seed__Admin__Password", AdminClave);
        Set("Seed__Docente__Nombre", "Docente de prueba");
        Set("Seed__Docente__Correo", DocenteCorreo);
        Set("Seed__Docente__Password", DocenteClave);
        Set("Seed__DatosDemoLocales", "true");
        Set("Moodle__SincronizacionAutomatica__Habilitada", "false");
    }

    private static void Set(string nombre, string valor) => Environment.SetEnvironmentVariable(nombre, valor);
}
