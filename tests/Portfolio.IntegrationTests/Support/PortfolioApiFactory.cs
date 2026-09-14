using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Portfolio.Infra.Persistence;
using Portfolio.WebApi;

namespace Portfolio.IntegrationTests.Support;

/// <summary>
/// Sobe a API de verdade (pipeline, autenticacao, controllers e serializacao) e
/// troca somente o provedor do EF Core pelo InMemory, para que a suite rode sem
/// SQL Server instalado. Cada instancia da fabrica usa um banco proprio, entao
/// as classes de teste nao interferem uma na outra.
/// </summary>
public class PortfolioApiFactory : WebApplicationFactory<ApiMarker>
{
    private readonly string _databaseName = $"portfolio-tests-{Guid.NewGuid():N}";

    /// <summary>
    /// Limite de tentativas nos endpoints de credencial. O padrao e alto de
    /// proposito: sob o TestServer nao existe IP remoto, entao a suite inteira cai
    /// numa unica particao e o limite de producao reprovaria testes sem relacao
    /// com ele. O teste do proprio limite baixa este valor.
    /// </summary>
    public int CredentialPermitLimit { get; init; } = 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // O InMemory nao tem migrations; o esquema vem do EnsureCreated mais abaixo.
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("RateLimiting:PermitLimit", CredentialPermitLimit.ToString());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<PortfolioDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<PortfolioDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                // O InMemory nao consegue avaliar tudo no "banco"; o aviso viraria
                // excecao e esconderia o motivo real de uma falha de teste.
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                    .InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // EnsureCreated e o que aplica o HasData: sem ele as cinco classes de ativo
        // da carga inicial nao existiriam e quase todo teste falharia com 400.
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.EnsureCreated();

        return host;
    }
}
