namespace Portfolio.Core.Investments;

/// <summary>
/// Toda consulta recebe o id do investidor. Nao existe busca so por id de
/// investimento: e isso que impede um usuario de alcancar o aporte de outro.
/// </summary>
public interface IInvestmentRepository
{
    Task<IReadOnlyList<Investment>> ListByInvestorAsync(Guid investorId, CancellationToken ct = default);

    Task<Investment?> FindAsync(Guid id, Guid investorId, CancellationToken ct = default);

    Task<IReadOnlyList<AssetClassTotal>> TotalsByAssetClassAsync(Guid investorId, CancellationToken ct = default);

    void Add(Investment investment);

    void Remove(Investment investment);
}
