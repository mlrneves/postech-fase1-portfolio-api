using System.ComponentModel.DataAnnotations;
using Portfolio.Core.AssetClasses;

namespace Portfolio.UseCases.AssetClasses;

/// <summary>Corpo de <c>POST /asset-classes</c> e <c>PUT /asset-classes/{id}</c>.</summary>
public record AssetClassRequest(
    [Required(ErrorMessage = "Informe o nome da classe de ativo.")]
    [MaxLength(AssetClass.MaxNameLength)]
    string Name);

/// <summary>
/// Classe de ativo nas respostas. <c>investmentCount</c> vai junto porque e ele
/// que explica por que um DELETE pode voltar 409.
/// </summary>
public record AssetClassView(
    int Id,
    string Name,
    string Slug,
    int InvestmentCount,
    DateTime CreatedAt,
    DateTime? ChangedAt)
{
    public static AssetClassView From(AssetClassUsage usage) =>
        new(usage.AssetClass.Id,
            usage.AssetClass.Name,
            usage.AssetClass.Slug,
            usage.InvestmentCount,
            usage.AssetClass.CreatedAt,
            usage.AssetClass.ChangedAt);
}
