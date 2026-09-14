using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.UseCases.AssetClasses;

namespace Portfolio.WebApi.Controllers;

/// <summary>
/// Manutencao das classes de ativo. Sao dados de referencia compartilhados: a
/// lista e a mesma para todos os investidores, por isso nao ha filtro por token aqui.
/// </summary>
[ApiController]
[Authorize]
[Route("asset-classes")]
[Produces("application/json")]
[Tags("Classes de ativo")]
public class AssetClassesController(IAssetClassService assetClasses) : ControllerBase
{
    /// <summary>Lista as classes de ativo em ordem alfabetica, com a quantidade de aportes de cada uma.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AssetClassView>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AssetClassView>>> List(CancellationToken ct) =>
        Ok(await assetClasses.ListAsync(ct));

    /// <summary>Obtem uma classe de ativo pelo id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AssetClassView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssetClassView>> GetById(int id, CancellationToken ct) =>
        Ok(await assetClasses.GetAsync(id, ct));

    /// <summary>Cadastra uma classe de ativo.</summary>
    /// <response code="409">Ja existe uma classe equivalente (a comparacao ignora acento e caixa).</response>
    [HttpPost]
    [ProducesResponseType<AssetClassView>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetClassView>> Create([FromBody] AssetClassRequest request, CancellationToken ct)
    {
        var created = await assetClasses.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Renomeia uma classe de ativo. Os aportes ja vinculados continuam apontando para ela.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AssetClassView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetClassView>> Rename(int id, [FromBody] AssetClassRequest request, CancellationToken ct) =>
        Ok(await assetClasses.RenameAsync(id, request, ct));

    /// <summary>Remove uma classe de ativo que nao tenha nenhum aporte vinculado.</summary>
    /// <response code="409">A classe esta em uso; a resposta informa quantos aportes a referenciam.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await assetClasses.DeleteAsync(id, ct);
        return NoContent();
    }
}
