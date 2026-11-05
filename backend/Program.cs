using System.Text;
using System.Threading.RateLimiting;
using Backend.Data;
using Backend.Options;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- JWT (secretos solo por variables de entorno / user-secrets) ----------
var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key no está configurada o tiene menos de 32 caracteres. Define la variable de entorno Jwt__Key (o JWT_KEY en .env).");

builder.Services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
builder.Services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));

// ---------- Base de datos ----------
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (string.Equals(config["Database:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        o.UseInMemoryDatabase(config["Database:InMemoryName"] ?? "edumonitor");
    else
        o.UseSqlServer(config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default."));
});

// ---------- Servicios ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<AnaliticaService>();
builder.Services.AddScoped<AnaliticaProvider>();

// ---------- Autenticación / autorización ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // conservamos los nombres cortos: sub, email, name, role
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ---------- Límite de intentos de login por IP ----------
var loginPorMinuto = config.GetValue("RateLimit:LoginPerMinute", 20);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPorMinuto,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// ---------- CORS (solo necesario si el frontend se sirve desde otro origen) ----------
var origenes = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
{
    if (origenes.Length > 0) p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Logger);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                // /openapi/v1.json
    app.MapScalarApiReference();     // /scalar/v1  (documentación interactiva)
}
else
{
    app.UseExceptionHandler();
}

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", servicio = "backend" }));
app.MapControllers();

app.Run();

// Necesario para las pruebas de integración (WebApplicationFactory<Program>)
public partial class Program { }
