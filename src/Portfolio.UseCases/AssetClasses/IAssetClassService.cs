namespace Portfolio.UseCases.AssetClasses;

public interface IAssetClassService
{
    Task<IReadOnlyList<AssetClassView>> ListAsync(CancellationToken ct = default);

    Task<AssetClassView> GetAsync(int id, CancellationToken ct = default);

    Task<AssetClassView> CreateAsync(AssetClassRequest request, CancellationToken ct = default);

    Task<AssetClassView> RenameAsync(int id, AssetClassRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
