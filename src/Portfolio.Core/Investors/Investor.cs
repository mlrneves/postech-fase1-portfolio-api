using System.Text.RegularExpressions;
using Portfolio.Core.Abstractions;
using Portfolio.Core.Investments;

namespace Portfolio.Core.Investors;

/// <summary>
/// Quem usa a API. A entidade recebe a senha ja transformada em hash: a senha em
/// texto puro nunca entra no dominio, logo nao ha como vazar num log de entidade.
/// </summary>
public partial class Investor
{
    public const int MaxNameLength = 120;
    public const int MaxEmailLength = 160;

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime RegisteredAt { get; private set; }

    private readonly List<Investment> _investments = [];
    public IReadOnlyCollection<Investment> Investments => _investments;

    /// <summary>Construtor sem parametros exigido pelo EF Core para materializar a entidade.</summary>
    private Investor() { }

    public Investor(string fullName, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleException("Informe o nome do investidor.");

        if (fullName.Trim().Length > MaxNameLength)
            throw new BusinessRuleException($"O nome nao pode passar de {MaxNameLength} caracteres.");

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length > MaxEmailLength)
            throw new BusinessRuleException($"O e-mail nao pode passar de {MaxEmailLength} caracteres.");

        if (!EmailShape().IsMatch(normalizedEmail))
            throw new BusinessRuleException("O e-mail informado nao parece valido.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new BusinessRuleException("O investidor precisa de um hash de senha.");

        Id = Guid.NewGuid();
        FullName = fullName.Trim();
        Email = normalizedEmail;
        PasswordHash = passwordHash;
        RegisteredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// O e-mail e a chave de login, entao precisa da mesma forma na gravacao e na
    /// busca. Normalizar num unico lugar evita cadastro duplicado por causa de caixa.
    /// </summary>
    public static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();
}
