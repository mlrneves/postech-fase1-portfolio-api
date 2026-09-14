using Microsoft.Extensions.DependencyInjection;
using Portfolio.UseCases.Abstractions;
using Portfolio.UseCases.Accounts;
using Portfolio.UseCases.AssetClasses;
using Portfolio.UseCases.Investments;
using Portfolio.UseCases.Security;

namespace Portfolio.UseCases;

/// <summary>
/// Registro dos casos de uso. Cada camada expoe o proprio metodo de extensao para
/// que o Program.cs nao precise conhecer as implementacoes uma por uma.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IInvestmentService, InvestmentService>();
        services.AddScoped<IAssetClassService, AssetClassService>();

        // Sem estado e sem dependencias: uma instancia atende a aplicacao inteira.
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}
