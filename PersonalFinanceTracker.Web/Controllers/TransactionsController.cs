using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Models;
using PersonalFinanceTracker.Web.Models.ViewModels;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>
/// List, add, edit, delete and export transactions.
///
/// Pattern used for every form: GET shows the form, POST validates. On success it redirects
/// (so refresh can't double-submit); on failure it re-shows the form with messages.
/// IDs from the URL are only ever looked up together with the signed-in UserId.
/// </summary>
public sealed class TransactionsController(FinanceService finance) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, TransactionType? type, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Transactions";
        ViewData["Nav"] = "transactions";

        // Ignore made-up enum values like ?type=42.
        if (type is { } t && !Enum.IsDefined(t)) type = null;

        return View(new TransactionListViewModel
        {
            Results = await finance.SearchTransactionsAsync(UserId, q, type, page, ct),
            Search = q,
            Type = type
        });
    }

    [HttpGet]
    public IActionResult Create(TransactionType type = TransactionType.Expense)
    {
        ViewData["Title"] = "Add transaction";
        ViewData["Nav"] = "add";

        var input = new TransactionInput { Date = finance.Today, Type = Enum.IsDefined(type) ? type : TransactionType.Expense };
        input.Category = input.Type == TransactionType.Income ? Category.Salary : Category.Food;
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TransactionInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add transaction";
            ViewData["Nav"] = "add";
            return View(input);
        }

        await finance.AddTransactionAsync(UserId, input, ct);
        Flash($"Added \u201c{input.Description.Trim()}\u201d.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var transaction = await finance.GetTransactionAsync(id, UserId, ct);
        if (transaction is null) return NotFound();

        ViewData["Title"] = "Edit transaction";
        ViewData["Nav"] = "transactions";
        ViewData["TransactionId"] = id;
        return View(TransactionInput.From(transaction));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, TransactionInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit transaction";
            ViewData["Nav"] = "transactions";
            ViewData["TransactionId"] = id;
            return View(input);
        }

        if (!await finance.UpdateTransactionAsync(id, UserId, input, ct)) return NotFound();

        Flash("Changes saved.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Confirmation page. Deleting itself only happens on POST.</summary>
    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var transaction = await finance.GetTransactionAsync(id, UserId, ct);
        if (transaction is null) return NotFound();

        ViewData["Title"] = "Delete transaction";
        ViewData["Nav"] = "transactions";
        return View(transaction);
    }

    [HttpPost, ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken ct)
    {
        if (!await finance.DeleteTransactionAsync(id, UserId, ct)) return NotFound();

        Flash("Transaction deleted.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Downloads every transaction as a CSV file (safe against formula injection).</summary>
    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var all = await finance.GetAllTransactionsAsync(UserId, ct);
        var fileName = $"transactions-{finance.Today:yyyy-MM-dd}.csv";
        return File(CsvExporter.Write(all), "text/csv; charset=utf-8", fileName);
    }
}
