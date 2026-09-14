using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Portfolio.WebApi.Setup;

public static class RateLimitingSetup
{
    /// <summary>
    /// Limita as tentativas nos endpoints que recebem credenciais. Sao dois problemas
    /// no mesmo lugar: forca bruta contra a senha e, como o BCrypt e caro de proposito,
    /// exaustao de CPU — algumas centenas de tentativas paralelas derrubariam a API
    /// sem que o atacante precise acertar senha nenhuma.
    /// </summary>
    public static IServiceCollection AddCredentialRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(RateLimitOptions.CredentialsPolicy, context =>
            {
                var options = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    // Particiona por origem: o excesso de um cliente nao pode
                    // impedir os outros de autenticar.
                    partitionKey: OriginOf(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),

                        // Sem fila: em endpoint de credencial, segurar a requisicao
                        // so prolongaria o custo que estamos tentando evitar.
                        QueueLimit = 0
                    });
            });

            limiter.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting")
                    .LogWarning("Limite de tentativas atingido por {Origin} em {Path}.",
                        OriginOf(context.HttpContext), context.HttpContext.Request.Path.Value);

                // Mesmo formato ProblemDetails das demais respostas de erro.
                await context.HttpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>()
                    .TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Tentativas demais",
                            Detail = "Excesso de tentativas a partir desta origem. Aguarde e tente de novo."
                        }
                    });
            };
        });

        return services;
    }

    /// <summary>
    /// Identifica a origem da requisicao. Atras de proxy o IP real chega no
    /// X-Forwarded-For; sem isso todos os clientes contariam como um so.
    /// </summary>
    private static string OriginOf(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "desconhecida";
    }
}
