using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Investors;

namespace Portfolio.Infra.Persistence.Repositories;

public class InvestorRepository(PortfolioDbContext db) : IInvestorRepository
{
    public Task<Investor?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        db.Investors.SingleOrDefaultAsync(i => i.Email == email, ct);

    public Task<Investor?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.Investors.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> EmailTakenAsync(string email, CancellationToken ct = default) =>
        db.Investors.AnyAsync(i => i.Email == email, ct);

    public void Add(Investor investor) => db.Investors.Add(investor);
}
