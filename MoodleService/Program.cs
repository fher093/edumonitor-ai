using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MoodleService;
using MoodleService.Conexion;
using MoodleService.Extraccion;
using MoodleService.Moodle;
using MoodleService.Seguridad;

// Microservicio de integración con Moodle (RF-02 conexión / RF-03 sincronización).
// - Guarda la URL y el token técnico de Moodle (cifrado) y verifica la conectividad.
// - Extrae cursos, matriculados, ítems de calificación, notas y estados normalizados al modelo de EduMonitor.
// - Solo valida los JWT que emite el backend; no emite tokens. Solo el Administrador Técnico puede usarlo.

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var key = config["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
    throw new InvalidOperationException("Jwt:Key no está configurada o tiene menos de 32 caracteres (variable Jwt__Key).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"] ?? "edumonitor-auth",
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"] ?? "edumonitor-clients",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("admin", p => p.RequireRole("AdministradorTecnico"));

builder.Services.AddDbContext<MoodleDbContext>(o =>
{
    if (string.Equals(config["Database:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        o.UseInMemoryDatabase(config["Database:InMemoryName"] ?? "edumonitor-moodle");
    else
        o.UseSqlServer(config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default."));
});

builder.Services.AddSingleton(new CifradoToken(config["Moodle:ClaveCifrado"] ?? ""));
builder.Services.AddScoped<ConexionStore>();
builder.Services.AddSingleton<ExtractorMoodle>();
builder.Services.AddHttpClient("moodle", c =>
{
    c.Timeout = TimeSpan.FromSeconds(config.GetValue("Moodle:TimeoutSegundos", 120));
    c.DefaultRequestHeaders.UserAgent.ParseAdd("EduMonitor-MoodleService/1.0");
});
builder.Services.AddProblemDetails();

var app = builder.Build();

// La tabla de configuración se crea al arrancar (reintenta mientras SQL Server inicia).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
    for (var intento = 1; ; intento++)
    {
        try { await db.AsegurarEsquemaAsync(CancellationToken.None); break; }
        catch (Exception ex) when (intento < 30)
        {
            app.Logger.LogWarning("Base de datos no disponible (intento {Intento}/30): {Mensaje}", intento, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", servicio = "moodleservice" }));

var api = app.MapGroup("/api/moodle").RequireAuthorization("admin");

// ---------- RF-02: configuración de la conexión ----------

api.MapGet("/conexion", async (ConexionStore store, CancellationToken ct) =>
{
    var c = await store.ObtenerAsync(ct);
    return Results.Ok(c is null
        ? new ConexionDto(null, false, "", "sin_configurar", null, null)
        : new ConexionDto(c.Url, true, CifradoToken.Enmascarar(c.Token), c.Origen, c.FechaActualizacion, c.ActualizadoPor));
});

api.MapPut("/conexion", async (GuardarConexionRequest req, ConexionStore store, ClaimsPrincipal user, CancellationToken ct) =>
{
    if (!UrlValida(req.Url, out var error)) return Results.BadRequest(new ErrorDto(error));
    if (req.Token is { Length: > 0 } t && t.Trim().Length is < 16 or > 128)
        return Results.BadRequest(new ErrorDto("El token debe tener entre 16 y 128 caracteres."));

    try
    {
        await store.GuardarAsync(req.Url, req.Token, user.FindFirst("email")?.Value, ct);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorDto(ex.Message));
    }

    var c = (await store.ObtenerAsync(ct))!;
    return Results.Ok(new ConexionDto(c.Url, true, CifradoToken.Enmascarar(c.Token), c.Origen, c.FechaActualizacion, c.ActualizadoPor));
});

// Prueba con los datos enviados (antes de guardar) o, si no se envían, con los vigentes.
api.MapPost("/conexion/probar", async (GuardarConexionRequest? req, ConexionStore store, ExtractorMoodle extractor,
    IHttpClientFactory http, CancellationToken ct) =>
{
    var vigente = await store.ObtenerAsync(ct);
    var url = string.IsNullOrWhiteSpace(req?.Url) ? vigente?.Url : req.Url.Trim().TrimEnd('/');
    var token = string.IsNullOrWhiteSpace(req?.Token) ? vigente?.Token : req.Token.Trim();

    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
        return Results.BadRequest(new ErrorDto("Configura la URL y el token de Moodle antes de probar la conexión."));
    if (!UrlValida(url, out var error)) return Results.BadRequest(new ErrorDto(error));

    var cliente = new MoodleWsClient(http.CreateClient("moodle"), url, token);
    return Results.Ok(await extractor.ProbarAsync(cliente, ct));
});

// ---------- RF-03: extracción para la sincronización (la invoca el backend) ----------

api.MapGet("/extraccion", async (string? cursos, ConexionStore store, ExtractorMoodle extractor,
    IHttpClientFactory http, ILogger<Program> logger, CancellationToken ct) =>
{
    var c = await store.ObtenerAsync(ct);
    if (c is null)
        return Results.Conflict(new ErrorDto("La conexión con Moodle no está configurada."));

    var soloCursos = (cursos ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => long.TryParse(s, out var n) ? n : 0).Where(n => n > 0).ToList();

    try
    {
        var cliente = new MoodleWsClient(http.CreateClient("moodle"), c.Url, c.Token);
        return Results.Ok(await extractor.ExtraerAsync(cliente, c.Url, soloCursos, ct));
    }
    catch (MoodleWsException ex)
    {
        logger.LogWarning("Moodle rechazó la extracción: {Codigo} {Mensaje}", ex.Codigo, ex.MensajeMoodle);
        return Results.Json(new ErrorDto(ex.Message), statusCode: StatusCodes.Status502BadGateway);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        logger.LogWarning("Moodle no disponible: {Mensaje}", ex.Message);
        return Results.Json(new ErrorDto($"No se pudo conectar con Moodle en {c.Url}: {ex.Message}"),
            statusCode: StatusCodes.Status502BadGateway);
    }
});

// ---------- Foto de perfil de un estudiante (la invoca el backend, que controla quién la ve) ----------

api.MapGet("/foto", async (string? ruta, ConexionStore store, IHttpClientFactory http,
    ILogger<Program> logger, CancellationToken ct) =>
{
    if (!FotosMoodle.EsRutaValida(ruta))
        return Results.BadRequest(new ErrorDto("Ruta de foto no válida."));
    var c = await store.ObtenerAsync(ct);
    if (c is null)
        return Results.Conflict(new ErrorDto("La conexión con Moodle no está configurada."));

    try
    {
        var foto = await FotosMoodle.DescargarAsync(http.CreateClient("moodle"), c.Url, c.Token, ruta!, ct);
        return foto is null ? Results.NotFound() : Results.File(foto.Value.Datos, foto.Value.Tipo);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        logger.LogWarning("No se pudo descargar la foto de Moodle: {Mensaje}", ex.Message);
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
});

app.Run();

static bool UrlValida(string? url, out string error)
{
    error = "";
    if (string.IsNullOrWhiteSpace(url) ||
        !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        error = "Ingresa una URL válida de Moodle (http:// o https://).";
        return false;
    }
    return true;
}

public partial class Program { }
