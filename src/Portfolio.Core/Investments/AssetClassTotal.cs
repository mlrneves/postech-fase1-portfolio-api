namespace Portfolio.Core.Investments;

/// <summary>Total agrupado por classe de ativo, calculado pelo banco.</summary>
public record AssetClassTotal(int AssetClassId, string AssetClassName, int Count, decimal Total);