using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portfolio.WebApi.Setup;

public static class JsonSetup
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                var json = options.JsonSerializerOptions;

                json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                // Campos opcionais nao preenchidos saem da resposta em vez de virar null.
                json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

                // Rejeita corpo com campo desconhecido: um "amout" no lugar de "amount"
                // devolve 400 em vez de gravar o aporte com valor zero.
                json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;

                json.Converters.Add(new JsonStringEnumConverter());
            });

        // Faz os erros de validacao do model binding sairem no mesmo formato
        // ProblemDetails usado pelo ApiExceptionHandler.
        services.AddProblemDetails();

        return services;
    }
}
