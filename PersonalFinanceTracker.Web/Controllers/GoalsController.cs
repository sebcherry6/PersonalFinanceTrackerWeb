using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Models.ViewModels;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>Savings goals: create, add/withdraw money, delete.</summary>
public sealed class GoalsController(FinanceService finance) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await PageAsync(new GoalInput(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "NewGoal")] GoalInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(nameof(Index), await PageAsync(input, ct));

        await finance.AddGoalAsync(UserId, input, ct);
        Flash($"Goal \u201c{input.Name.Trim()}\u201d created.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The "Add" and "Withdraw" buttons both post here; Withdraw=true for the latter.</summary>
    [HttpPost]
    public async Task<IActionResult> Contribute(Guid id, GoalContributionInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            FlashError("Enter an amount between 0.01 and 10,000,000.");
            return RedirectToAction(nameof(Index));
        }

        if (!await finance.ContributeToGoalAsync(id, UserId, input, ct)) return NotFound();

        Flash(input.Withdraw ? "Withdrawal recorded." : "Added to your goal.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await finance.DeleteGoalAsync(id, UserId, ct)) return NotFound();

        Flash("Goal removed.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<GoalsViewModel> PageAsync(GoalInput input, CancellationToken ct)
    {
        ViewData["Title"] = "Goals";
        ViewData["Nav"] = "goals";
        return new GoalsViewModel { Goals = await finance.GetGoalsAsync(UserId, ct), NewGoal = input };
    }
}
