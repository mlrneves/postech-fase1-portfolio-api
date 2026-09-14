using System.Net;
using System.Net.Http.Json;
using Portfolio.IntegrationTests.Support;
using Portfolio.UseCases.AssetClasses;
using Portfolio.UseCases.Investments;

namespace Portfolio.IntegrationTests.Endpoints;

/// <summary>Cobre a manutencao de /asset-classes e a protecao contra remover uma classe em uso.</summary>
public class AssetClassEndpointsTests(PortfolioApiFactory factory) : IClassFixture<PortfolioApiFactory>
{
    private static DateOnly Ontem => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    /// <summary>Nome unico por teste: a lista de classes de ativo e compartilhada entre os investidores.</summary>
    private static string NomeUnico(string prefixo) => $"{prefixo} {Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task Endpoints_sem_token_devem_devolver_401()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/asset-classes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/asset-classes/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/asset-classes", new AssetClassRequest("Nova"), ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync("/asset-classes/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deve_listar_as_cinco_classes_da_carga_inicial()
    {
        var (client, _) = await factory.SignedInAsync();

        var classes = await (await client.GetAsync("/asset-classes")).ReadAsync<List<AssetClassView>>();

        classes.Select(c => c.Name).Should().Contain(
            ["Ações", "Renda Fixa", "Fundos Imobiliários", "Tesouro Direto", "Criptomoedas"]);

        classes.Single(c => c.Name == "Ações").Slug.Should().Be("acoes");
    }

    [Fact]
    public async Task Ciclo_completo_de_vida_de_uma_classe_de_ativo()
    {
        var (client, _) = await factory.SignedInAsync();
        var nome = NomeUnico("Previdencia");

        var create = await client.PostAsJsonAsync("/asset-classes", new AssetClassRequest(nome), ApiClient.Json);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.ReadAsync<AssetClassView>();
        created.Id.Should().BeGreaterThan(0);
        created.InvestmentCount.Should().Be(0);

        (await client.GetAsync(create.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.OK);

        var novoNome = NomeUnico("PrevidenciaPrivada");
        var rename = await client.PutAsJsonAsync($"/asset-classes/{created.Id}",
            new AssetClassRequest(novoNome), ApiClient.Json);
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rename.ReadAsync<AssetClassView>()).ChangedAt.Should().NotBeNull();

        (await client.DeleteAsync($"/asset-classes/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/asset-classes/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Classe_recem_criada_deve_poder_ser_usada_num_aporte_na_hora()
    {
        var (client, _) = await factory.SignedInAsync();
        var nome = NomeUnico("Debentures");

        var created = await (await client.PostAsJsonAsync("/asset-classes",
            new AssetClassRequest(nome), ApiClient.Json)).ReadAsync<AssetClassView>();

        var aporte = await client.PostAsJsonAsync("/investments",
            new InvestmentRequest(nome, 500m, Ontem, "BTG", null), ApiClient.Json);

        aporte.StatusCode.Should().Be(HttpStatusCode.Created);
        (await aporte.ReadAsync<InvestmentView>()).AssetClass.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Nao_deve_remover_classe_que_tenha_aporte_vinculado()
    {
        var (client, _) = await factory.SignedInAsync();
        var nome = NomeUnico("EmUso");

        var created = await (await client.PostAsJsonAsync("/asset-classes",
            new AssetClassRequest(nome), ApiClient.Json)).ReadAsync<AssetClassView>();

        var aporte = await (await client.PostAsJsonAsync("/investments",
            new InvestmentRequest(nome, 500m, Ontem, null, null), ApiClient.Json)).ReadAsync<InvestmentView>();

        var bloqueado = await client.DeleteAsync($"/asset-classes/{created.Id}");
        bloqueado.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await bloqueado.Content.ReadAsStringAsync()).Should().Contain("1");

        // A contagem que explica o 409 tambem aparece na consulta da classe.
        (await (await client.GetAsync($"/asset-classes/{created.Id}")).ReadAsync<AssetClassView>())
            .InvestmentCount.Should().Be(1);

        // Removido o aporte, a classe libera.
        (await client.DeleteAsync($"/investments/{aporte.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/asset-classes/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Nome_equivalente_ignorando_acento_deve_devolver_409()
    {
        var (client, _) = await factory.SignedInAsync();

        var duplicada = await client.PostAsJsonAsync("/asset-classes",
            new AssetClassRequest("ACOES"), ApiClient.Json);

        duplicada.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public async Task Nome_invalido_deve_devolver_400(string nome)
    {
        var (client, _) = await factory.SignedInAsync();

        (await client.PostAsJsonAsync("/asset-classes", new AssetClassRequest(nome), ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Classe_inexistente_deve_devolver_404()
    {
        var (client, _) = await factory.SignedInAsync();

        (await client.GetAsync("/asset-classes/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync("/asset-classes/987654")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
