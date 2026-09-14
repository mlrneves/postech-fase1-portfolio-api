using System.ComponentModel.DataAnnotations;

namespace Portfolio.WebApi.Setup;

/// <summary>
/// Secao "RateLimiting" do appsettings. Como a secao "Jwt", e validada na
/// inicializacao para que um valor absurdo derrube o boot em vez de abrir a porta.
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Nome da politica aplicada aos endpoints de credencial.</summary>
    public const string CredentialsPolicy = "credentials";

    /// <summary>Tentativas permitidas por janela, para cada origem.</summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 10;

    /// <summary>Tamanho da janela, em segundos.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}
