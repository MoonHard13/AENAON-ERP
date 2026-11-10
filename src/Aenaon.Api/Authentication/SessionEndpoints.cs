using System.Security.Claims;
using Aenaon.Api.Contracts;
using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Microsoft.AspNetCore.Antiforgery;

namespace Aenaon.Api.Authentication;

/// <summary>Development-only endpoints για Cookie Login/Logout με CSRF και ταυτότητα προερχόμενη από τον server.</summary>
public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");
        group.MapGet("/csrf", GetCsrfToken).RequireRateLimiting(SessionAuthenticationOptions.RateLimitPolicy);
        group.MapPost("/login", LoginAsync).RequireRateLimiting(SessionAuthenticationOptions.RateLimitPolicy);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization()
            .RequireRateLimiting(SessionAuthenticationOptions.RateLimitPolicy);
        group.MapGet("/me", MeAsync).RequireAuthorization();
        group.MapGet("/access", CheckTenantAccessAsync).RequireAuthorization();
        return group;
    }

    private static IResult GetCsrfToken(HttpContext context, IAntiforgery antiforgery)
    {
        if (!context.Request.IsHttps)
        {
            return Results.BadRequest();
        }

        context.Response.Headers.CacheControl = "no-store";
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context, LoginRequest credentials, IAntiforgery antiforgery,
        ISessionIdentityService sessions, ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        context.Response.Headers.CacheControl = "no-store";
        if (credentials.UserName is null || credentials.Password is null ||
            credentials.UserName.Length > 128 || credentials.Password.Length > 1024)
        {
            return Results.Unauthorized();
        }

        SessionIdentity? account = await sessions.AuthenticateAsync(
            credentials.UserName, credentials.Password, context.RequestAborted);
        if (account is null)
        {
            loggerFactory.CreateLogger("Aenaon.Api.Authentication").LogInformation("Login rejected");
            return Results.Unauthorized();
        }

        await SessionCookieIssuer.SignInAsync(context, account);

        loggerFactory.CreateLogger("Aenaon.Api.Authentication")
            .LogInformation("User signed in: {UserId}", account.UserId);
        return Results.Ok(new { account.UserId, account.UserName });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context, IAntiforgery antiforgery, ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        await SessionCookieIssuer.SignOutAsync(context);
        loggerFactory.CreateLogger("Aenaon.Api.Authentication")
            .LogInformation("User signed out: {UserId}", userId);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(HttpContext context, TenantAuthorizationService authorization)
    {
        if (!TryGetAuthenticatedUserId(context, out Guid userId))
        {
            return Results.Unauthorized();
        }

        IReadOnlyList<AccessibleTenant> tenants =
            await authorization.GetAccessibleTenantsAsync(userId, context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new CurrentSessionResponse(userId, context.User.Identity?.Name ?? string.Empty, tenants));
    }

    private static async Task<IResult> CheckTenantAccessAsync(
        HttpContext context, Guid tenantId, Guid? storeId, string? permission,
        TenantAuthorizationService authorization)
    {
        if (!TryGetAuthenticatedUserId(context, out Guid userId))
        {
            return Results.Unauthorized();
        }

        TenantPermission required;
        if (string.Equals(permission, "Read", StringComparison.OrdinalIgnoreCase))
        {
            required = TenantPermission.Read;
        }
        else if (string.Equals(permission, "Manage", StringComparison.OrdinalIgnoreCase))
        {
            required = TenantPermission.Manage;
        }
        else
        {
            return Results.BadRequest();
        }

        TenantAccessResult result = await authorization.CheckAsync(userId, tenantId, storeId,
            required, context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        return result.Status == TenantAccessStatus.Granted
            ? Results.Ok(new TenantPermissionResponse(tenantId, storeId, required, true))
            : Results.Forbid();
    }

    private static bool TryGetAuthenticatedUserId(HttpContext context, out Guid userId)
    {
        userId = Guid.Empty;
        return context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
            userId != Guid.Empty;
    }
}
