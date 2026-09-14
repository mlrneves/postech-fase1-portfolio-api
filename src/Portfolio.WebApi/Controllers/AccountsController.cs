using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.UseCases.Accounts;
using Portfolio.WebApi.Extensions;
using Portfolio.WebApi.Setup;

namespace Portfolio.WebApi.Controllers;

/// <summary>Cadastro de investidor e emissao de token. Os dois endpoints anonimos da API.</summary>
[ApiController]
[Produces("application/json")]
[Tags("Conta")]
public class AccountsController(IAccountService accounts) : ControllerBase
{
    /// <summary>Cadastra um novo investidor.</summary>
    /// <response code="201">Investidor criado.</response>
    /// <response code="400">Algum campo obrigatorio esta ausente ou invalido.</response>
    /// <response code="409">Ja existe investidor com este e-mail.</response>
    [HttpPost("/users")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitOptions.CredentialsPolicy)]
    [ProducesResponseType<InvestorView>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<InvestorView>> SignUp([FromBody] SignUpRequest request, CancellationToken ct)
    {
        var investor = await accounts.SignUpAsync(request, ct);

        // O Location aponta para /users/me: e onde o proprio investidor le seus
        // dados depois de autenticar. A API nao expoe o cadastro de terceiros.
        return CreatedAtAction(nameof(Me), routeValues: null, value: investor);
    }

    /// <summary>Autentica o investidor e devolve o token de acesso.</summary>
    /// <response code="200">Token emitido.</response>
    /// <response code="401">E-mail ou senha invalidos.</response>
    [HttpPost("/auth")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitOptions.CredentialsPolicy)]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SignInResponse>> SignIn([FromBody] SignInRequest request, CancellationToken ct) =>
        Ok(await accounts.SignInAsync(request, ct));

    /// <summary>Devolve o investidor dono do token enviado. Serve para conferir se o Bearer esta valendo.</summary>
    /// <response code="200">Dados do investidor autenticado.</response>
    /// <response code="401">Token ausente, expirado ou invalido.</response>
    [HttpGet("/users/me")]
    [Authorize]
    [ProducesResponseType<InvestorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InvestorView>> Me(CancellationToken ct) =>
        Ok(await accounts.GetAsync(User.InvestorId(), ct));
}
