using System.Net;
using System.Net.Http.Json;
using System.Text;
using Portfolio.IntegrationTests.Support;
using Portfolio.UseCases.Investments;

namespace Portfolio.IntegrationTests.Endpoints;

/// <summary>Cobre o CRUD de /investments, o resumo e o isolamento entre investidores.</summary>
public class InvestmentEndpointsTests(PortfolioApiFactory factory) : IClassFixture<PortfolioApiFactory>
{
    private static DateOnly Ontem => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    private static InvestmentRequest Aporte(
        string assetClass = "Ações",
        decimal amount = 1_000m,
        string? institution = "XP",
        string? notes = "PETR4") =>
        new(assetClass, amount, Ontem, institution, notes);

    [Fact]
    public async Task Endpoints_protegidos_sem_token_devem_devolver_401()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/investments")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/investments/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/investments/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/investments", Aporte(), ApiClient.Json)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync($"/investments/{Guid.NewGuid()}", Aporte(), ApiClient.Json)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync($"/investments/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ciclo_completo_de_vida_de_um_aporte()
    {
        var (client, _) = await factory.SignedInAsync();

        // A carteira comeca vazia.
        (await (await client.GetAsync("/investments")).ReadAsync<List<InvestmentView>>())
            .Should().BeEmpty();

        var create = await client.PostAsJsonAsync("/investments", Aporte(), ApiClient.Json);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.ReadAsync<InvestmentView>();
        created.Amount.Should().Be(1_000m);
        created.AssetClass.Slug.Should().Be("acoes");
        created.InvestedOn.Should().Be(Ontem);
        created.Institution.Should().Be("XP");
        created.ChangedAt.Should().BeNull();

        // O Location do 201 precisa apontar para um recurso que existe de fato.
        create.Headers.Location.Should().NotBeNull();
        (await client.GetAsync(create.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await client.GetAsync("/investments")).ReadAsync<List<InvestmentView>>();
        list.Should().ContainSingle(i => i.Id == created.Id);

        var update = await client.PutAsJsonAsync($"/investments/{created.Id}",
            Aporte("Renda Fixa", 2_500m, "Nubank", "CDB 110% CDI"), ApiClient.Json);
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await update.ReadAsync<InvestmentView>();
        updated.AssetClass.Name.Should().Be("Renda Fixa");
        updated.Amount.Should().Be(2_500m);
        updated.Notes.Should().Be("CDB 110% CDI");
        updated.ChangedAt.Should().NotBeNull();

        // O segundo DELETE ja nao acha mais nada.
        (await client.DeleteAsync($"/investments/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/investments/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/investments/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Um_investidor_nao_deve_alcancar_o_aporte_de_outro()
    {
        var (ana, _) = await factory.SignedInAsync("Ana");
        var (bruno, _) = await factory.SignedInAsync("Bruno");

        var aporteDaAna = await (await ana.PostAsJsonAsync("/investments", Aporte(), ApiClient.Json))
            .ReadAsync<InvestmentView>();

        // Bruno nao ve na listagem...
        (await (await bruno.GetAsync("/investments")).ReadAsync<List<InvestmentView>>())
            .Should().NotContain(i => i.Id == aporteDaAna.Id);

        // ...e recebe 404, nao 403: um 403 confirmaria que o id existe.
        (await bruno.GetAsync($"/investments/{aporteDaAna.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await bruno.PutAsJsonAsync($"/investments/{aporteDaAna.Id}", Aporte(), ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await bruno.DeleteAsync($"/investments/{aporteDaAna.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // E o aporte da Ana continua intacto.
        (await ana.GetAsync($"/investments/{aporteDaAna.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Aporte_sem_a_data_deve_devolver_400()
    {
        var (client, _) = await factory.SignedInAsync();

        var response = await client.PostAsync("/investments", new StringContent(
            """{"assetClass":"Ações","amount":100}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Aporte_com_valor_invalido_deve_devolver_400(decimal amount)
    {
        var (client, _) = await factory.SignedInAsync();

        (await client.PostAsJsonAsync("/investments", Aporte(amount: amount), ApiClient.Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Aporte_com_data_futura_deve_devolver_400()
    {
        var (client, _) = await factory.SignedInAsync();
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var response = await client.PostAsJsonAsync("/investments",
            new InvestmentRequest("Ações", 100m, amanha, null, null), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("futuro");
    }

    [Fact]
    public async Task Aporte_com_campo_desconhecido_no_corpo_deve_devolver_400()
    {
        var (client, _) = await factory.SignedInAsync();

        // "amout" em vez de "amount": melhor recusar do que gravar o aporte zerado.
        var response = await client.PostAsync("/investments", new StringContent(
            """{"assetClass":"Ações","amout":100,"investedOn":"2026-01-10"}""",
            Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Classe_de_ativo_inexistente_deve_devolver_400_listando_as_disponiveis()
    {
        var (client, _) = await factory.SignedInAsync();

        var response = await client.PostAsJsonAsync("/investments", Aporte("Previdencia"), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Previdencia").And.Contain("Ações");
    }

    [Theory]
    [InlineData("Ações")]
    [InlineData("ACOES")]
    [InlineData("acoes")]
    public async Task Classe_de_ativo_deve_ser_aceita_com_ou_sem_acento(string informado)
    {
        var (client, _) = await factory.SignedInAsync();

        var response = await client.PostAsJsonAsync("/investments", Aporte(informado), ApiClient.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<InvestmentView>()).AssetClass.Slug.Should().Be("acoes");
    }

    [Fact]
    public async Task Summary_deve_somar_por_classe_de_ativo_apenas_do_investidor_autenticado()
    {
        var (ana, _) = await factory.SignedInAsync("Ana");
        var (bruno, _) = await factory.SignedInAsync("Bruno");

        await ana.PostAsJsonAsync("/investments", Aporte("Ações", 600m), ApiClient.Json);
        await ana.PostAsJsonAsync("/investments", Aporte("Ações", 200m), ApiClient.Json);
        await ana.PostAsJsonAsync("/investments", Aporte("Renda Fixa", 200m), ApiClient.Json);
        await bruno.PostAsJsonAsync("/investments", Aporte("Criptomoedas", 9_999m), ApiClient.Json);

        var summary = await (await ana.GetAsync("/investments/summary")).ReadAsync<PortfolioSummary>();

        summary.TotalInvested.Should().Be(1_000m);
        summary.Count.Should().Be(3);
        summary.ByAssetClass.Should().HaveCount(2);

        var acoes = summary.ByAssetClass.Single(s => s.AssetClass == "Ações");
        acoes.Count.Should().Be(2);
        acoes.Total.Should().Be(800m);
        acoes.Share.Should().Be(80m);

        summary.ByAssetClass.Should().NotContain(s => s.AssetClass == "Criptomoedas");
    }

    [Fact]
    public async Task Summary_de_carteira_vazia_deve_voltar_zerado()
    {
        var (client, _) = await factory.SignedInAsync();

        var summary = await (await client.GetAsync("/investments/summary")).ReadAsync<PortfolioSummary>();

        summary.TotalInvested.Should().Be(0m);
        summary.Count.Should().Be(0);
        summary.ByAssetClass.Should().BeEmpty();
    }

    [Fact]
    public async Task Rota_summary_nao_deve_ser_confundida_com_um_id()
    {
        var (client, _) = await factory.SignedInAsync();

        // "summary" nao casa com a restricao :guid, entao cai no endpoint certo.
        (await client.GetAsync("/investments/summary")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/investments/nao-e-guid")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
