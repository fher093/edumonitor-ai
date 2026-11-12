using Xunit;
using MoodleService.Extraccion;

namespace MoodleService.Tests;

/// <summary>Ruta de la foto de perfil de Moodle y protección del endpoint de fotos.</summary>
public class FotosMoodleTests
{
    [Theory]
    [InlineData("http://localhost:8081/pluginfile.php/26/user/icon/boost/f1?rev=1517", "26/user/icon/boost/f1?rev=1517")]
    [InlineData("https://moodle.uta.edu.ec/pluginfile.php/5/user/icon/classic/f3", "5/user/icon/classic/f3")]
    public void Extrae_la_ruta_de_una_foto_subida(string url, string esperada)
    {
        Assert.Equal(esperada, FotosMoodle.RutaDesdeUrl(url));
    }

    [Theory]
    [InlineData("http://localhost:8081/theme/image.php/boost/core/1794269894/u/f1")] // imagen genérica: sin foto
    [InlineData("")]
    [InlineData(null)]
    public void Sin_foto_devuelve_null(string? url)
    {
        Assert.Null(FotosMoodle.RutaDesdeUrl(url));
    }

    [Theory]
    [InlineData("26/user/icon/boost/f1?rev=3", true)]
    [InlineData("26/mod_assign/submission_files/7/tarea.pdf", false)] // otros archivos de Moodle: prohibido
    [InlineData("26/user/icon/boost/f1?rev=3&token=x", false)]
    [InlineData("../../config.php", false)]
    [InlineData("26/user/icon/../../course/overviewfiles/a.png", false)]
    public void Solo_acepta_rutas_de_iconos_de_usuario(string ruta, bool valida)
    {
        Assert.Equal(valida, FotosMoodle.EsRutaValida(ruta));
    }
}
