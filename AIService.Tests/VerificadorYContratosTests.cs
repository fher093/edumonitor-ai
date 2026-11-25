using System.Text.Json;
using AIService.Agente;

namespace AIService.Tests;

public class VerificadorHechosTests
{
    private static readonly HashSet<decimal> Cifras = VerificadorHechos.CifrasDe([
        """{"promedio_curso":13.42,"escala_promedio":20,"alertas":[{"nombre":"Ana"},{"nombre":"Luis"}],"hallazgo":"logro 52.0 % de la Lección 3"}"""
    ]);

    [Theory]
    [InlineData("El promedio del curso es 13.42/20.")]
    [InlineData("El promedio del curso es 13,4 sobre 20.")]   // redondeo y coma decimal
    [InlineData("El promedio es aproximadamente 13 sobre 20.")] // entero redondeado
    [InlineData("Hay 2 alertas activas.")]                      // conteo de la lista
    [InlineData("La Lección 3 tuvo un logro del 52 %.")]
    [InlineData("Revisar la regla RN-05 el 20/11/2026 a las 15:30.")] // códigos, fechas y horas no cuentan
    [InlineData("Sin cifras: el curso necesita atención.")]
    public void Acepta_afirmaciones_respaldadas_por_los_datos(string texto)
    {
        Assert.True(VerificadorHechos.EsVerificable(texto, Cifras));
    }

    [Theory]
    [InlineData("El promedio del curso es 15.8/20.")]
    [InlineData("Hay 7 alertas activas.")]
    [InlineData("El 87 % aprobará.")]
    public void Rechaza_cifras_que_no_estan_en_los_datos(string texto)
    {
        Assert.False(VerificadorHechos.EsVerificable(texto, Cifras));
    }
}

/// <summary>Las herramientas implementadas coinciden con los contratos documentados (TT-04.3).</summary>
public class ContratosHerramientasTests
{
    private static JsonElement Contratos()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EduMonitor.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var json = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "agent-tools-contracts.json"));
        return JsonDocument.Parse(json).RootElement.GetProperty("tools").Clone();
    }

    [Fact]
    public void Las_cinco_herramientas_coinciden_con_docs_agent_tools_contracts()
    {
        var documentadas = Contratos().EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();

        Assert.Equal(5, HerramientasAcademicas.Definiciones.Count);
        Assert.True(documentadas.SetEquals(HerramientasAcademicas.Nombres));
    }

    [Fact]
    public void Los_parametros_obligatorios_coinciden_con_el_contrato()
    {
        foreach (var contrato in Contratos().EnumerateArray())
        {
            var nombre = contrato.GetProperty("name").GetString();
            var definicion = HerramientasAcademicas.Definiciones.Single(d => d.Nombre == nombre);
            var requeridos = (JsonElement p) => p.TryGetProperty("required", out var r)
                ? r.EnumerateArray().Select(x => x.GetString()).OrderBy(x => x).ToList()
                : [];
            Assert.Equal(requeridos(contrato.GetProperty("parameters")), requeridos(definicion.Parametros));
        }
    }
}
