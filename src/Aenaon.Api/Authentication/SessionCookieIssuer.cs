using System.Security.Claims;
using Aenaon.Application.Identity;
using Microsoft.AspNetCore.Authentication;

namespace Aenaon.Api.Authentication;

/// <summary>Κοινή έκδοση cookie για JSON και Blazor SSR φόρμες, χωρίς duplication ασφαλείας.</summary>
public static class SessionCookieIssuer
{
    public static async Task SignInAsync(HttpContext context, SessionIdentity account)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
            new Claim(ClaimTypes.Name, account.UserName),
            new Claim(SessionAuthenticationOptions.SecurityStampClaim, account.SecurityStamp)
        };
        var identity = new ClaimsIdentity(claims, SessionAuthenticationOptions.Scheme);
        await context.SignInAsync(SessionAuthenticationOptions.Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });
        context.Response.Headers.CacheControl = "no-store";
    }

    public static async Task SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(SessionAuthenticationOptions.Scheme);
        context.Response.Headers.CacheControl = "no-store";
    }
}
