namespace Portfolio.UseCases.Investments;

public interface IInvestmentService
{
    Task<IReadOnlyList<InvestmentView>> ListAsync(Guid investorId, CancellationToken ct = default);

    Task<InvestmentView> GetAsync(Guid investorId, Guid id, CancellationToken ct = default);

    Task<PortfolioSummary> SummarizeAsync(Guid investorId, CancellationToken ct = default);

    Task<InvestmentView> CreateAsync(Guid investorId, InvestmentRequest request, CancellationToken ct = default);

    Task<InvestmentView> UpdateAsync(Guid investorId, Guid id, InvestmentRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid investorId, Guid id, CancellationToken ct = default);
}
