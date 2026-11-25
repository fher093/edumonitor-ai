using AIService.Agente;
using AIService.Clientes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIService.Tests;

/// <summary>
/// Hallazgos de la evaluación con el modelo real (TT-11): qwen2.5-coder escribe las llamadas a herramientas
/// como texto JSON, y una consulta sobre otro curso no debe responderse con los datos del curso activo.
/// </summary>
public class CompatibilidadModeloTests
{
    private static readonly IReadOnlySet<string> Nombres = HerramientasAcademicas.Nombres;

    [Theory]
    [InlineData("""{ "name": "identificar_alertas", "arguments": { "curso_id": 1 } }""")]
    [InlineData("```json\n{\"name\":\"identificar_alertas\",\"arguments\":{\"curso_id\":1}}\n```")]
    [InlineData("<tool_call>\n{\"name\": \"identificar_alertas\", \"arguments\": {\"curso_id\": 1}}\n</tool_call>")]
    [InlineData("""{ "function": { "name": "identificar_alertas", "arguments": "{\"curso_id\":1}" } }""")]
    public void Reconoce_una_llamada_escrita_como_texto(string contenido)
    {
        var llamada = Assert.Single(OllamaClient.ExtraerLlamadasDeTexto(contenido, Nombres));
        Assert.Equal("identificar_alertas", llamada.Nombre);
        Assert.Equal(1, llamada.Argumentos.GetProperty("curso_id").GetInt32());
    }

    [Fact]
    public void Reconoce_varias_llamadas_en_una_lista()
    {
        var llamadas = OllamaClient.ExtraerLlamadasDeTexto(
            """[{"name":"generar_resumen","arguments":{"curso_id":1}},{"name":"obtener_progreso","arguments":{"curso_id":1}}]""", Nombres);

        Assert.Equal(["generar_resumen", "obtener_progreso"], llamadas.Select(l => l.Nombre));
    }

    [Theory]
    [InlineData("El curso tiene 3 estudiantes con alertas.")]
    [InlineData("""{ "name": "borrar_calificaciones", "arguments": { "curso_id": 1 } }""")]
    [InlineData("""{ "hechos": [], "inferencias": [], "recomendaciones": [] }""")]
    public void Ignora_texto_normal_y_herramientas_desconocidas(string contenido)
    {
        Assert.Empty(OllamaClient.ExtraerLlamadasDeTexto(contenido, Nombres));
    }

    [Theory]
    [InlineData("Muéstrame las alertas del curso con id 9999", 9999)]
    [InlineData("¿cómo va el curso 12?", 12)]
    [InlineData("datos del curso_id=5", 5)]
    [InlineData("Resumen del curso", null)]
    [InlineData("¿Qué estudiantes necesitan apoyo?", null)]
    public void Detecta_el_curso_mencionado(string mensaje, int? esperado)
    {
        Assert.Equal(esperado, AgenteAcademico.CursoMencionado(mensaje));
    }

    [Fact]
    public async Task Consulta_automatica_sobre_otro_curso_no_usa_el_curso_activo()
    {
        var modelo = new ModeloSimulado().Responde("No sé.");
        var agente = new AgenteAcademico(modelo, new HerramientasAcademicas(Datos.BackendCompleto()), NullLogger<AgenteAcademico>.Instance);

        var r = await agente.ResponderAsync(
            new PeticionChat("Muéstrame las alertas del curso con id 9999", 1), new ContextoConsulta("jwt", "c1"), CancellationToken.None);

        Assert.Equal("sin_acceso", r.Estado);
        Assert.All(r.Traza, t => Assert.Contains("9999", t.Argumentos));
    }
}
