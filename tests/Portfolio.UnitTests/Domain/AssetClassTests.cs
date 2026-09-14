using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;

namespace Portfolio.UnitTests.Domain;

public class AssetClassTests
{
    [Fact]
    public void Deve_guardar_o_nome_com_acento_e_derivar_o_slug_sem_acento()
    {
        var assetClass = new AssetClass("  Fundos Imobiliários  ");

        assetClass.Name.Should().Be("Fundos Imobiliários");
        assetClass.Slug.Should().Be("fundos-imobiliarios");
        assetClass.ChangedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("Ações", "acoes")]
    [InlineData("ACOES", "acoes")]
    [InlineData("acoes", "acoes")]
    [InlineData("  Ações  ", "acoes")]
    [InlineData("Tesouro   Direto", "tesouro-direto")]
    [InlineData("Cripto/Moedas", "cripto-moedas")]
    public void Variacoes_do_mesmo_nome_devem_cair_no_mesmo_slug(string name, string expected)
    {
        new AssetClass(name).Slug.Should().Be(expected);
    }

    [Fact]
    public void Rename_deve_atualizar_nome_slug_e_marcar_a_alteracao()
    {
        var assetClass = new AssetClass("Fiis");

        assetClass.Rename("Fundos Imobiliários");

        assetClass.Name.Should().Be("Fundos Imobiliários");
        assetClass.Slug.Should().Be("fundos-imobiliarios");
        assetClass.ChangedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("!!!")]
    public void Nao_deve_aceitar_nome_sem_letras_ou_numeros(string name)
    {
        var criar = () => new AssetClass(name);

        criar.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Nao_deve_aceitar_nome_maior_que_o_limite_da_coluna()
    {
        var criar = () => new AssetClass(new string('a', AssetClass.MaxNameLength + 1));

        criar.Should().Throw<BusinessRuleException>().WithMessage($"*{AssetClass.MaxNameLength}*");
    }
}
