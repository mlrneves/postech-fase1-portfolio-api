using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Core.Abstractions;
using Portfolio.Core.AssetClasses;
using Portfolio.Core.Investments;
using Portfolio.Core.Investors;
using Portfolio.Infra.Identity;
using Portfolio.Infra.Persistence;
using Portfolio.Infra.Persistence.Repositories;
using Portfolio.UseCases.Abstractions;

namespace Portfolio.Infra;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"A connection string '{ConnectionStringName}' nao foi configurada.");

        services.AddDbContext<PortfolioDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql
                // Reconecta em falha transitoria (container de SQL Server ainda subindo, por exemplo).
                .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)
                .MigrationsHistoryTable("__MigrationsHistory")));

        // O DbContext e a unidade de trabalho: a mesma instancia atende aos dois contratos.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PortfolioDbContext>());

        services.AddScoped<IInvestorRepository, InvestorRepository>();
        services.AddScoped<IInvestmentRepository, InvestmentRepository>();
        services.AddScoped<IAssetClassRepository, AssetClassRepository>();

        return services;
    }

    public static IServiceCollection AddTokenIssuing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Falha no boot, e nao na primeira tentativa de login.
            .ValidateOnStart();

        services.AddScoped<IAccessTokenFactory, JwtAccessTokenFactory>();

        return services;
    }
}
