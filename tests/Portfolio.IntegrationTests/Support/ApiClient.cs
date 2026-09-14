using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Portfolio.UseCases.Accounts;

namespace Portfolio.IntegrationTests.Support;

/// <summary>
/// Atalhos que todo teste de endpoint precisa: as mesmas opcoes de JSON usadas
/// pela API e um cliente que ja passou por cadastro e login.
/// </summary>
internal static class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Cadastra um investidor com e-mail unico, autentica e devolve o cliente com o Bearer pronto.</summary>
    public static async Task<(HttpClient Client, InvestorView Investor)> SignedInAsync(
        this PortfolioApiFactory factory,
        string name = "Investidor de Teste")
    {
        var client = factory.CreateClient();
        var email = $"teste-{Guid.NewGuid():N}@exemplo.com";
        const string password = "Senha@12345";

        var signUp = await client.PostAsJsonAsync("/users", new SignUpRequest(name, email, password), Json);
        signUp.EnsureSuccessStatusCode();

        var signIn = await client.PostAsJsonAsync("/auth", new SignInRequest(email, password), Json);
        signIn.EnsureSuccessStatusCode();

        var session = (await signIn.Content.ReadFromJsonAsync<SignInResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return (client, session.Investor);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
