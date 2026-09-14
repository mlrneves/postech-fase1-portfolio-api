using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;
using Portfolio.Core.Investments;
using Portfolio.UnitTests.Support;
using Portfolio.UseCases.Errors;
using Portfolio.UseCases.Investments;

namespace Portfolio.UnitTests.UseCases;

/// <summary>
/// Casos de uso de aporte com repositorios simulados: aqui interessa o fluxo de
/// decisao (o que grava, o que recusa, o que nao chega a commitar), nao o SQL.
/// </summary>
public class InvestmentServiceTests
{
    private readonly IInvestmentRepository _investments = Substitute.For<IInvestmentRepository>();
    private readonly IAssetClassRepository _assetClasses = Substitute.For<IAssetClassRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly InvestmentService _service;

    private readonly Guid _investor = Guid.NewGuid();
    private readonly AssetClass _shares = Fake.Shares();
    private readonly AssetClass _fixedIncome = Fake.FixedIncome();

    public InvestmentServiceTests()
    {
        _assetClasses.FindBySlugAsync("acoes", Arg.Any<CancellationToken>()).Returns(_shares);
        _assetClasses.FindBySlugAsync("renda-fixa", Arg.Any<CancellationToken>()).Returns(_fixedIncome);
        _assetClasses.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new AssetClassUsage(_shares, 0),
            new AssetClassUsage(_fixedIncome, 0)
        ]);

        _service = new InvestmentService(
            _investments,
            _assetClasses,
            _unitOfWork,
            NullLogger<InvestmentService>.Instance);
    }

    private InvestmentRequest Request(string assetClass = "Ações", decimal amount = 1_000m) =>
        new(assetClass, amount, Fake.Yesterday(), "XP", "PETR4");

    [Fact]
    public async Task Create_deve_gravar_o_aporte_e_fechar_a_unidade_de_trabalho()
    {
        var view = await _service.CreateAsync(_investor, Request());

        view.Amount.Should().Be(1_000m);
        view.AssetClass.Id.Should().Be(1);
        view.AssetClass.Name.Should().Be("Ações");
        view.AssetClass.Slug.Should().Be("acoes");

        _investments.Received(1).Add(Arg.Is<Investment>(i => i.InvestorId == _investor));
        await _unitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Ações")]
    [InlineData("ACOES")]
    [InlineData("acoes")]
    [InlineData("  ações  ")]
    public async Task Create_deve_resolver_a_classe_de_ativo_ignorando_acento_e_caixa(string informado)
    {
        var view = await _service.CreateAsync(_investor, Request(informado));

        view.AssetClass.Id.Should().Be(1);
    }

    [Fact]
    public async Task Create_com_classe_inexistente_deve_falhar_listando_as_disponiveis()
    {
        var criar = () => _service.CreateAsync(_investor, Request("Previdencia"));

        var erro = await criar.Should().ThrowAsync<BusinessRuleException>();
        erro.And.Message.Should().Contain("Previdencia").And.Contain("Ações").And.Contain("Renda Fixa");

        _investments.DidNotReceive().Add(Arg.Any<Investment>());
        await _unitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_de_aporte_de_outro_investidor_deve_dar_NotFound()
    {
        _investments.FindAsync(Arg.Any<Guid>(), _investor, Arg.Any<CancellationToken>())
            .Returns((Investment?)null);

        var alterar = () => _service.UpdateAsync(_investor, Guid.NewGuid(), Request());

        await alterar.Should().ThrowAsync<ResourceNotFoundException>();
        await _unitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_deve_trocar_a_classe_de_ativo_sem_chamar_nenhum_metodo_de_update_no_repositorio()
    {
        var investment = new Investment(_investor, _shares, 100m, Fake.Yesterday(), null, null);
        _investments.FindAsync(investment.Id, _investor, Arg.Any<CancellationToken>()).Returns(investment);

        var view = await _service.UpdateAsync(_investor, investment.Id, Request("Renda Fixa", 2_500m));

        view.AssetClass.Id.Should().Be(2);
        view.Amount.Should().Be(2_500m);
        view.ChangedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_deve_remover_o_aporte_do_proprio_investidor()
    {
        var investment = new Investment(_investor, _shares, 50m, Fake.Yesterday(), null, null);
        _investments.FindAsync(investment.Id, _investor, Arg.Any<CancellationToken>()).Returns(investment);

        await _service.DeleteAsync(_investor, investment.Id);

        _investments.Received(1).Remove(investment);
        await _unitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_de_aporte_inexistente_deve_dar_NotFound()
    {
        _investments.FindAsync(Arg.Any<Guid>(), _investor, Arg.Any<CancellationToken>())
            .Returns((Investment?)null);

        var remover = () => _service.DeleteAsync(_investor, Guid.NewGuid());

        await remover.Should().ThrowAsync<ResourceNotFoundException>();
        _investments.DidNotReceive().Remove(Arg.Any<Investment>());
    }

    [Fact]
    public async Task List_deve_repassar_o_id_do_investidor_para_o_repositorio()
    {
        _investments.ListByInvestorAsync(_investor, Arg.Any<CancellationToken>()).Returns(
        [
            new Investment(_investor, _shares, 100m, Fake.Yesterday(), null, null),
            new Investment(_investor, _fixedIncome, 200m, Fake.Yesterday(), null, null)
        ]);

        var result = await _service.ListAsync(_investor);

        result.Should().HaveCount(2);
        await _investments.Received(1).ListByInvestorAsync(_investor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Summary_deve_somar_os_totais_e_calcular_a_participacao()
    {
        _investments.TotalsByAssetClassAsync(_investor, Arg.Any<CancellationToken>()).Returns(
        [
            new AssetClassTotal(2, "Renda Fixa", 1, 250m),
            new AssetClassTotal(1, "Ações", 3, 750m)
        ]);

        var summary = await _service.SummarizeAsync(_investor);

        summary.TotalInvested.Should().Be(1_000m);
        summary.Count.Should().Be(4);

        // Ordenado do maior total para o menor.
        summary.ByAssetClass.Select(s => s.AssetClass).Should().ContainInOrder("Ações", "Renda Fixa");
        summary.ByAssetClass.Single(s => s.AssetClass == "Ações").Share.Should().Be(75m);
        summary.ByAssetClass.Single(s => s.AssetClass == "Renda Fixa").Share.Should().Be(25m);
    }

    [Fact]
    public async Task Summary_de_carteira_vazia_deve_voltar_zerado_e_sem_divisao_por_zero()
    {
        _investments.TotalsByAssetClassAsync(_investor, Arg.Any<CancellationToken>()).Returns([]);

        var summary = await _service.SummarizeAsync(_investor);

        summary.TotalInvested.Should().Be(0m);
        summary.Count.Should().Be(0);
        summary.ByAssetClass.Should().BeEmpty();
    }
}
