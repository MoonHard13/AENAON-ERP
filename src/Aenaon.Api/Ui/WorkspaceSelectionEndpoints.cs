using System.Security.Claims;
using Aenaon.Application.Tenancy;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Aenaon.Api.Ui;

/// <summary>Αλλάζει προτίμηση εργασίας μόνο με αυθεντικοποιημένο και CSRF-protected POST.</summary>
public static class WorkspaceSelectionEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceSelectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ui/workspace/select", SelectAsync)
            .WithMetadata(new RequestSizeLimitAttribute(8192))
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SelectAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        TenantWorkspaceResolver resolver,
        WorkspacePreferenceService preference,
        ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps ||
            !context.Request.HasFormContentType ||
            context.Request.ContentLength > 8192 ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId) ||
            userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!Guid.TryParse(form["tenantId"], out Guid tenantId) || tenantId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        Guid? storeId = null;
        string storeField = form["storeId"].ToString();
        if (storeField.Length > 0)
        {
            if (!Guid.TryParse(storeField, out Guid parsedStore) || parsedStore == Guid.Empty)
            {
                return Results.BadRequest();
            }

            storeId = parsedStore;
        }

        TenantWorkspaceSnapshot selection = await resolver.ResolveAsync(
            userId, tenantId, storeId, context.RequestAborted);

        if (selection.Status is not (TenantWorkspaceStatus.Ready or TenantWorkspaceStatus.NoStores))
        {
            loggerFactory.CreateLogger("Aenaon.Api.Ui.Workspace")
                .LogWarning("Workspace selection refused for {UserId}", userId);
            return Results.Forbid();
        }

        preference.Save(context, userId, selection);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Redirect("/dashboard", permanent: false, preserveMethod: false);
    }
}
