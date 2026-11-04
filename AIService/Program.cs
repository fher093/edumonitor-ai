using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
    throw new InvalidOperationException("Jwt:Key no está configurada o tiene menos de 32 caracteres (variable Jwt__Key).");

var issuer = builder.Configuration["Jwt:Issuer"] ?? "edumonitor-auth";
var audience = builder.Configuration["Jwt:Audience"] ?? "edumonitor-clients";

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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", servicio = "aiservice" }));

app.MapGet("/api/ia/estado", (ClaimsPrincipal user) => Results.Ok(new
{
    servicio = "aiservice",
    estado = "operativo",
    usuario = user.FindFirst("email")?.Value,
    rol = user.FindFirst("role")?.Value
})).RequireAuthorization();

app.Run();
