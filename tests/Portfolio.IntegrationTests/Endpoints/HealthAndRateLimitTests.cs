using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Portfolio.IntegrationTests.Support;
using Portfolio.UseCases.Accounts;

namespace Portfolio.IntegrationTests.Endpoints;

/// <summary>Sonda de saude e limite de tentativas nos endpoints de credencial.</summary>
public class HealthAndRateLimitTests
{
    [Fact]
    public async Task Health_responde_sem_token_e_reporta_o_banco()
    {
        using var factory = new PortfolioApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        report.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        report.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .Should().Contain("database");
    }

    [Fact]
    public async Task Login_repetido_passa_a_responder_429_com_Retry_After()
    {
        const int limit = 4;
        using var factory = new PortfolioApiFactory { CredentialPermitLimit = limit };
        var client = factory.CreateClient();

        var email = $"forca-bruta-{Guid.NewGuid():N}@exemplo.com";
        var tentativa = new SignInRequest(email, "qualquer-senha");

        // Dentro do limite, a resposta continua sendo a de credencial invalida.
        for (var i = 0; i < limit; i++)
        {
            var permitida = await client.PostAsJsonAsync("/auth", tentativa, ApiClient.Json);
            permitida.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var bloqueada = await client.PostAsJsonAsync("/auth", tentativa, ApiClient.Json);

        bloqueada.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        bloqueada.Headers.RetryAfter.Should().NotBeNull();
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("Tentativas demais");
    }

    [Fact]
    public async Task Limite_nao_alcanca_os_endpoints_protegidos()
    {
        using var factory = new PortfolioApiFactory { CredentialPermitLimit = 2 };
        var (client, _) = await factory.SignedInAsync();

        // O cadastro e o login ja consumiram a cota; listar aportes nao e endpoint
        // de credencial e precisa seguir respondendo normalmente.
        for (var i = 0; i < 5; i++)
            (await client.GetAsync("/investments")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
