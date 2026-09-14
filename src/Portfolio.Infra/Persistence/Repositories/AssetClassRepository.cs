using Microsoft.EntityFrameworkCore;
using Portfolio.Core.AssetClasses;

namespace Portfolio.Infra.Persistence.Repositories;

public class AssetClassRepository(PortfolioDbContext db) : IAssetClassRepository
{
    public async Task<IReadOnlyList<AssetClassUsage>> ListAsync(CancellationToken ct = default) =>
        await db.AssetClasses
            .OrderBy(a => a.Name)
            .Select(a => new AssetClassUsage(a, a.Investments.Count))
            .ToListAsync(ct);

    /// <summary>
    /// Sem AsNoTracking de proposito: quem chama pode renomear ou remover a
    /// entidade, e para isso ela precisa estar sendo rastreada pelo DbContext.
    /// </summary>
    public Task<AssetClassUsage?> FindAsync(int id, CancellationToken ct = default) =>
        db.AssetClasses
            .Where(a => a.Id == id)
            .Select(a => new AssetClassUsage(a, a.Investments.Count))
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// Tambem rastreada: a instancia devolvida aqui e ligada ao aporte que esta
    /// sendo criado, e uma copia desanexada faria o EF tentar inserir a classe de novo.
    /// </summary>
    public Task<AssetClass?> FindBySlugAsync(string slug, CancellationToken ct = default) =>
        db.AssetClasses.SingleOrDefaultAsync(a => a.Slug == slug, ct);

    public Task<bool> SlugTakenAsync(string slug, int? ignoreId = null, CancellationToken ct = default) =>
        db.AssetClasses.AnyAsync(a => a.Slug == slug && (ignoreId == null || a.Id != ignoreId), ct);

    public void Add(AssetClass assetClass) => db.AssetClasses.Add(assetClass);

    public void Remove(AssetClass assetClass) => db.AssetClasses.Remove(assetClass);
}
