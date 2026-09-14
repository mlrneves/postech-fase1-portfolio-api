using Microsoft.Extensions.Logging;
using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;
using Portfolio.UseCases.Errors;

namespace Portfolio.UseCases.AssetClasses;

/// <summary>
/// Manutencao das classes de ativo. Sao dados de referencia compartilhados: todo
/// investidor autenticado ve e edita a mesma lista.
/// </summary>
public class AssetClassService(
    IAssetClassRepository assetClasses,
    IUnitOfWork unitOfWork,
    ILogger<AssetClassService> logger) : IAssetClassService
{
    public async Task<IReadOnlyList<AssetClassView>> ListAsync(CancellationToken ct = default)
    {
        var usages = await assetClasses.ListAsync(ct);
        return [.. usages.Select(AssetClassView.From)];
    }

    public async Task<AssetClassView> GetAsync(int id, CancellationToken ct = default) =>
        AssetClassView.From(await RequireAsync(id, ct));

    public async Task<AssetClassView> CreateAsync(AssetClassRequest request, CancellationToken ct = default)
    {
        var assetClass = new AssetClass(request.Name);
        await RejectIfSlugTakenAsync(assetClass.Slug, null, ct);

        assetClasses.Add(assetClass);
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Classe de ativo {AssetClassId} ({Slug}) criada.", assetClass.Id, assetClass.Slug);
        return AssetClassView.From(new AssetClassUsage(assetClass, 0));
    }

    public async Task<AssetClassView> RenameAsync(int id, AssetClassRequest request, CancellationToken ct = default)
    {
        var usage = await RequireAsync(id, ct);

        // O slug e conferido antes do Rename para nao deixar a entidade rastreada
        // suja em memoria caso o nome colida com o de outra classe de ativo.
        await RejectIfSlugTakenAsync(Slugger.From(request.Name), id, ct);

        usage.AssetClass.Rename(request.Name);
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Classe de ativo {AssetClassId} renomeada para {Name}.", id, usage.AssetClass.Name);
        return AssetClassView.From(usage);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var usage = await RequireAsync(id, ct);

        // A FK usa Restrict, entao o banco recusaria de qualquer forma. A checagem
        // aqui existe para devolver 409 com uma mensagem util em vez de um 500.
        if (usage.InvestmentCount > 0)
            throw new DuplicateResourceException(
                $"'{usage.AssetClass.Name}' esta em uso por {usage.InvestmentCount} aporte(s) e nao pode ser removida.");

        assetClasses.Remove(usage.AssetClass);
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Classe de ativo {AssetClassId} removida.", id);
    }

    private async Task<AssetClassUsage> RequireAsync(int id, CancellationToken ct) =>
        await assetClasses.FindAsync(id, ct)
        ?? throw new ResourceNotFoundException($"Classe de ativo {id} nao encontrada.");

    private async Task RejectIfSlugTakenAsync(string slug, int? ignoreId, CancellationToken ct)
    {
        if (await assetClasses.SlugTakenAsync(slug, ignoreId, ct))
            throw new DuplicateResourceException($"Ja existe uma classe de ativo equivalente a '{slug}'.");
    }
}
