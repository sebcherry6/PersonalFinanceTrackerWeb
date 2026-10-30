using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Models.ViewModels;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>Theme, currency, data export and account deletion.</summary>
public sealed class SettingsController(
    FinanceService finance,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    ILogger<SettingsController> logger) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var settings = await finance.GetSettingsAsync(UserId, ct);
        return View(Page(new SettingsInput { Theme = settings.Theme, Currency = settings.Currency }));
    }

    [HttpPost]
    public async Task<IActionResult> Index([Bind(Prefix = "Preferences")] SettingsInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(Page(input));

        // Note: no UserId is accepted from the form. It's always the signed-in user.
        await finance.SaveSettingsAsync(UserId, input, ct);
        Flash("Settings saved.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Permanently deletes the account. Requires the current password (counts toward lockout),
    /// then removes the user; the database's cascade rules delete all their transactions,
    /// budgets, goals and settings with it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteAccount([Bind(Prefix = "DeleteAccount")] DeleteAccountInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            await signIn.SignOutAsync();
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            FlashError("Enter your password to delete your account.");
            return RedirectToAction(nameof(Index));
        }

        var check = await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            FlashError("That password isn't right, so nothing was deleted.");
            return RedirectToAction(nameof(Index));
        }

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            logger.LogError("Account deletion failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Code)));
            FlashError("Your account couldn't be deleted. Please try again.");
            return RedirectToAction(nameof(Index));
        }

        await signIn.SignOutAsync();
        TempData["Status"] = "Your account and all of its data have been deleted.";
        return RedirectToAction("Login", "Auth");
    }

    private SettingsViewModel Page(SettingsInput input)
    {
        ViewData["Title"] = "Settings";
        ViewData["Nav"] = "settings";
        return new SettingsViewModel { Preferences = input, Email = User.Identity?.Name ?? string.Empty };
    }
}
