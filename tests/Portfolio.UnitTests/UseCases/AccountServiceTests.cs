using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Portfolio.Core.Abstractions;
using Portfolio.Core.Investors;
using Portfolio.UseCases.Abstractions;
using Portfolio.UseCases.Accounts;
using Portfolio.UseCases.Errors;
using Portfolio.UseCases.Security;

namespace Portfolio.UnitTests.UseCases;

public class AccountServiceTests
{
    private readonly IInvestorRepository _investors = Substitute.For<IInvestorRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAccessTokenFactory _tokens = Substitute.For<IAccessTokenFactory>();
    private readonly IPasswordHasher _hasher = new BCryptPasswordHasher();
    private readonly AccountService _service;

    private static readonly DateTime Expiry = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public AccountServiceTests()
    {
        _tokens.IssueFor(Arg.Any<Investor>()).Returns(new AccessToken("token-de-teste", Expiry));

        _service = new AccountService(
            _investors,
            _unitOfWork,
            _hasher,
            _tokens,
            NullLogger<AccountService>.Instance);
    }

    [Fact]
    public async Task SignUp_deve_gravar_o_investidor_com_a_senha_transformada_em_hash()
    {
        Investor? gravado = null;
        _investors.When(r => r.Add(Arg.Any<Investor>())).Do(call => gravado = call.Arg<Investor>());

        var view = await _service.SignUpAsync(new SignUpRequest("Ana Souza", "Ana@Teste.com", "Senha@123"));

        view.Email.Should().Be("ana@teste.com");
        view.FullName.Should().Be("Ana Souza");

        gravado.Should().NotBeNull();
        gravado!.PasswordHash.Should().NotBe("Senha@123");
        _hasher.Matches("Senha@123", gravado.PasswordHash).Should().BeTrue();

        await _unitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignUp_com_email_ja_cadastrado_deve_dar_conflito()
    {
        _investors.EmailTakenAsync("ana@teste.com", Arg.Any<CancellationToken>()).Returns(true);

        var cadastrar = () => _service.SignUpAsync(new SignUpRequest("Ana", "ANA@teste.com", "Senha@123"));

        await cadastrar.Should().ThrowAsync<DuplicateResourceException>();
        _investors.DidNotReceive().Add(Arg.Any<Investor>());
        await _unitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignIn_com_credencial_correta_deve_devolver_o_token()
    {
        var investor = new Investor("Ana", "ana@teste.com", _hasher.Hash("Senha@123"));
        _investors.FindByEmailAsync("ana@teste.com", Arg.Any<CancellationToken>()).Returns(investor);

        var response = await _service.SignInAsync(new SignInRequest("ANA@Teste.com", "Senha@123"));

        response.AccessToken.Should().Be("token-de-teste");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresAtUtc.Should().Be(Expiry);
        response.Investor.Id.Should().Be(investor.Id);
    }

    [Fact]
    public async Task SignIn_com_senha_errada_deve_recusar()
    {
        var investor = new Investor("Ana", "ana@teste.com", _hasher.Hash("Senha@123"));
        _investors.FindByEmailAsync("ana@teste.com", Arg.Any<CancellationToken>()).Returns(investor);

        var entrar = () => _service.SignInAsync(new SignInRequest("ana@teste.com", "outra-senha"));

        await entrar.Should().ThrowAsync<InvalidCredentialsException>();
        _tokens.DidNotReceive().IssueFor(Arg.Any<Investor>());
    }

    [Fact]
    public async Task SignIn_de_email_inexistente_deve_dar_a_mesma_mensagem_da_senha_errada()
    {
        _investors.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Investor?)null);
        var existente = new Investor("Ana", "ana@teste.com", _hasher.Hash("Senha@123"));

        var inexistente = await ((Func<Task>)(() =>
            _service.SignInAsync(new SignInRequest("ninguem@teste.com", "Senha@123"))))
            .Should().ThrowAsync<InvalidCredentialsException>();

        _investors.FindByEmailAsync("ana@teste.com", Arg.Any<CancellationToken>()).Returns(existente);

        var senhaErrada = await ((Func<Task>)(() =>
            _service.SignInAsync(new SignInRequest("ana@teste.com", "errada"))))
            .Should().ThrowAsync<InvalidCredentialsException>();

        // Nao vazar qual dos dois falhou impede descobrir quais e-mails existem.
        inexistente.And.Message.Should().Be(senhaErrada.And.Message);
    }

    [Fact]
    public async Task Get_de_investidor_inexistente_deve_dar_NotFound()
    {
        _investors.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Investor?)null);

        var buscar = () => _service.GetAsync(Guid.NewGuid());

        await buscar.Should().ThrowAsync<ResourceNotFoundException>();
    }
}
