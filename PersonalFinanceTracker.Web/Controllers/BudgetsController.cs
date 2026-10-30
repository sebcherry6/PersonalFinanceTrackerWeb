using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Models;
using PersonalFinanceTracker.Web.Models.ViewModels;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>Monthly spending limits per category.</summary>
public sealed class BudgetsController(FinanceService finance) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await PageAsync(new BudgetInput(), ct));

    /// <summary>
    /// Creates a budget, or updates the limit if that category already has one.
    /// Fields are posted as "NewBudget.Category" etc., hence the Bind prefix.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Save([Bind(Prefix = "NewBudget")] BudgetInput input, CancellationToken ct)
    {
        if (!Categories.Expense.Contains(input.Category))
        {
            ModelState.AddModelError("NewBudget.Category", "Budgets are for spending categories.");
        }

        if (!ModelState.IsValid) return View(nameof(Index), await PageAsync(input, ct));

        await finance.SaveBudgetAsync(UserId, input, ct);
        Flash($"Budget for {input.Category} saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await finance.DeleteBudgetAsync(id, UserId, ct)) return NotFound();

        Flash("Budget removed.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<BudgetsViewModel> PageAsync(BudgetInput input, CancellationToken ct)
    {
        ViewData["Title"] = "Budgets";
        ViewData["Nav"] = "budgets";

        var today = finance.Today;
        return new BudgetsViewModel
        {
            Month = new DateOnly(today.Year, today.Month, 1),
            Budgets = await finance.GetBudgetProgressAsync(UserId, ct),
            NewBudget = input
        };
    }
}
