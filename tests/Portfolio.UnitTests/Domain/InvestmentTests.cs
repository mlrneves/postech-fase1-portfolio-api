using Portfolio.Core.Abstractions;
using Portfolio.Core.Investments;
using Portfolio.UnitTests.Support;

namespace Portfolio.UnitTests.Domain;

public class InvestmentTests
{
    private static readonly Guid Investor = Guid.NewGuid();

    [Fact]
    public void Deve_criar_o_aporte_com_os_dados_informados()
    {
        var investedOn = Fake.Yesterday();

        var investment = new Investment(Investor, Fake.Shares(), 1_500.50m, investedOn, "XP", "PETR4");

        investment.Id.Should().NotBeEmpty();
        investment.InvestorId.Should().Be(Investor);
        investment.AssetClassId.Should().Be(1);
        investment.AssetClass!.Name.Should().Be("Ações");
        investment.Amount.Should().Be(1_500.50m);
        investment.InvestedOn.Should().Be(investedOn);
        investment.Institution.Should().Be("XP");
        investment.Notes.Should().Be("PETR4");
        investment.ChangedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    public void Nao_deve_aceitar_valor_menor_ou_igual_a_zero(decimal amount)
    {
        var criar = () => new Investment(Investor, Fake.Shares(), amount, Fake.Yesterday(), null, null);

        criar.Should().Throw<BusinessRuleException>().WithMessage("*maior que zero*");
    }

    [Fact]
    public void Nao_deve_aceitar_valor_acima_do_limite_da_coluna()
    {
        var criar = () => new Investment(Investor, Fake.Shares(), Investment.MaxAmount + 1, Fake.Yesterday(), null, null);

        criar.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Deve_arredondar_o_valor_para_duas_casas()
    {
        var investment = new Investment(Investor, Fake.Shares(), 10.005m, Fake.Yesterday(), null, null);

        investment.Amount.Should().Be(10.00m);
    }

    [Fact]
    public void Deve_aceitar_aporte_feito_hoje()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var investment = new Investment(Investor, Fake.Shares(), 10m, hoje, null, null);

        investment.InvestedOn.Should().Be(hoje);
    }

    [Fact]
    public void Nao_deve_aceitar_data_no_futuro()
    {
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var criar = () => new Investment(Investor, Fake.Shares(), 10m, amanha, null, null);

        criar.Should().Throw<BusinessRuleException>().WithMessage("*futuro*");
    }

    [Fact]
    public void Nao_deve_aceitar_data_em_branco()
    {
        var criar = () => new Investment(Investor, Fake.Shares(), 10m, default, null, null);

        criar.Should().Throw<BusinessRuleException>().WithMessage("*data do aporte*");
    }

    [Fact]
    public void Nao_deve_aceitar_aporte_sem_investidor()
    {
        var criar = () => new Investment(Guid.Empty, Fake.Shares(), 10m, Fake.Yesterday(), null, null);

        criar.Should().Throw<BusinessRuleException>().WithMessage("*investidor*");
    }

    [Fact]
    public void Nao_deve_aceitar_observacao_maior_que_o_limite()
    {
        var notes = new string('x', Investment.MaxNotesLength + 1);

        var criar = () => new Investment(Investor, Fake.Shares(), 10m, Fake.Yesterday(), null, notes);

        criar.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Campos_opcionais_em_branco_devem_virar_nulo()
    {
        var investment = new Investment(Investor, Fake.Shares(), 10m, Fake.Yesterday(), "   ", "");

        investment.Institution.Should().BeNull();
        investment.Notes.Should().BeNull();
    }

    [Fact]
    public void Revise_deve_trocar_os_dados_e_registrar_a_alteracao()
    {
        var investment = new Investment(Investor, Fake.Shares(), 100m, Fake.Yesterday(), "XP", "PETR4");
        var novaData = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        investment.Revise(Fake.FixedIncome(), 2_500m, novaData, "Nubank", "CDB 110% CDI");

        investment.AssetClassId.Should().Be(2);
        investment.Amount.Should().Be(2_500m);
        investment.InvestedOn.Should().Be(novaData);
        investment.Institution.Should().Be("Nubank");
        investment.Notes.Should().Be("CDB 110% CDI");
        investment.ChangedAt.Should().NotBeNull();
    }

    [Fact]
    public void Revise_invalido_nao_deve_deixar_o_aporte_em_estado_quebrado()
    {
        var investment = new Investment(Investor, Fake.Shares(), 100m, Fake.Yesterday(), null, null);

        var alterar = () => investment.Revise(Fake.Crypto(), -5m, Fake.Yesterday(), null, null);

        alterar.Should().Throw<BusinessRuleException>();
        investment.Amount.Should().Be(100m);
        investment.AssetClassId.Should().Be(1);
        investment.ChangedAt.Should().BeNull();
    }
}
