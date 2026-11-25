using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AIService.Agente;
using AIService.Clientes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIService.Tests;

/// <summary>Manejador HTTP simulado: registra las peticiones y responde con un guion.</summary>
public sealed class ManejadorSimulado(params (HttpStatusCode Codigo, string Cuerpo)[] respuestas) : HttpMessageHandler
{
    private int _i;
    public List<(HttpRequestMessage Peticion, string Cuerpo)> Recibidas { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Recibidas.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct)));
        var (codigo, cuerpo) = respuestas[Math.Min(_i++, respuestas.Length - 1)];
        return new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
    }
}

/// <summary>Gemini como modelo opcional de la demostración: seudonimización (RN-11), respaldo y formato de la API.</summary>
public class GeminiTests
{
    private static readonly string[] Nombres = ["Ana Torres", "María Gómez", "Ana Paredes", "Luis Del Pozo"];

    // ------------------------------------------------------------------ Seudonimizador

    [Theory]
    [InlineData("¿Cómo va Ana Torres?", "¿Cómo va Estudiante A?")]
    [InlineData("deber para maria gomez", "deber para Estudiante B")]          // sin tildes ni mayúsculas
    [InlineData("¿Y Gómez entregó?", "¿Y Estudiante B entregó?")]             // apellido único
    [InlineData("¿Qué pasa con Ana?", "¿Qué pasa con Ana?")]                  // nombre ambiguo: dos Ana
    [InlineData("el promedio del curso", "el promedio del curso")]           // "del" de "Del Pozo" no se reemplaza
    [InlineData("Analizar a Torres-Paredes", "Analizar a Estudiante A-Estudiante C")]
    public void Oculta_los_nombres_de_los_estudiantes(string texto, string esperado)
    {
        Assert.Equal(esperado, new Seudonimizador(Nombres).Ocultar(texto));
    }

    [Fact]
    public void Repone_los_nombres_en_la_respuesta()
    {
        var s = new Seudonimizador(Nombres);
        Assert.Equal("María Gómez y Ana Torres necesitan apoyo; Estudiante Z no existe.",
            s.Mostrar("Estudiante B y Estudiante A necesitan apoyo; Estudiante Z no existe."));
    }

    [Fact]
    public async Task Al_modelo_externo_no_le_llegan_nombres_reales_y_la_respuesta_los_recupera()
    {
        var interno = new ModeloSimulado().Responde("""{ "respuesta": "Estudiante B necesita una tutoría." }""");
        var modelo = new ModeloSeudonimizado(interno, new Seudonimizador(Nombres));

        var r = await modelo.ChatAsync(
            [new MensajeChat("system", "Eres un asistente."), new MensajeChat("user", "Datos: [{\"nombre\":\"María Gómez\",\"nota\":3}] ¿Qué necesita Maria Gomez?")],
            null, null, CancellationToken.None);

        var enviado = string.Join("\n", interno.Recibidos[0].Select(m => m.Contenido));
        Assert.DoesNotContain("María", enviado);
        Assert.DoesNotContain("Maria", enviado);
        Assert.Contains("Estudiante B", enviado);
        Assert.Contains("María Gómez necesita una tutoría.", r.Contenido);
    }

    // ------------------------------------------------------------------ Respaldo

    [Fact]
    public async Task Si_Gemini_falla_la_consulta_continua_con_el_modelo_local()
    {
        var gemini = new ModeloSimulado { Caido = true };
        var local = new ModeloSimulado().Responde("uno").Responde("dos");
        var modelo = new ModeloConRespaldo(gemini, local, NullLogger.Instance);

        var r1 = await modelo.ChatAsync([new MensajeChat("user", "hola")], null, null, CancellationToken.None);
        var r2 = await modelo.ChatAsync([new MensajeChat("user", "hola")], null, null, CancellationToken.None);

        Assert.Equal("uno", r1.Contenido);
        Assert.Equal("dos", r2.Contenido);
        Assert.True(modelo.UsoRespaldo);
        Assert.Equal("modelo-simulado", modelo.NombreModelo);
        Assert.Contains("Ollama no responde", modelo.Motivo);
    }

    // ------------------------------------------------------------------ Cliente de la API

    private static GeminiClient Cliente(ManejadorSimulado manejador, string? clave = "clave-de-prueba") =>
        new(new HttpClient(manejador) { BaseAddress = new Uri("https://gemini.test/v1beta/") },
            Options.Create(new GeminiOptions { ApiKey = clave, Modelo = "gemini-prueba", NivelPensamiento = "minimal", ModeloAlternativo = null, EsperaReintentoMs = 1 }),
            NullLogger<GeminiClient>.Instance);

    private const string RespuestaTexto = """
        { "candidates": [ { "content": { "role": "model", "parts": [
            { "text": "pensando...", "thought": true },
            { "text": "{\"respuesta\":\"ok\"}" } ] } } ],
          "usageMetadata": { "promptTokenCount": 10, "candidatesTokenCount": 5 } }
        """;

    [Fact]
    public async Task Envia_la_peticion_con_el_formato_de_Gemini()
    {
        var manejador = new ManejadorSimulado((HttpStatusCode.OK, RespuestaTexto));
        var esquema = JsonDocument.Parse("""{"type":"object","properties":{"respuesta":{"type":"string"}}}""").RootElement;

        var r = await Cliente(manejador).ChatAsync(
        [
            new MensajeChat("system", "Instrucciones"),
            new MensajeChat("user", "Pregunta"),
            new MensajeChat("assistant", "", [new LlamadaHerramienta("identificar_alertas", JsonDocument.Parse("""{"curso_id":1}""").RootElement)]),
            new MensajeChat("tool", """{"alertas":[]}""", NombreHerramienta: "identificar_alertas"),
            new MensajeChat("tool", """{"mensaje":"x"}""", NombreHerramienta: "obtener_progreso")
        ], HerramientasAcademicas.Definiciones, esquema, CancellationToken.None);

        Assert.Equal("{\"respuesta\":\"ok\"}", r.Contenido); // el razonamiento ("thought") no se incluye
        var (peticion, cuerpo) = manejador.Recibidas.Single();
        Assert.Equal("https://gemini.test/v1beta/models/gemini-prueba:generateContent", peticion.RequestUri!.ToString());
        Assert.Equal("clave-de-prueba", peticion.Headers.GetValues("x-goog-api-key").Single());
        using var doc = JsonDocument.Parse(cuerpo);
        var raiz = doc.RootElement;
        Assert.Equal("Instrucciones", raiz.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        var contenidos = raiz.GetProperty("contents");
        Assert.Equal(["user", "model", "user"], contenidos.EnumerateArray().Select(c => c.GetProperty("role").GetString()));
        Assert.Equal(2, contenidos[2].GetProperty("parts").GetArrayLength()); // las dos respuestas de herramientas, juntas
        Assert.Equal("identificar_alertas", contenidos[1].GetProperty("parts")[0].GetProperty("functionCall").GetProperty("name").GetString());
        var config = raiz.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.True(config.TryGetProperty("responseJsonSchema", out _));
        Assert.Equal("minimal", config.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.DoesNotContain("additionalProperties", raiz.GetProperty("tools").GetRawText());
    }

    [Fact]
    public async Task Lee_las_llamadas_a_herramientas()
    {
        var manejador = new ManejadorSimulado((HttpStatusCode.OK, """
            { "candidates": [ { "content": { "parts": [ { "functionCall": { "name": "generar_resumen", "args": { "curso_id": 3 } } } ] } } ] }
            """));

        var r = await Cliente(manejador).ChatAsync([new MensajeChat("user", "Resumen")], HerramientasAcademicas.Definiciones, null, CancellationToken.None);

        var llamada = Assert.Single(r.LlamadasHerramienta!);
        Assert.Equal("generar_resumen", llamada.Nombre);
        Assert.Equal(3, llamada.Argumentos.GetProperty("curso_id").GetInt32());
    }

    [Fact]
    public async Task Si_el_modelo_no_acepta_el_nivel_de_razonamiento_reintenta_sin_el()
    {
        var manejador = new ManejadorSimulado(
            (HttpStatusCode.BadRequest, """{"error":{"message":"Thinking level MINIMAL is not supported for this model."}}"""),
            (HttpStatusCode.OK, RespuestaTexto));

        await Cliente(manejador).ChatAsync([new MensajeChat("user", "hola")], null, null, CancellationToken.None);

        Assert.Equal(2, manejador.Recibidas.Count);
        Assert.Contains("thinkingConfig", manejador.Recibidas[0].Cuerpo);
        Assert.DoesNotContain("thinkingConfig", manejador.Recibidas[1].Cuerpo);
    }

    [Fact]
    public async Task Si_Gemini_esta_sobrecargado_reintenta_y_luego_usa_el_modelo_alternativo()
    {
        var manejador = new ManejadorSimulado(
            (HttpStatusCode.ServiceUnavailable, "{}"),
            (HttpStatusCode.ServiceUnavailable, "{}"),
            (HttpStatusCode.OK, RespuestaTexto));
        var cliente = new GeminiClient(new HttpClient(manejador) { BaseAddress = new Uri("https://gemini.test/v1beta/") },
            Options.Create(new GeminiOptions { ApiKey = "k", Modelo = "principal", ModeloAlternativo = "alternativo", EsperaReintentoMs = 1 }),
            NullLogger<GeminiClient>.Instance);

        var r = await cliente.ChatAsync([new MensajeChat("user", "hola")], null, null, CancellationToken.None);

        Assert.Equal("{\"respuesta\":\"ok\"}", r.Contenido);
        Assert.Equal(["principal", "principal", "alternativo"],
            manejador.Recibidas.Select(x => x.Peticion.RequestUri!.Segments[^1].Split(':')[0]));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "cuota")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "sobrecargado")]
    [InlineData(HttpStatusCode.Forbidden, "clave")]
    public async Task Los_errores_de_Gemini_se_informan_como_modelo_no_disponible(HttpStatusCode codigo, string motivo)
    {
        var manejador = new ManejadorSimulado((codigo, """{"error":{"message":"x"}}"""));

        var ex = await Assert.ThrowsAsync<ModeloNoDisponibleException>(() =>
            Cliente(manejador).ChatAsync([new MensajeChat("user", "hola")], null, null, CancellationToken.None));

        Assert.Contains(motivo, ex.Message);
    }

    [Fact]
    public async Task Sin_clave_Gemini_no_se_llama()
    {
        var manejador = new ManejadorSimulado((HttpStatusCode.OK, RespuestaTexto));

        await Assert.ThrowsAsync<ModeloNoDisponibleException>(() =>
            Cliente(manejador, clave: null).ChatAsync([new MensajeChat("user", "hola")], null, null, CancellationToken.None));
        Assert.Empty(manejador.Recibidas);
    }
}

/// <summary>Endpoint con proveedor "gemini": seudonimiza, y si Gemini falla responde el modelo local con un aviso.</summary>
public class ChatGeminiEndpointTests : IDisposable
{
    private const string Clave = "clave-de-pruebas-del-aiservice-1234567890-abcdef";
    private readonly ModeloSimulado _local = new();
    private readonly ManejadorSimulado _gemini;
    private readonly WebApplicationFactory<Program> _factory;

    public ChatGeminiEndpointTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", Clave);
        Environment.SetEnvironmentVariable("Gemini__ApiKey", "clave-gemini-de-prueba");
        _gemini = new ManejadorSimulado((HttpStatusCode.ServiceUnavailable, """{"error":{"message":"high demand"}}"""));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IModeloLenguaje>();
            s.AddSingleton<IModeloLenguaje>(_local);
            s.RemoveAll<IBackendAcademico>();
            s.AddSingleton<IBackendAcademico>(Datos.BackendCompleto());
            s.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => _gemini);
        }));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("Gemini__ApiKey", null);
        _factory.Dispose();
    }

    private static string Token() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "edumonitor-auth",
        Audience = "edumonitor-clients",
        Expires = DateTime.UtcNow.AddMinutes(10),
        Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("role", "Docente")]),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Clave)), SecurityAlgorithms.HmacSha256)
    });

    [Fact]
    public async Task Gemini_sobrecargado_responde_el_modelo_local_y_no_salieron_nombres()
    {
        _local.Responde("""{ "respuesta": "Hay 2 alertas.", "hechos": [], "inferencias": [], "recomendaciones_ids": [1] }""");
        var cliente = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/ia/chat")
        {
            Content = JsonContent.Create(new { mensaje = "¿Qué necesita María Gómez?", cursoId = 1, proveedor = "gemini" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        var resp = await cliente.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var r = await resp.Content.ReadFromJsonAsync<RespuestaAgenteDto>();
        Assert.Equal("modelo-simulado", r!.Modelo);
        Assert.Contains(r.Advertencias, a => a.Contains("Gemini no respondió") && a.Contains("sobrecargado"));
        // Se intentó con Gemini y lo enviado no contenía nombres reales (los datos simulados tienen 3 estudiantes).
        // Reintento con el mismo modelo y luego el alternativo, antes de pasar al modelo local.
        Assert.Equal(3, _gemini.Recibidas.Count);
        var enviado = string.Join('\n', _gemini.Recibidas.Select(x => x.Cuerpo));
        Assert.DoesNotContain("María Gómez", enviado);
        Assert.DoesNotContain("Mar\\u00EDa", enviado);
        Assert.DoesNotContain("Pedro Castillo", enviado);
        Assert.Contains("Estudiante", enviado);
    }

    [Fact]
    public async Task El_estado_informa_si_Gemini_esta_configurado()
    {
        var cliente = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/ia/estado");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        var json = await (await cliente.SendAsync(req)).Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("gemini").GetProperty("configurado").GetBoolean());
        Assert.DoesNotContain("clave-gemini-de-prueba", json); // la clave nunca se expone
    }
}
