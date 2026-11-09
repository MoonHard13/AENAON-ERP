using System.Security.Claims;
using Aenaon.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace Aenaon.Api.Authentication;

/// <summary>Ανακαλεί έγκαιρα cookies μετά από αλλαγή security stamp, λογαριασμού ή συμμετοχής.</summary>
public sealed class SessionCookieEvents(IServiceScopeFactory scopeFactory) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        string? rawId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? stamp = context.Principal?.FindFirstValue(SessionAuthenticationOptions.SecurityStampClaim);

        if (!Guid.TryParse(rawId, out Guid userId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(SessionAuthenticationOptions.Scheme);
            return;
        }

        // Η SQL υπηρεσία επιλύεται μόνο κατά τον πραγματικό έλεγχο ενεργού cookie.
        await using var scope = scopeFactory.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionIdentityService>();
        if (!await sessions.ValidateSessionAsync(userId, stamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(SessionAuthenticationOptions.Scheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/dashboard"))
        {
            context.Response.Redirect("/login");
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }

        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
