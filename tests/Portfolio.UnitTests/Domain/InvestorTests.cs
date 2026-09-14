using Portfolio.Core.Abstractions;
using Portfolio.Core.Investors;

namespace Portfolio.UnitTests.Domain;

public class InvestorTests
{
    [Fact]
    public void Deve_aparar_o_nome_e_padronizar_o_email_em_minusculas()
    {
        var investor = new Investor("  Marcio Rodrigues  ", "  Marcio@Email.COM ", "hash-qualquer");

        investor.FullName.Should().Be("Marcio Rodrigues");
        investor.Email.Should().Be("marcio@email.com");
        investor.Id.Should().NotBeEmpty();
        investor.RegisteredAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("", "a@b.com", "hash")]
    [InlineData("   ", "a@b.com", "hash")]
    [InlineData("Nome", "sem-arroba", "hash")]
    [InlineData("Nome", "sem@dominio", "hash")]
    [InlineData("Nome", "espaco no@email.com", "hash")]
    [InlineData("Nome", "a@b.com", "")]
    public void Nao_deve_aceitar_dados_invalidos(string name, string email, string hash)
    {
        var criar = () => new Investor(name, email, hash);

        criar.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Nao_deve_aceitar_nome_maior_que_o_limite_da_coluna()
    {
        var criar = () => new Investor(new string('a', Investor.MaxNameLength + 1), "a@b.com", "hash");

        criar.Should().Throw<BusinessRuleException>().WithMessage($"*{Investor.MaxNameLength}*");
    }

    [Fact]
    public void NormalizeEmail_deve_produzir_a_mesma_chave_para_variacoes_de_caixa()
    {
        Investor.NormalizeEmail(" ANA@Teste.com ").Should().Be(Investor.NormalizeEmail("ana@teste.COM"));
    }
}
