using Portfolio.Core.Abstractions;
using Portfolio.Core.Investments;

namespace Portfolio.Core.AssetClasses;

/// <summary>
/// Classe de ativo ("Ações", "Renda Fixa", "Tesouro Direto"...). Vive em tabela
/// propria, e nao em um enum, para que a lista possa crescer em producao sem
/// recompilar e sem migration.
/// </summary>
public class AssetClass
{
    public const int MaxNameLength = 60;

    public int Id { get; private set; }

    /// <summary>Nome como o usuario digitou, acentos inclusos. E o que aparece na resposta.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Forma canonica do nome. E por aqui que a busca e a checagem de duplicidade acontecem.</summary>
    public string Slug { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }
    public DateTime? ChangedAt { get; private set; }

    private readonly List<Investment> _investments = [];
    public IReadOnlyCollection<Investment> Investments => _investments;

    /// <summary>Construtor sem parametros exigido pelo EF Core para materializar a entidade.</summary>
    private AssetClass() { }

    public AssetClass(string name)
    {
        Apply(name);
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        Apply(name);
        ChangedAt = DateTime.UtcNow;
    }

    private void Apply(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Informe o nome da classe de ativo.");

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new BusinessRuleException($"O nome da classe de ativo nao pode passar de {MaxNameLength} caracteres.");

        var slug = Slugger.From(trimmed);
        if (slug.Length == 0)
            throw new BusinessRuleException("O nome da classe de ativo precisa ter letras ou numeros.");

        Name = trimmed;
        Slug = slug;
    }
}
