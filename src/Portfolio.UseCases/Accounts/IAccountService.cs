namespace Portfolio.UseCases.Accounts;

public interface IAccountService
{
    Task<InvestorView> SignUpAsync(SignUpRequest request, CancellationToken ct = default);

    Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct = default);

    Task<InvestorView> GetAsync(Guid investorId, CancellationToken ct = default);
}
