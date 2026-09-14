using System.Diagnostics;

namespace Portfolio.WebApi.Middleware;

/// <summary>
/// Registra metodo, rota, status e duracao de cada requisicao. Fica na ponta de
/// fora do pipeline para que o log saia com o status final, inclusive quando o
/// tratador de excecoes tiver trocado 500 por 400/404/409.
/// </summary>
public class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    /// <summary>Acima disso a requisicao e logada como Warning para aparecer na triagem.</summary>
    private const long SlowRequestMs = 1_000;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var level = stopwatch.ElapsedMilliseconds >= SlowRequestMs
                ? LogLevel.Warning
                : LogLevel.Information;

            logger.Log(level, "{Method} {Path} respondeu {StatusCode} em {Elapsed} ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
