using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AIService.Agente;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIService.Tests;

/// <summary>Pruebas del endpoint POST /api/ia/chat: autenticación, rol, validación y modelo caído.</summary>
public class ChatEndpointTests : IDisposable
{
    private const string Clave = "clave-de-pruebas-del-aiservice-1234567890-abcdef";
    private readonly ModeloSimulado _modelo = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ChatEndpointTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", Clave);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IModeloLenguaje>();
            s.AddSingleton<IModeloLenguaje>(_modelo);
            s.RemoveAll<IBackendAcademico>();
            s.AddSingleton<IBackendAcademico>(Datos.BackendCompleto());
        }));
    }

    public void Dispose() => _factory.Dispose();

    private static string Token(string rol) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "edumonitor-auth",
        Audience = "edumonitor-clients",
        Expires = DateTime.UtcNow.AddMinutes(10),
        Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("email", "docente@test.local"), new Claim("role", rol)]),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Clave)), SecurityAlgorithms.HmacSha256)
    });

    private Task<HttpResponseMessage> Preguntar(string? rol, object cuerpo)
    {
        var cliente = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/ia/chat") { Content = JsonContent.Create(cuerpo) };
        if (rol is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(rol));
        return cliente.SendAsync(req);
    }

    [Fact]
    public async Task Sin_token_devuelve_401()
    {
        var resp = await Preguntar(null, new { mensaje = "Hola", cursoId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task El_administrador_tecnico_no_puede_usar_el_agente()
    {
        var resp = await Preguntar("AdministradorTecnico", new { mensaje = "Resumen", cursoId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Pregunta_vacia_devuelve_400()
    {
        var resp = await Preguntar("Docente", new { mensaje = " ", cursoId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Cuerpo_que_no_es_utf8_devuelve_400_y_no_500()
    {
        var cliente = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/ia/chat")
        {
            // "¿Qué?" codificado en Windows-1252 (bytes BF y E9 no válidos en UTF-8).
            Content = new ByteArrayContent([.. "{\"mensaje\":\""u8, 0xBF, (byte)'Q', (byte)'u', 0xE9, .. "\",\"cursoId\":1}"u8])
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("Docente"));

        var resp = await cliente.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Modelo_caido_devuelve_503_con_mensaje_claro()
    {
        _modelo.Caido = true;

        var resp = await Preguntar("Docente", new { mensaje = "Resumen del curso", cursoId = 1 });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Contains("no está disponible", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Docente_recibe_respuesta_estructurada()
    {
        // Configuración real: ruta rápida, una sola llamada al modelo (la respuesta final).
        _modelo.Responde("""
            { "hechos": [ { "texto": "Hay 2 alertas.", "fuente": "identificar_alertas" } ], "inferencias": [], "recomendaciones": [] }
            """);

        var resp = await Preguntar("Docente", new { mensaje = "¿Alertas?", cursoId = 1 });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var r = await resp.Content.ReadFromJsonAsync<RespuestaAgenteDto>();
        Assert.Equal("ok", r!.Estado);
        Assert.Equal("Hay 2 alertas.", Assert.Single(r.Hechos).Texto);
        Assert.Equal(12, r.ConsultaId.Length);
    }
}
