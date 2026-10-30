using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Security;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>
/// Base class for the signed-in pages. Authentication is enforced globally by the
/// fallback policy in Program.cs, so there's no [Authorize] to forget here.
/// </summary>
public abstract class AppController : Controller
{
    /// <summary>The signed-in user's ID, from their (signed, encrypted) auth cookie.</summary>
    protected string UserId => User.GetRequiredUserId();

    /// <summary>
    /// One-off messages shown at the top of the next page ("Transaction added").
    /// Used with redirect-after-POST so refreshing the page never re-submits a form.
    /// </summary>
    protected void Flash(string message) => TempData["Status"] = message;

    protected void FlashError(string message) => TempData["Error"] = message;
}
