using Microsoft.Extensions.Logging;
using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;
using Portfolio.Core.Investments;
using Portfolio.UseCases.Errors;

namespace Portfolio.UseCases.Investments;

public class InvestmentService(
    IInvestmentRepository investments,
    IAssetClassRepository assetClasses,
    IUnitOfWork unitOfWork,
    ILogger<InvestmentService> logger) : IInvestmentService
{
    public async Task<IReadOnlyList<InvestmentView>> ListAsync(Guid investorId, CancellationToken ct = default)
    {
        var found = await investments.ListByInvestorAsync(investorId, ct);
        return [.. found.Select(Project)];
    }

    public async Task<InvestmentView> GetAsync(Guid investorId, Guid id, CancellationToken ct = default) =>
        Project(await RequireOwnedAsync(investorId, id, ct));

    public async Task<PortfolioSummary> SummarizeAsync(Guid investorId, CancellationToken ct = default)
    {
        var totals = await investments.TotalsByAssetClassAsync(investorId, ct);
        var grandTotal = totals.Sum(t => t.Total);

        var slices = totals
            .OrderByDescending(t => t.Total)
            .Select(t => new PortfolioSlice(
                t.AssetClassName,
                t.Count,
                t.Total,
                grandTotal == 0 ? 0m : decimal.Round(t.Total / grandTotal * 100m, 2)))
            .ToList();

        return new PortfolioSummary(grandTotal, totals.Sum(t => t.Count), slices);
    }

    public async Task<InvestmentView> CreateAsync(Guid investorId, InvestmentRequest request, CancellationToken ct = default)
    {
        var assetClass = await ResolveAssetClassAsync(request.AssetClass, ct);

        var investment = new Investment(
            investorId,
            assetClass,
            request.Amount,
            request.InvestedOn!.Value,
            request.Institution,
            request.Notes);

        investments.Add(investment);
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Aporte {InvestmentId} criado para o investidor {InvestorId}.", investment.Id, investorId);
        return Project(investment);
    }

    public async Task<InvestmentView> UpdateAsync(Guid investorId, Guid id, InvestmentRequest request, CancellationToken ct = default)
    {
        var investment = await RequireOwnedAsync(investorId, id, ct);
        var assetClass = await ResolveAssetClassAsync(request.AssetClass, ct);

        investment.Revise(
            assetClass,
            request.Amount,
            request.InvestedOn!.Value,
            request.Institution,
            request.Notes);

        // Nao existe chamada a um Update() de repositorio: a entidade veio rastreada
        // pelo DbContext, entao o SaveAsync sozinho ja detecta o que mudou.
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Aporte {InvestmentId} alterado pelo investidor {InvestorId}.", id, investorId);
        return Project(investment);
    }

    public async Task DeleteAsync(Guid investorId, Guid id, CancellationToken ct = default)
    {
        var investment = await RequireOwnedAsync(investorId, id, ct);

        investments.Remove(investment);
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Aporte {InvestmentId} removido pelo investidor {InvestorId}.", id, investorId);
    }

    /// <summary>
    /// Ponto unico de controle de acesso. Se o aporte nao for do investidor
    /// autenticado a resposta e 404, e nao 403: um 403 confirmaria que o id existe.
    /// </summary>
    private async Task<Investment> RequireOwnedAsync(Guid investorId, Guid id, CancellationToken ct) =>
        await investments.FindAsync(id, investorId, ct)
        ?? throw new ResourceNotFoundException($"Aporte {id} nao encontrado.");

    /// <summary>
    /// Troca o texto enviado pelo cliente pela classe de ativo cadastrada. Quando
    /// nao existe, a mensagem de erro ja lista as opcoes validas.
    /// </summary>
    private async Task<AssetClass> ResolveAssetClassAsync(string name, CancellationToken ct)
    {
        var assetClass = await assetClasses.FindBySlugAsync(Slugger.From(name), ct);
        if (assetClass is not null) return assetClass;

        var available = string.Join(", ", (await assetClasses.ListAsync(ct)).Select(u => u.AssetClass.Name));
        throw new BusinessRuleException(
            $"A classe de ativo '{name}' nao existe. Disponiveis: {available}. " +
            "Para incluir outra, use POST /asset-classes.");
    }

    private static InvestmentView Project(Investment investment) =>
        new(investment.Id,
            new AssetClassRef(
                investment.AssetClassId,
                investment.AssetClass?.Name ?? string.Empty,
                investment.AssetClass?.Slug ?? string.Empty),
            investment.Amount,
            investment.InvestedOn,
            investment.Institution,
            investment.Notes,
            investment.CreatedAt,
            investment.ChangedAt);
}
