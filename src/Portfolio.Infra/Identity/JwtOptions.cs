using System.ComponentModel.DataAnnotations;

namespace Portfolio.Infra.Identity;

/// <summary>
/// Secao "Jwt" do appsettings. As DataAnnotations aqui sao validadas na
/// inicializacao (ValidateOnStart), entao uma chave curta derruba a aplicacao no
/// boot em vez de gerar tokens fracos silenciosamente.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 usa chave de 256 bits; 32 caracteres e o minimo aceitavel.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:Secret precisa ter ao menos 32 caracteres.")]
    public string Secret { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int LifetimeMinutes { get; set; } = 60;
}
