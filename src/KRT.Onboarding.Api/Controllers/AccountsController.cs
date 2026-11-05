using KRT.Onboarding.Application.Accounts;
using KRT.Onboarding.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace KRT.Onboarding.Api.Controllers;

/// <summary>CRUD de contas de clientes do banco KRT.</summary>
[ApiController]
[Route("api/v1/accounts")]
[Produces("application/json")]
public sealed class AccountsController(IAccountService accountService) : ControllerBase
{
    /// <summary>Abre uma nova conta (status inicial: Active).</summary>
    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Create(
        [FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await accountService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }

    /// <summary>Consulta uma conta pelo Id (servida do cache quando já consultada no dia).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await accountService.GetByIdAsync(id, cancellationToken));

    /// <summary>Lista as contas de forma paginada.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AccountResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AccountResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await accountService.ListAsync(page, pageSize, cancellationToken));

    /// <summary>Atualiza o nome do titular e/ou o status (Active/Inactive). O CPF não pode ser alterado.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Update(
        Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken) =>
        Ok(await accountService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Remove uma conta.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await accountService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
