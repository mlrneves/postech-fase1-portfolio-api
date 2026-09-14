using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;
using Portfolio.Core.Investors;

namespace Portfolio.Core.Investments;

/// <summary>
/// Um aporte feito por um investidor. O valor e a data sao validados aqui, no
/// construtor e no <see cref="Revise"/>, para que nao exista instancia invalida
/// circulando pela aplicacao.
/// </summary>
public class Investment
{
    public const int MaxInstitutionLength = 80;
    public const int MaxNotesLength = 240;

    /// <summary>Teto da coluna decimal(18,2); sem isso um valor absurdo estouraria no banco.</summary>
    public const decimal MaxAmount = 9_999_999_999_999.99m;

    public Guid Id { get; private set; }
    public Guid InvestorId { get; private set; }
    public int AssetClassId { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>Data do aporte. E <c>DateOnly</c> porque hora e fuso nao dizem nada aqui.</summary>
    public DateOnly InvestedOn { get; private set; }

    public string? Institution { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ChangedAt { get; private set; }

    public Investor? Investor { get; private set; }
    public AssetClass? AssetClass { get; private set; }

    /// <summary>Construtor sem parametros exigido pelo EF Core para materializar a entidade.</summary>
    private Investment() { }

    public Investment(
        Guid investorId,
        AssetClass assetClass,
        decimal amount,
        DateOnly investedOn,
        string? institution,
        string? notes)
    {
        if (investorId == Guid.Empty)
            throw new BusinessRuleException("O aporte precisa estar vinculado a um investidor.");

        Id = Guid.NewGuid();
        InvestorId = investorId;
        CreatedAt = DateTime.UtcNow;
        Apply(assetClass, amount, investedOn, institution, notes);
    }

    public void Revise(
        AssetClass assetClass,
        decimal amount,
        DateOnly investedOn,
        string? institution,
        string? notes)
    {
        Apply(assetClass, amount, investedOn, institution, notes);
        ChangedAt = DateTime.UtcNow;
    }

    private void Apply(
        AssetClass assetClass,
        decimal amount,
        DateOnly investedOn,
        string? institution,
        string? notes)
    {
        ArgumentNullException.ThrowIfNull(assetClass);

        if (amount <= 0)
            throw new BusinessRuleException("O valor do aporte precisa ser maior que zero.");

        if (amount > MaxAmount)
            throw new BusinessRuleException($"O valor do aporte nao pode passar de {MaxAmount:N2}.");

        if (investedOn == default)
            throw new BusinessRuleException("Informe a data do aporte.");

        if (investedOn > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessRuleException("A data do aporte nao pode estar no futuro.");

        if (institution is { Length: > MaxInstitutionLength })
            throw new BusinessRuleException($"A instituicao nao pode passar de {MaxInstitutionLength} caracteres.");

        if (notes is { Length: > MaxNotesLength })
            throw new BusinessRuleException($"A observacao nao pode passar de {MaxNotesLength} caracteres.");

        // A entidade inteira e atribuida (e nao so o id) para que a resposta do
        // POST/PUT ja saia com o nome da classe de ativo, sem uma segunda consulta.
        AssetClass = assetClass;
        AssetClassId = assetClass.Id;
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        InvestedOn = investedOn;
        Institution = Blank(institution);
        Notes = Blank(notes);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
