using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Domain.Common;

namespace KRT.Onboarding.UnitTests.Domain;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData(" 529 982 247 25 ")]
    public void Create_WithValidCpf_StoresOnlyDigits(string input)
    {
        var cpf = Cpf.Create(input);

        Assert.Equal("52998224725", cpf.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("529.982.247-26")] // dígito verificador errado
    [InlineData("123.456.789-00")]
    [InlineData("111.111.111-11")] // todos os dígitos iguais
    [InlineData("5299822472")]     // 10 dígitos
    [InlineData("5299822472a")]    // caractere inválido
    public void Create_WithInvalidCpf_ThrowsDomainException(string? input)
    {
        Assert.Throws<DomainException>(() => Cpf.Create(input));
    }

    [Fact]
    public void Cpfs_WithSameDigits_AreEqual()
    {
        Assert.Equal(Cpf.Create("529.982.247-25"), Cpf.Create("52998224725"));
    }
}
