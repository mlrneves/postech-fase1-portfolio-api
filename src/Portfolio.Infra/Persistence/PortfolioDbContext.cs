using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;
using Portfolio.Core.Investments;
using Portfolio.Core.Investors;
using Portfolio.UseCases.Errors;

namespace Portfolio.Infra.Persistence;

/// <summary>
/// O proprio DbContext faz o papel de <see cref="IUnitOfWork"/>: ele ja e a unidade
/// de trabalho do EF Core, e um wrapper em volta so acrescentaria indirecao.
/// </summary>
public class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options)
    : DbContext(options), IUnitOfWork
{
    // Codigos de erro do SQL Server para violacao de indice e de constraint unica.
    private const int DuplicateKeyRow = 2601;
    private const int DuplicateKeyConstraint = 2627;

    public DbSet<Investor> Investors => Set<Investor>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<AssetClass> AssetClasses => Set<AssetClass>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Varre o assembly e aplica todas as classes *Map (IEntityTypeConfiguration),
        // assim incluir uma entidade nova nao exige mexer aqui.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortfolioDbContext).Assembly);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // Duas requisicoes concorrentes com o mesmo e-mail passam pela checagem
            // previa e colidem so no indice unico. Sem este catch o cliente veria 500.
            throw new DuplicateResourceException("Ja existe um registro com estes dados.");
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is SqlException sql &&
        sql.Number is DuplicateKeyRow or DuplicateKeyConstraint;
}
