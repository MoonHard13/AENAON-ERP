using Aenaon.Api.Authentication;
using Aenaon.Application.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Aenaon.Api.Ui;

/// <summary>Συμβατικό HTML POST-redirect-get για Blazor SSR χωρίς χειρισμό κωδικού μέσω JavaScript.</summary>
public static class UiSessionEndpoints
{
    public static RouteGroupBuilder MapUiSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ui/session");
        group.MapPost("/login", LoginAsync)
            .WithMetadata(new RequestSizeLimitAttribute(8192))
            .RequireRateLimiting(SessionAuthenticationOptions.RateLimitPolicy);
        group.MapPost("/logout", LogoutAsync)
            .WithMetadata(new RequestSizeLimitAttribute(8192))
            .RequireAuthorization()
            .RequireRateLimiting(SessionAuthenticationOptions.RateLimitPolicy);
        return group;
    }

    private static async Task<IResult> LoginAsync(HttpContext context, IAntiforgery antiforgery,
        ISessionIdentityService sessions, ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !context.Request.HasFormContentType ||
            context.Request.ContentLength > 8192 ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        var fields = await context.Request.ReadFormAsync(context.RequestAborted);
        string userName = fields["username"].ToString();
        string password = fields["password"].ToString();
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 128 ||
            string.IsNullOrEmpty(password) || password.Length > 1024)
        {
            return RedirectAfterPost("/login?error=invalid");
        }

        SessionIdentity? user = await sessions.AuthenticateAsync(userName, password,
            context.RequestAborted);
        if (user is null)
        {
            loggerFactory.CreateLogger("Aenaon.Api.Ui.Login").LogInformation("UI sign-in rejected");
            return RedirectAfterPost("/login?error=invalid");
        }

        await SessionCookieIssuer.SignInAsync(context, user);
        loggerFactory.CreateLogger("Aenaon.Api.Ui.Login")
            .LogInformation("UI sign-in succeeded for {UserId}", user.UserId);
        return RedirectAfterPost("/dashboard");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery,
        ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !context.Request.HasFormContentType ||
            context.Request.ContentLength > 8192 ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        await SessionCookieIssuer.SignOutAsync(context);
        loggerFactory.CreateLogger("Aenaon.Api.Ui.Logout").LogInformation("UI session ended");
        return RedirectAfterPost("/login");
    }

    private static IResult RedirectAfterPost(string relativePath)
    {
        // Σταθερή εσωτερική διαδρομή αποκλείει open redirects και διατηρεί το POST/Redirect/GET.
        return Results.Redirect(relativePath, permanent: false, preserveMethod: false);
    }
}
