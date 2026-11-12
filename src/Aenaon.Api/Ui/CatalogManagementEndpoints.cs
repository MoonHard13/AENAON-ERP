using System.Security.Claims;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Aenaon.Api.Ui;

/// <summary>Μοναδικό προστατευμένο HTTP write entry point του Development Back Office καταλόγου.</summary>
public static class CatalogManagementEndpoints
{
    public static IEndpointRouteBuilder MapCatalogManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ui/catalog/save", SaveAsync)
            .WithMetadata(new RequestSizeLimitAttribute(8192))
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SaveAsync(HttpContext context,
        IAntiforgery antiforgery, CatalogWriteService manager,
        ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !context.Request.HasFormContentType ||
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

        var fields = await context.Request.ReadFormAsync(context.RequestAborted);
        string view = fields["view"].ToString();
        CatalogManagedEntity entity = view switch
        {
            "categories" => CatalogManagedEntity.Category,
            "products" => CatalogManagedEntity.Product,
            "services" => CatalogManagedEntity.Service,
            _ => (CatalogManagedEntity)(-1)
        };
        string actionText = fields["action"].ToString();
        CatalogChangeAction action = actionText switch
        {
            "create" => CatalogChangeAction.Create,
            "update" => CatalogChangeAction.Update,
            "activate" => CatalogChangeAction.Activate,
            "deactivate" => CatalogChangeAction.Deactivate,
            _ => (CatalogChangeAction)(-1)
        };

        if (!Enum.IsDefined(entity) || !Enum.IsDefined(action) ||
            !Guid.TryParse(fields["tenantId"], out Guid tenantId) || tenantId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        string idText = fields["id"].ToString();
        Guid? id = null;
        if (idText.Length > 0)
        {
            if (!Guid.TryParse(idText, out Guid parsedId) || parsedId == Guid.Empty)
            {
                return Results.BadRequest();
            }
            id = parsedId;
        }

        Guid? categoryId = null;
        string categoryText = fields["categoryId"].ToString();
        if (action is CatalogChangeAction.Create or CatalogChangeAction.Update &&
            entity != CatalogManagedEntity.Category)
        {
            if (!Guid.TryParse(categoryText, out Guid parsedCategory) ||
                parsedCategory == Guid.Empty)
            {
                return Results.Redirect(
                    ReturnToForm(view, tenantId, id, "category"), permanent: false);
            }
            categoryId = parsedCategory;
        }

        string code = fields["code"].ToString();
        string name = fields["name"].ToString();
        if (code.Length > CatalogItem.MaxCodeLength + 8 ||
            name.Length > CatalogItem.MaxNameLength + 8)
        {
            return Results.BadRequest();
        }

        CatalogChangeCommand command = new(entity, action, id,
            code, name, categoryId,
            entity == CatalogManagedEntity.Product &&
            string.Equals(fields["tracksStock"], "true", StringComparison.Ordinal));

        CatalogChangeResult result = await manager.ApplyAsync(
            userId, tenantId, command, context.RequestAborted);
        var logger = loggerFactory.CreateLogger("Aenaon.Api.Ui.CatalogManagement");
        if (result.Status == CatalogChangeStatus.Denied)
        {
            logger.LogWarning("Catalog mutation denied for {UserId} {TenantId} {Entity} {Action}",
                userId, tenantId, entity, action);
            return Results.Forbid();
        }

        if (result.Status == CatalogChangeStatus.Missing)
        {
            return Results.NotFound();
        }

        if (result.Status == CatalogChangeStatus.Saved)
        {
            logger.LogInformation(
                "Catalog changed by {UserId} in {TenantId}: {Entity} {Action} {RecordId}",
                userId, tenantId, entity, action, result.Id);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Redirect($"/catalog?view={view}&tenantId={tenantId:D}&saved=1", permanent: false);
        }

        string error = result.Status == CatalogChangeStatus.Duplicate ? "duplicate" :
            result.Error == "catalog.category.inactive_or_missing" ? "category" : "invalid";
        logger.LogInformation("Catalog mutation rejected for {UserId} {TenantId} {Entity}: {Status}",
            userId, tenantId, entity, result.Status);
        return Results.Redirect(ReturnToForm(view, tenantId, id, error), permanent: false);
    }

    private static string ReturnToForm(string view, Guid tenantId, Guid? id, string error) =>
        $"/catalog/manage?view={view}&tenantId={tenantId:D}" +
        (id.HasValue ? $"&id={id.Value:D}" : string.Empty) + $"&error={error}";
}
