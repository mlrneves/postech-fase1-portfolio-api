using Portfolio.Infra;
using Portfolio.UseCases;
using Portfolio.WebApi.Errors;
using Portfolio.WebApi.Middleware;
using Portfolio.WebApi.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(console =>
{
    console.SingleLine = true;
    console.TimestampFormat = "HH:mm:ss ";
});

// Cada camada registra o que e dela. O arquivo de inicializacao so escolhe as pecas.
builder.Services
    .AddPersistence(builder.Configuration)
    .AddTokenIssuing(builder.Configuration)
    .AddUseCases()
    .AddBearerAuthentication()
    .AddApiControllers()
    .AddCredentialRateLimiting(builder.Configuration)
    .AddHealthProbes()
    .AddOpenApiDocs();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

await app.ApplyPendingMigrationsAsync();

// A ordem importa. O cronometro e o mais externo, para medir tudo e registrar o
// status final; o tratador de excecoes vem logo depois, para que nenhuma excecao
// das camadas de baixo chegue crua ao cliente.
app.UseMiddleware<RequestTimingMiddleware>();
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(ui =>
{
    ui.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio API v1");
    ui.DocumentTitle = "Portfolio API teste";
});

app.UseAuthentication();
app.UseAuthorization();

// Depois do roteamento, para que cada endpoint encontre a politica que declarou.
app.UseRateLimiter();

app.MapControllers();
app.MapHealthProbes();

app.Run();
