using Microsoft.EntityFrameworkCore;
using Portfolio.Infra.Persistence;

namespace Portfolio.WebApi.Setup;

public static class DatabaseSetup
{
    /// <summary>
    /// Aplica as migrations pendentes na subida quando <c>Database:AutoMigrate</c>
    /// esta ligado. Pratico para a demonstracao e para o container; num ambiente
    /// real a migration seria um passo separado do deploy.
    /// </summary>
    public static async Task ApplyPendingMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:AutoMigrate"))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();

        // O provedor InMemory usado nos testes nao tem migrations.
        if (!db.Database.IsRelational())
            return;

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Banco de dados ja esta atualizado.");
            return;
        }

        logger.LogInformation("Aplicando {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
    }
}
