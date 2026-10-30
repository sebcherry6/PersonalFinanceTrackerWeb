using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Web.Models.ViewModels;

namespace PersonalFinanceTracker.Web.Controllers;

/// <summary>
/// Error pages. The original pointed UseExceptionHandler at /Home/Error but there was no
/// HomeController, so any crash in production produced a second, blank error.
///
/// [IgnoreAntiforgeryToken] matters: an error can happen during a POST, and the error page
/// is re-run with that same POST, which must not fail anti-forgery validation itself.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class HomeController : Controller
{
    [Route("error")]
    public IActionResult Error() => ErrorView(StatusCodes.Status500InternalServerError);

    [Route("error/{code:int}")]
    public IActionResult Status(int code) => ErrorView(code is >= 400 and < 600 ? code : 500);

    private ViewResult ErrorView(int code)
    {
        Response.StatusCode = code;
        return View("Error", new ErrorViewModel
        {
            StatusCode = code,
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
