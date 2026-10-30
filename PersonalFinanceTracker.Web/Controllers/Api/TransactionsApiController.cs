using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceTracker.Web.Models;
using PersonalFinanceTracker.Web.Security;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers.Api;

/// <summary>
/// REST API for transactions. Requires "Authorization: Bearer &lt;token&gt;" from /api/auth/token.
///
///   GET    /api/transactions?search=&amp;type=Expense&amp;page=1
///   GET    /api/transactions/{id}
///   POST   /api/transactions           body: { description, amount, type, category, date }
///   PUT    /api/transactions/{id}      body: same as POST
///   DELETE /api/transactions/{id}
///   GET    /api/summary
///
/// [ApiController] validates the body automatically (same rules as the web form, via
/// TransactionInput) and returns 400 with details if it's invalid. Another user's ID
/// returns 404 - identical to an ID that doesn't exist, so nothing is revealed.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(AuthenticationSchemes = ApiAuth.Scheme)]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(RateLimitPolicies.Api)]
public sealed class TransactionsApiController(FinanceService finance) : ControllerBase
{
    private string UserId => User.GetRequiredUserId();

    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResponse<TransactionDto>>> List(
        string? search, TransactionType? type, int page = 1, CancellationToken ct = default)
    {
        if (type is { } t && !Enum.IsDefined(t)) type = null;

        var results = await finance.SearchTransactionsAsync(UserId, search, type, page, ct);
        return new PagedResponse<TransactionDto>(
            results.Items.Select(TransactionDto.From).ToList(),
            results.Page, results.PageSize, results.TotalCount, results.TotalPages);
    }

    [HttpGet("transactions/{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Get(Guid id, CancellationToken ct)
    {
        var transaction = await finance.GetTransactionAsync(id, UserId, ct);
        if (transaction is null) return NotFound();
        return TransactionDto.From(transaction);
    }

    [HttpPost("transactions")]
    public async Task<ActionResult<TransactionDto>> Create(TransactionInput input, CancellationToken ct)
    {
        var created = await finance.AddTransactionAsync(UserId, input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, TransactionDto.From(created));
    }

    [HttpPut("transactions/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, TransactionInput input, CancellationToken ct)
    {
        if (!await finance.UpdateTransactionAsync(id, UserId, input, ct)) return NotFound();
        return NoContent();
    }

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await finance.DeleteTransactionAsync(id, UserId, ct)) return NotFound();
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<SummaryDto> Summary(CancellationToken ct)
    {
        var d = await finance.GetDashboardAsync(UserId, ct);
        return new SummaryDto(d.Balance, d.Month, d.MonthIn, d.MonthOut, d.MonthSpending);
    }
}
