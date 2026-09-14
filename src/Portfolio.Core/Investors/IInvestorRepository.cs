namespace Portfolio.Core.Investors;

public interface IInvestorRepository
{
    Task<Investor?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<Investor?> FindAsync(Guid id, CancellationToken ct = default);

    Task<bool> EmailTakenAsync(string email, CancellationToken ct = default);

    void Add(Investor investor);
}
