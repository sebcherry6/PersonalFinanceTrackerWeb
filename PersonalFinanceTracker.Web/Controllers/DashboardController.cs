using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>Overview (home page after sign-in) and Insights.</summary>
public sealed class DashboardController(FinanceService finance) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Overview";
        ViewData["Nav"] = "overview";
        return View(await finance.GetDashboardAsync(UserId, ct));
    }

    public async Task<IActionResult> Analytics(CancellationToken ct)
    {
        ViewData["Title"] = "Insights";
        ViewData["Nav"] = "insights";
        return View(await finance.GetAnalyticsAsync(UserId, months: 6, ct));
    }
}
