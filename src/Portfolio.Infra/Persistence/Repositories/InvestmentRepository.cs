using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Investments;

namespace Portfolio.Infra.Persistence.Repositories;

public class InvestmentRepository(PortfolioDbContext db) : IInvestmentRepository
{
    /// <summary>Somente leitura: AsNoTracking evita o custo de rastrear a lista inteira.</summary>
    public async Task<IReadOnlyList<Investment>> ListByInvestorAsync(Guid investorId, CancellationToken ct = default) =>
        await db.Investments
            .AsNoTracking()
            .Include(a => a.AssetClass)
            .Where(a => a.InvestorId == investorId)
            .OrderByDescending(a => a.InvestedOn)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// O filtro por investidor faz parte da consulta, e nao de uma checagem depois:
    /// nao existe caminho no codigo que leia um aporte de outro usuario.
    /// </summary>
    public Task<Investment?> FindAsync(Guid id, Guid investorId, CancellationToken ct = default) =>
        db.Investments
            .Include(a => a.AssetClass)
            .SingleOrDefaultAsync(a => a.Id == id && a.InvestorId == investorId, ct);

    /// <summary>O agrupamento roda no banco (GROUP BY), nao em memoria.</summary>
    public async Task<IReadOnlyList<AssetClassTotal>> TotalsByAssetClassAsync(Guid investorId, CancellationToken ct = default) =>
        await db.Investments
            .AsNoTracking()
            .Where(a => a.InvestorId == investorId)
            .GroupBy(a => new { a.AssetClassId, a.AssetClass!.Name })
            .Select(g => new AssetClassTotal(
                g.Key.AssetClassId,
                g.Key.Name,
                g.Count(),
                g.Sum(a => a.Amount)))
            .ToListAsync(ct);

    public void Add(Investment investment) => db.Investments.Add(investment);

    public void Remove(Investment investment) => db.Investments.Remove(investment);
}
