using System.ComponentModel.DataAnnotations;
using Portfolio.Core.Investors;

namespace Portfolio.UseCases.Accounts;

/// <summary>Corpo de <c>POST /users</c>.</summary>
public record SignUpRequest(
    [Required(ErrorMessage = "Informe o nome.")]
    [MaxLength(Investor.MaxNameLength)]
    string FullName,

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    [MaxLength(Investor.MaxEmailLength)]
    string Email,

    // O limite de 72 nao e arbitrario: o BCrypt ignora tudo o que passa de 72 bytes,
    // entao aceitar mais daria a falsa impressao de uma senha mais forte.
    [Required(ErrorMessage = "Informe a senha.")]
    [MinLength(8, ErrorMessage = "A senha precisa ter ao menos 8 caracteres.")]
    [MaxLength(72, ErrorMessage = "A senha nao pode passar de 72 caracteres.")]
    string Password);

/// <summary>Corpo de <c>POST /auth</c>.</summary>
public record SignInRequest(
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    string Email,

    [Required(ErrorMessage = "Informe a senha.")]
    string Password);

/// <summary>Investidor como ele aparece nas respostas. Nao carrega o hash da senha.</summary>
public record InvestorView(Guid Id, string FullName, string Email, DateTime RegisteredAt);

/// <summary>Resposta de <c>POST /auth</c>.</summary>
public record SignInResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    InvestorView Investor);
