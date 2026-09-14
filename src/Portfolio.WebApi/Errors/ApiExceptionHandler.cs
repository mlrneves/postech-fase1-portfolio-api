using Microsoft.AspNetCore.Diagnostics;
using Portfolio.Core.Abstractions;
using Portfolio.UseCases.Errors;

namespace Portfolio.WebApi.Errors;

/// <summary>
/// Traduz as excecoes das camadas internas em respostas ProblemDetails (RFC 9457).
/// E a implementacao de <see cref="IExceptionHandler"/> do ASP.NET Core 8, que
/// substitui o middleware de try/catch escrito a mao e reaproveita o mesmo
/// formatador usado pelos erros de validacao do model binding.
/// </summary>
internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Status nao oficial, mas consagrado pelo nginx: o cliente desistiu antes da resposta.</summary>
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (status, title) = Classify(exception);

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Falha nao tratada em {Method} {Path}.",
                context.Request.Method, context.Request.Path.Value);
        else
            logger.LogWarning("{Status} em {Method} {Path}: {Message}",
                status, context.Request.Method, context.Request.Path.Value, exception.Message);

        context.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new()
            {
                Status = status,
                Title = title,

                // Detalhe de erro interno nao vai para o cliente: a mensagem original
                // pode expor nome de tabela, de constraint ou caminho de arquivo.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Nao foi possivel concluir a operacao. Tente novamente."
                    : exception.Message
            }
        });
    }

    private static (int Status, string Title) Classify(Exception exception) => exception switch
    {
        BusinessRuleException => (StatusCodes.Status400BadRequest, "Requisicao invalida"),
        ResourceNotFoundException => (StatusCodes.Status404NotFound, "Recurso nao encontrado"),
        DuplicateResourceException => (StatusCodes.Status409Conflict, "Conflito"),
        InvalidCredentialsException or UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Nao autenticado"),
        OperationCanceledException => (ClientClosedRequest, "Requisicao cancelada"),
        _ => (StatusCodes.Status500InternalServerError, "Erro interno")
    };
}
