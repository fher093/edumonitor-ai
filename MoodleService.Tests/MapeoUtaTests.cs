using MoodleService.Extraccion;
using MoodleService.Seguridad;
using Xunit;

namespace MoodleService.Tests;

/// <summary>Ubicación de ítems de calificación de Moodle en el modelo de evaluación UTA.</summary>
public class MapeoUtaTests
{
    private static readonly DateTime Inicio = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Fin = new(2027, 2, 28, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("P1-CD-01", "contacto_docente", 1)]
    [InlineData("p2-pe", "practico_experimental", 2)]
    [InlineData("P2_AA_7", "autonomo", 2)]
    public void Usa_la_convencion_del_numero_de_ID(string idNumber, string componente, int parcial)
    {
        var u = MapeoUta.Ubicar(idNumber, [], "Cualquier nombre", "assign", null, null, null);
        Assert.Equal(new UbicacionUta(componente, parcial, "idnumber"), u);
    }

    [Theory]
    [InlineData("Parcial 1", "Aprendizaje en contacto con el docente", "contacto_docente", 1)]
    [InlineData("Segundo parcial", "Aprendizaje práctico-experimental", "practico_experimental", 2)]
    [InlineData("PARCIAL 2", "Aprendizaje Autónomo", "autonomo", 2)]
    [InlineData("P1", "Docencia", "contacto_docente", 1)]
    public void Usa_las_categorias_del_libro_de_calificaciones(string parcialCat, string componenteCat, string componente, int parcial)
    {
        var u = MapeoUta.Ubicar(null, [parcialCat, componenteCat], "Tarea", "assign", null, null, null);
        Assert.Equal(new UbicacionUta(componente, parcial, "categoria"), u);
    }

    [Fact]
    public void Combina_categoria_y_nombre_del_item()
    {
        var u = MapeoUta.Ubicar(null, ["Parcial 2"], "Informe práctico de laboratorio", "assign", null, null, null);
        Assert.Equal(new UbicacionUta("practico_experimental", 2, "categoria"), u);
    }

    [Fact]
    public void Infiere_parcial_por_fecha_y_componente_por_modulo_cuando_no_hay_estructura()
    {
        var antesDeLaMitad = MapeoUta.Ubicar(null, [], "Quiz", "quiz", new DateTime(2026, 11, 1), Inicio, Fin);
        Assert.Equal(new UbicacionUta("contacto_docente", 1, "inferido"), antesDeLaMitad);

        var despues = MapeoUta.Ubicar(null, [], "Entrega", "assign", new DateTime(2027, 2, 1), Inicio, Fin);
        Assert.Equal(new UbicacionUta("autonomo", 2, "inferido"), despues);
    }

    [Fact]
    public void Normaliza_tildes_y_mayusculas()
    {
        Assert.Equal("aprendizaje autonomo", MapeoUta.Normalizar("Aprendizaje AUTÓNOMO"));
    }

    [Fact]
    public void El_token_se_cifra_y_se_descifra_sin_exponerlo()
    {
        var cifrado = new CifradoToken("clave-de-pruebas-de-al-menos-32-caracteres!!");
        const string token = "0123456789abcdef0123456789abcdef";

        var almacenado = cifrado.Cifrar(token);

        Assert.DoesNotContain(token, almacenado);
        Assert.NotEqual(almacenado, cifrado.Cifrar(token)); // nonce aleatorio
        Assert.Equal(token, cifrado.Descifrar(almacenado));
        Assert.Equal("••••••••cdef", CifradoToken.Enmascarar(token));
    }
}
