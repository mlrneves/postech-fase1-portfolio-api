using System.ComponentModel.DataAnnotations;
using Portfolio.Core.Investments;

namespace Portfolio.UseCases.Investments;

/// <summary>Corpo de <c>POST /investments</c> e <c>PUT /investments/{id}</c>.</summary>
public record InvestmentRequest(
    // Aceita o nome ou o slug da classe de ativo: "Ações", "ACOES" e "acoes"
    // apontam para o mesmo registro.
    [Required(ErrorMessage = "Informe a classe de ativo.")]
    string AssetClass,

    [Required(ErrorMessage = "Informe o valor do aporte.")]
    [Range(0.01, 9_999_999_999_999.99, ErrorMessage = "O valor do aporte precisa ser maior que zero.")]
    decimal Amount,

    // Anulavel de proposito: sem isso o binder colocaria 0001-01-01 quando o campo
    // viesse ausente, e a resposta seria "data invalida" em vez de "campo obrigatorio".
    [Required(ErrorMessage = "Informe a data do aporte.")]
    DateOnly? InvestedOn,

    [MaxLength(Investment.MaxInstitutionLength)]
    string? Institution,

    [MaxLength(Investment.MaxNotesLength)]
    string? Notes);

/// <summary>Classe de ativo resumida dentro da resposta de um aporte.</summary>
public record AssetClassRef(int Id, string Name, string Slug);

/// <summary>Aporte como ele aparece nas respostas.</summary>
public record InvestmentView(
    Guid Id,
    AssetClassRef AssetClass,
    decimal Amount,
    DateOnly InvestedOn,
    string? Institution,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ChangedAt);

/// <summary>Uma linha do resumo da carteira.</summary>
public record PortfolioSlice(string AssetClass, int Count, decimal Total, decimal Share);

/// <summary>Resposta de <c>GET /investments/summary</c>.</summary>
public record PortfolioSummary(decimal TotalInvested, int Count, IReadOnlyList<PortfolioSlice> ByAssetClass);
