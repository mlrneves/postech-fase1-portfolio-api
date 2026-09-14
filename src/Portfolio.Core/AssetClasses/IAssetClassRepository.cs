namespace Portfolio.Core.AssetClasses;

public interface IAssetClassRepository
{
    Task<IReadOnlyList<AssetClassUsage>> ListAsync(CancellationToken ct = default);

    Task<AssetClassUsage?> FindAsync(int id, CancellationToken ct = default);

    /// <summary>Busca pela forma canonica do nome, tolerando acento e caixa.</summary>
    Task<AssetClass?> FindBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> SlugTakenAsync(string slug, int? ignoreId = null, CancellationToken ct = default);

    void Add(AssetClass assetClass);

    void Remove(AssetClass assetClass);
}
