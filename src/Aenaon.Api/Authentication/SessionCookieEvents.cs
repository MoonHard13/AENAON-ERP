using System.Security.Claims;
using Aenaon.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Aenaon.Api.Authentication;

/// <summary>Ανακαλεί έγκαιρα cookies μετά από αλλαγή security stamp, λογαριασμού ή συμμετοχής.</summary>
public sealed class SessionCookieEvents(ISessionIdentityService sessions) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        string? rawId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? stamp = context.Principal?.FindFirstValue(SessionAuthenticationOptions.SecurityStampClaim);

        if (!Guid.TryParse(rawId, out Guid userId) ||
            !await sessions.ValidateSessionAsync(userId, stamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(SessionAuthenticationOptions.Scheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
