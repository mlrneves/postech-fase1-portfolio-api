namespace Portfolio.Core.AssetClasses;

/// <summary>
/// Classe de ativo junto com quantos investimentos a referenciam. O contador vem
/// do banco na mesma consulta porque e ele que decide se a exclusao e permitida.
/// </summary>
public record AssetClassUsage(AssetClass AssetClass, int InvestmentCount);
