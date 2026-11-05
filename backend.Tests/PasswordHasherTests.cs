using Backend.Services;

namespace Backend.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_devuelve_true_con_la_clave_correcta()
    {
        var hash = _hasher.Hash("Clave.Segura-1");
        Assert.True(_hasher.Verify("Clave.Segura-1", hash));
    }

    [Fact]
    public void Verify_devuelve_false_con_clave_incorrecta()
    {
        var hash = _hasher.Hash("Clave.Segura-1");
        Assert.False(_hasher.Verify("otra-clave", hash));
    }

    [Fact]
    public void El_hash_no_contiene_la_clave_y_usa_sal_distinta_cada_vez()
    {
        var h1 = _hasher.Hash("Clave.Segura-1");
        var h2 = _hasher.Hash("Clave.Segura-1");
        Assert.DoesNotContain("Clave.Segura-1", h1);
        Assert.NotEqual(h1, h2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("v1.210000.no-es-base64.tampoco")]
    public void Verify_devuelve_false_con_hash_malformado(string hashInvalido)
    {
        Assert.False(_hasher.Verify("lo-que-sea", hashInvalido));
    }
}
