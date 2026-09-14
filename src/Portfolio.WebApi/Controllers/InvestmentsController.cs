using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.UseCases.Investments;
using Portfolio.WebApi.Extensions;

namespace Portfolio.WebApi.Controllers;

/// <summary>
/// Aportes do investidor autenticado. Todo metodo tira o id do investidor do
/// token; nao existe parametro de rota ou de query que permita apontar para outro.
/// </summary>
[ApiController]
[Authorize]
[Route("investments")]
[Produces("application/json")]
[Tags("Aportes")]
public class InvestmentsController(IInvestmentService investments) : ControllerBase
{
    /// <summary>Lista os aportes do investidor autenticado, do mais recente para o mais antigo.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvestmentView>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<InvestmentView>>> List(CancellationToken ct) =>
        Ok(await investments.ListAsync(User.InvestorId(), ct));

    /// <summary>Resume a carteira: total aplicado e participacao de cada classe de ativo.</summary>
    /// <remarks>Fica antes da rota por id no arquivo, mas nao ha ambiguidade: "summary" nao casa com a restricao :guid.</remarks>
    [HttpGet("summary")]
    [ProducesResponseType<PortfolioSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PortfolioSummary>> Summary(CancellationToken ct) =>
        Ok(await investments.SummarizeAsync(User.InvestorId(), ct));

    /// <summary>Obtem um aporte do investidor autenticado.</summary>
    /// <response code="404">O aporte nao existe ou pertence a outro investidor.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<InvestmentView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvestmentView>> GetById(Guid id, CancellationToken ct) =>
        Ok(await investments.GetAsync(User.InvestorId(), id, ct));

    /// <summary>Registra um aporte.</summary>
    /// <response code="201">Aporte registrado.</response>
    /// <response code="400">Campo obrigatorio ausente, valor fora da faixa, data futura ou classe de ativo inexistente.</response>
    [HttpPost]
    [ProducesResponseType<InvestmentView>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvestmentView>> Create([FromBody] InvestmentRequest request, CancellationToken ct)
    {
        var created = await investments.CreateAsync(User.InvestorId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Substitui os dados de um aporte.</summary>
    /// <response code="404">O aporte nao existe ou pertence a outro investidor.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<InvestmentView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvestmentView>> Update(Guid id, [FromBody] InvestmentRequest request, CancellationToken ct) =>
        Ok(await investments.UpdateAsync(User.InvestorId(), id, request, ct));

    /// <summary>Remove um aporte.</summary>
    /// <response code="204">Aporte removido.</response>
    /// <response code="404">O aporte nao existe, pertence a outro investidor ou ja foi removido.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await investments.DeleteAsync(User.InvestorId(), id, ct);
        return NoContent();
    }
}
