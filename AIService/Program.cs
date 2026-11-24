using System.Security.Claims;
using System.Text;
using AIService.Agente;
using AIService.Clientes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var key = config["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
    throw new InvalidOperationException("Jwt:Key no está configurada o tiene menos de 32 caracteres (variable Jwt__Key).");

var issuer = config["Jwt:Issuer"] ?? "edumonitor-auth";
var audience = config["Jwt:Audience"] ?? "edumonitor-clients";

// Este microservicio NO emite tokens: solo valida los que emite el backend (misma clave, issuer y audience).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
// Un cuerpo JSON mal formado (o con texto que no es UTF-8) es un 400, no una excepción 500.
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);

// ---------- Agente académico (HU-12 a HU-15, TT-09) ----------
builder.Services.Configure<OllamaOptions>(config.GetSection(OllamaOptions.Section));
builder.Services.Configure<AgenteOptions>(config.GetSection(AgenteOptions.Section));
builder.Services.AddHttpClient<OllamaClient>((sp, c) =>
{
    var o = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    c.BaseAddress = new Uri(o.Url.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(Math.Max(10, o.TimeoutSegundos));
});
builder.Services.AddTransient<IModeloLenguaje>(sp => sp.GetRequiredService<OllamaClient>());
// Gemini (opcional, solo para la demostración): si no hay GEMINI_API_KEY, la opción queda desactivada.
builder.Services.Configure<GeminiOptions>(config.GetSection(GeminiOptions.Section));
builder.Services.AddHttpClient<GeminiClient>((sp, c) =>
{
    var o = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    c.BaseAddress = new Uri(o.Url.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(Math.Max(10, o.TimeoutSegundos));
});
builder.Services.AddHttpClient<IBackendAcademico, BackendClient>(c =>
{
    c.BaseAddress = new Uri((config["Servicios:BackendUrl"] ?? "http://localhost:5000").TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<HerramientasAcademicas>();
builder.Services.AddScoped<AgenteAcademico>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", servicio = "aiservice" }));

/// Estado del servicio, de Ollama y del modelo configurado.
app.MapGet("/api/ia/estado", async (ClaimsPrincipal user, OllamaClient ollama, IOptions<OllamaOptions> opciones,
    IOptions<GeminiOptions> gemini, CancellationToken ct) =>
{
    var (disponible, descargado, error) = await ollama.EstadoAsync(ct);
    return Results.Ok(new
    {
        servicio = "aiservice",
        estado = disponible && descargado ? "operativo" : "degradado",
        modelo = opciones.Value.Modelo,
        ollamaDisponible = disponible,
        modeloDescargado = descargado,
        error,
        gemini = new { configurado = gemini.Value.Configurado, modelo = gemini.Value.Modelo },
        herramientas = HerramientasAcademicas.Definiciones.Select(h => h.Nombre),
        usuario = user.FindFirst("email")?.Value,
        rol = user.FindFirst("role")?.Value
    });
}).RequireAuthorization();

/// Chat con el agente académico (solo docentes: el agente consulta datos de sus cursos).
app.MapPost("/api/ia/chat", async (PeticionChat? peticion, HttpContext http, AgenteAcademico agenteLocal, GeminiClient gemini,
    IBackendAcademico backend, ILogger<Program> logger, CancellationToken ct) =>
{
    var mensaje = peticion?.Mensaje?.Trim() ?? string.Empty;
    if (mensaje.Length is < 2 or > 1000)
        return Results.BadRequest(new { message = "La pregunta debe tener entre 2 y 1000 caracteres." });
    if (peticion!.Historial is { Count: > 20 })
        return Results.BadRequest(new { message = "El historial admite como máximo 20 mensajes." });

    var autorizacion = http.Request.Headers.Authorization.ToString();
    var token = autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? autorizacion[7..].Trim() : string.Empty;
    var contexto = new ContextoConsulta(token, Guid.NewGuid().ToString("N")[..12]);

    // Modelo elegido en el chat: Ollama (local, por defecto) o Gemini (demostración). Con Gemini, los nombres de los
    // estudiantes se seudonimizan antes de salir del equipo (RN-11) y, si Gemini falla, se continúa con Ollama.
    var agente = agenteLocal;
    ModeloConRespaldo? conRespaldo = null;
    var advertenciasExtra = new List<string>();
    if (string.Equals(peticion.Proveedor, "gemini", StringComparison.OrdinalIgnoreCase))
    {
        if (!gemini.Configurado)
        {
            advertenciasExtra.Add("Gemini no está configurado (falta GEMINI_API_KEY); se usó el modelo local.");
        }
        else
        {
            var curso = AgenteAcademico.CursoMencionado(mensaje) ?? peticion.CursoId;
            var seudonimos = new Seudonimizador(curso is null ? [] : await NombresDelCursoAsync(backend, curso.Value, contexto, ct));
            conRespaldo = new ModeloConRespaldo(new ModeloSeudonimizado(gemini, seudonimos),
                http.RequestServices.GetRequiredService<IModeloLenguaje>(), logger);
            agente = ActivatorUtilities.CreateInstance<AgenteAcademico>(http.RequestServices, (IModeloLenguaje)conRespaldo);
        }
    }

    try
    {
        var respuesta = await agente.ResponderAsync(peticion with { Mensaje = mensaje }, contexto, ct);
        if (conRespaldo is { UsoRespaldo: true })
            advertenciasExtra.Add($"Gemini no respondió ({conRespaldo.Motivo}); se usó el modelo local.");
        if (advertenciasExtra.Count > 0)
            respuesta = respuesta with { Advertencias = [.. advertenciasExtra, .. respuesta.Advertencias] };
        return Results.Ok(respuesta);
    }
    catch (ModeloNoDisponibleException ex)
    {
        logger.LogWarning("Modelo de lenguaje no disponible: {Error}", ex.Message);
        var causa = conRespaldo is { UsoRespaldo: true }
            ? $"Gemini no respondió ({conRespaldo.Motivo}) y el modelo local tampoco: ¿está abierto Ollama?"
            : "el modelo de lenguaje local no responde: ¿está abierto Ollama?";
        return Results.Json(new
        {
            message = $"El asistente de IA no está disponible en este momento ({causa}). " +
                      "Los datos del curso siguen disponibles en Resumen, Alertas e Informe.",
            consultaId = contexto.ConsultaId
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization(p => p.RequireRole("Docente"));

app.Run();

// Nombres de los estudiantes del curso, solo para seudonimizarlos antes de llamar a Gemini.
static async Task<List<string>> NombresDelCursoAsync(IBackendAcademico backend, int curso, ContextoConsulta contexto, CancellationToken ct)
{
    var r = await backend.GetAsync($"api/indicadores/curso/{curso}", HerramientasAcademicas.ObtenerCalificaciones, contexto, ct);
    if (!r.Exito || !r.Cuerpo!.Value.TryGetProperty("estudiantes", out var estudiantes)) return [];
    return estudiantes.EnumerateArray()
        .Select(e => e.TryGetProperty("nombre", out var n) ? n.GetString() ?? string.Empty : string.Empty)
        .Where(n => n.Length > 0)
        .ToList();
}

// Necesario para las pruebas de integración (WebApplicationFactory<Program>)
public partial class Program { }
