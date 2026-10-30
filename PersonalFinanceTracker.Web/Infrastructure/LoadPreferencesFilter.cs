using Microsoft.AspNetCore.Mvc.Filters;
using PersonalFinanceTracker.Web.Security;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Infrastructure;

/// <summary>
/// Runs before every MVC action. For signed-in users it loads their theme and currency
/// into the request's UserPreferences, so views (and the layout) can format money and set
/// the theme without each controller having to remember to do it.
/// </summary>
public sealed class LoadPreferencesFilter(UserPreferences preferences) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            await preferences.LoadAsync(user.GetRequiredUserId(), context.HttpContext.RequestAborted);
        }

        await next();
    }
}
