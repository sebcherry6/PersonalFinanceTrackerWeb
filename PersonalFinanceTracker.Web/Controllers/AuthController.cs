using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceTracker.Web.Models.ViewModels;
using PersonalFinanceTracker.Web.Security;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>Sign in, create account, sign out.</summary>
[AllowAnonymous]
public sealed class AuthController(
    AccountSecurity account,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    ILogger<AuthController> logger) : Controller
{
    // ---------------- Sign in ----------------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (signIn.IsSignedIn(User)) return RedirectToAction("Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]   // max ~10 attempts/minute per IP
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(Cleared(model));

        var result = await account.PasswordSignInAsync(model.Email.Trim(), model.Password, model.RememberMe);

        if (result.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            // Logged server-side for you; the user sees the same message as a wrong password
            // so the page doesn't reveal which emails have accounts.
            logger.LogWarning("Sign-in blocked by lockout for a user account.");
        }

        ModelState.AddModelError(string.Empty,
            "That email and password don't match. After 5 failed attempts, sign-in is paused for 15 minutes.");
        return View(Cleared(model));
    }

    // ---------------- Create account ----------------

    [HttpGet]
    public IActionResult Register()
    {
        if (signIn.IsSignedIn(User)) return RedirectToAction("Index", "Dashboard");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(Cleared(model));

        var email = model.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await signIn.SignInAsync(user, isPersistent: false);
            TempData["Status"] = "Your account is ready. Add your first transaction to get started.";
            return RedirectToAction("Index", "Dashboard");
        }

        // Identity reports a duplicate twice (username + email); show one clear message.
        if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists. Sign in instead.");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(Cleared(model));
    }

    // ---------------- Sign out ----------------

    /// <summary>POST only (with anti-forgery token) so another site can't sign you out via a link.</summary>
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---------------- Helpers ----------------

    /// <summary>
    /// Only follow returnUrl if it points inside this site. Otherwise a link like
    /// /Auth/Login?returnUrl=https://evil.example could bounce users to a phishing page
    /// right after a real sign-in. (The original called LocalRedirect, which threw an
    /// exception - a 500 error - instead.)
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");

    /// <summary>Never send a typed password back to the browser in the re-rendered form.</summary>
    private static LoginViewModel Cleared(LoginViewModel m) { m.Password = string.Empty; return m; }

    private static RegisterViewModel Cleared(RegisterViewModel m)
    {
        m.Password = string.Empty;
        m.ConfirmPassword = string.Empty;
        return m;
    }
}
