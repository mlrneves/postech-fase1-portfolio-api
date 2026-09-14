using Microsoft.Extensions.Logging;
using Portfolio.Core.Abstractions;
using Portfolio.Core.Investors;
using Portfolio.UseCases.Abstractions;
using Portfolio.UseCases.Errors;

namespace Portfolio.UseCases.Accounts;

public class AccountService(
    IInvestorRepository investors,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenFactory tokenFactory,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<InvestorView> SignUpAsync(SignUpRequest request, CancellationToken ct = default)
    {
        var email = Investor.NormalizeEmail(request.Email);

        if (await investors.EmailTakenAsync(email, ct))
            throw new DuplicateResourceException($"O e-mail {email} ja esta cadastrado.");

        var investor = new Investor(request.FullName, email, passwordHasher.Hash(request.Password));

        investors.Add(investor);
        await unitOfWork.SaveAsync(ct);

        logger.LogInformation("Investidor {InvestorId} cadastrado.", investor.Id);
        return Project(investor);
    }

    public async Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct = default)
    {
        var email = Investor.NormalizeEmail(request.Email);
        var investor = await investors.FindByEmailAsync(email, ct);

        // A mesma mensagem para e-mail inexistente e senha errada: responder
        // coisas diferentes permitiria descobrir quais e-mails estao cadastrados.
        if (investor is null || !passwordHasher.Matches(request.Password, investor.PasswordHash))
        {
            logger.LogWarning("Login recusado para {Email}.", email);
            throw new InvalidCredentialsException("E-mail ou senha invalidos.");
        }

        var token = tokenFactory.IssueFor(investor);

        logger.LogInformation("Investidor {InvestorId} autenticado ate {ExpiresAtUtc:o}.", investor.Id, token.ExpiresAtUtc);
        return new SignInResponse(token.Value, "Bearer", token.ExpiresAtUtc, Project(investor));
    }

    public async Task<InvestorView> GetAsync(Guid investorId, CancellationToken ct = default)
    {
        var investor = await investors.FindAsync(investorId, ct)
            ?? throw new ResourceNotFoundException("Investidor nao encontrado.");

        return Project(investor);
    }

    private static InvestorView Project(Investor investor) =>
        new(investor.Id, investor.FullName, investor.Email, investor.RegisteredAt);
}
