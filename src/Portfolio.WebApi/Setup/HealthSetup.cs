using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Portfolio.Infra.Persistence;

namespace Portfolio.WebApi.Setup;

public static class HealthSetup
{
    /// <summary>Nome da verificacao do banco, usado tambem no corpo da resposta.</summary>
    private const string DatabaseCheck = "database";

    public static IServiceCollection AddHealthProbes(this IServiceCollection services)
    {
        services.AddHealthChecks()
            // Nao basta o processo responder: se o banco caiu, a API nao serve
            // para nada e o orquestrador precisa saber disso.
            .AddDbContextCheck<PortfolioDbContext>(DatabaseCheck);

        return services;
    }

    /// <summary>
    /// Publica /health sem autenticacao: o probe do orquestrador (App Service,
    /// Container Apps, Docker) nao tem token, e a resposta nao expoe nada sensivel.
    /// </summary>
    public static WebApplication MapHealthProbes(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteReportAsync,
            AllowCachingResponses = false
        }).AllowAnonymous();

        return app;
    }

    private static async Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = (int)entry.Value.Duration.TotalMilliseconds,

                // A excecao original fica no log; o corpo so diz o que falhou.
                description = entry.Value.Status == HealthStatus.Healthy
                    ? null
                    : entry.Value.Description ?? "Verificacao falhou."
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }
}
