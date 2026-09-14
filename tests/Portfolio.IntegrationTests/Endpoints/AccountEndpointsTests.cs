using System.Net;
using System.Net.Http.Json;
using Portfolio.IntegrationTests.Support;
using Portfolio.UseCases.Accounts;

namespace Portfolio.IntegrationTests.Endpoints;

/// <summary>Cobre POST /users, POST /auth e GET /users/me.</summary>
public class AccountEndpointsTests(PortfolioApiFactory factory) : IClassFixture<PortfolioApiFactory>
{
    private static string NovoEmail() => $"conta-{Guid.NewGuid():N}@exemplo.com";

    [Fact]
    public async Task Cadastro_deve_devolver_201_sem_expor_o_hash_da_senha()
    {
        var client = factory.CreateClient();
        var email = NovoEmail();

        var response = await client.PostAsJsonAsync("/users",
            new SignUpRequest("Marcio Rodrigues", email, "Senha@12345"), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Senha@12345").And.NotContain("passwordHash").And.NotContain("$2");

        var investor = await response.ReadAsync<InvestorView>();
        investor.Email.Should().Be(email);
        investor.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Cadastro_deve_normalizar_o_email_e_recusar_o_mesmo_endereco_em_outra_caixa()
    {
        var client = factory.CreateClient();
        var email = NovoEmail();

        (await client.PostAsJsonAsync("/users", new SignUpRequest("Primeiro", email, "Senha@12345"), ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicado = await client.PostAsJsonAsync("/users",
            new SignUpRequest("Segundo", email.ToUpperInvariant(), "Senha@12345"), ApiClient.Json);

        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "a@b.com", "Senha@12345")]
    [InlineData("Nome", "nao-e-email", "Senha@12345")]
    [InlineData("Nome", "a@b.com", "curta")]
    public async Task Cadastro_com_campo_invalido_deve_devolver_400(string name, string email, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/users", new SignUpRequest(name, email, password), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Auth_deve_devolver_token_com_validade_no_futuro()
    {
        var client = factory.CreateClient();
        var email = NovoEmail();
        await client.PostAsJsonAsync("/users", new SignUpRequest("Ana", email, "Senha@12345"), ApiClient.Json);

        var response = await client.PostAsJsonAsync("/auth", new SignInRequest(email, "Senha@12345"), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.ReadAsync<SignInResponse>();
        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.TokenType.Should().Be("Bearer");
        session.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        session.Investor.Email.Should().Be(email);
    }

    [Fact]
    public async Task Auth_com_senha_errada_deve_devolver_401()
    {
        var client = factory.CreateClient();
        var email = NovoEmail();
        await client.PostAsJsonAsync("/users", new SignUpRequest("Ana", email, "Senha@12345"), ApiClient.Json);

        var response = await client.PostAsJsonAsync("/auth", new SignInRequest(email, "errada"), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_de_email_inexistente_deve_devolver_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth",
            new SignInRequest(NovoEmail(), "Senha@12345"), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_me_deve_exigir_token_e_devolver_o_dono_dele()
    {
        (await factory.CreateClient().GetAsync("/users/me"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (client, investor) = await factory.SignedInAsync("Marcio");

        var response = await client.GetAsync("/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<InvestorView>()).Id.Should().Be(investor.Id);
    }

    [Fact]
    public async Task Token_falsificado_deve_devolver_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer nao.e.um.token");

        (await client.GetAsync("/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
