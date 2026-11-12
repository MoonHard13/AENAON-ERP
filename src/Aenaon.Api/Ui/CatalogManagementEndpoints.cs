using System.Security.Claims;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Aenaon.Api.Ui;

/// <summary>Προστατευμένα endpoints καταλόγου, διαθέσιμα αποκλειστικά στο Development.</summary>
public static class CatalogManagementEndpoints
{
    public static IEndpointRouteBuilder MapCatalogManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ui/catalog/save", SaveAsync)
            .WithMetadata(new RequestSizeLimitAttribute(8192))
            .RequireAuthorization();
        endpoints.MapGet("/ui/catalog/categories", SearchCategoriesAsync)
            .RequireAuthorization();
        endpoints.MapGet("/ui/catalog/children", ReadChildrenAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ReadChildrenAsync(HttpContext context,
        CatalogReadService reader)
    {
        if (!context.Request.IsHttps || !TryIdentity(context, out Guid userId) ||
            !Guid.TryParse(context.Request.Query["tenantId"], out Guid tenantId) ||
            tenantId == Guid.Empty ||
            !Guid.TryParse(context.Request.Query["parentId"], out Guid parentId) ||
            parentId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        CatalogReadResult<CatalogCategorySummary> result = await reader.ReadCategoriesAsync(
            userId, tenantId, new CatalogReadQuery(1, 100, ParentCategoryId: parentId),
            context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        return !result.IsAuthorized ? Results.Forbid() : Results.Json(result.Page);
    }

    private static async Task<IResult> SearchCategoriesAsync(HttpContext context,
        CatalogWriteService manager)
    {
        if (!context.Request.IsHttps || !TryIdentity(context, out Guid userId) ||
            !Guid.TryParse(context.Request.Query["tenantId"], out Guid tenantId) ||
            tenantId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        string term = context.Request.Query["search"].ToString();
        if (term.Length > 80 || term.Any(char.IsControl))
        {
            return Results.BadRequest();
        }

        Guid? selected = null;
        string selectedText = context.Request.Query["selectedId"].ToString();
        if (selectedText.Length > 0)
        {
            if (!Guid.TryParse(selectedText, out Guid candidate) || candidate == Guid.Empty)
            {
                return Results.BadRequest();
            }

            selected = candidate;
        }

        IReadOnlyList<CatalogCategoryOption>? rows = await manager.SearchCategoriesAsync(
            userId, tenantId, term, selected, context.RequestAborted);
        context.Response.Headers.CacheControl = "no-store";
        return rows is null ? Results.Forbid() : Results.Json(rows);
    }

    private static async Task<IResult> SaveAsync(HttpContext context,
        IAntiforgery antiforgery, CatalogWriteService manager,
        CatalogEditorDraftStore drafts, ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !context.Request.HasFormContentType ||
            context.Request.ContentLength > 8192 ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        if (!TryIdentity(context, out Guid userId))
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

        Guid? requestId = null;
        string requestText = fields["requestId"].ToString();
        if (requestText.Length > 0)
        {
            if (!Guid.TryParse(requestText, out Guid parsedRequest) ||
                parsedRequest == Guid.Empty)
            {
                return Results.BadRequest();
            }

            requestId = parsedRequest;
        }

        bool writingFields = action is CatalogChangeAction.Create or CatalogChangeAction.Update;
        string code = fields["code"].ToString();
        string name = fields["name"].ToString();
        bool autoCode = writingFields && action == CatalogChangeAction.Create &&
            string.Equals(fields["codeMode"], "auto", StringComparison.Ordinal);

        Guid? categoryId = null;
        Guid? parentCategoryId = null;
        string invalidField = "";
        if (writingFields && entity != CatalogManagedEntity.Category)
        {
            if (!Guid.TryParse(fields["categoryId"], out Guid candidate) ||
                candidate == Guid.Empty)
            {
                invalidField = "category";
            }
            else
            {
                categoryId = candidate;
            }
        }

        if (writingFields && entity == CatalogManagedEntity.Category &&
            !string.IsNullOrWhiteSpace(fields["parentCategoryId"].ToString()))
        {
            if (!Guid.TryParse(fields["parentCategoryId"], out Guid candidate) ||
                candidate == Guid.Empty)
            {
                invalidField = "parent";
            }
            else
            {
                parentCategoryId = candidate;
            }
        }

        bool tracksStock = entity == CatalogManagedEntity.Product &&
            string.Equals(fields["tracksStock"], "true", StringComparison.Ordinal);
        if (code.Length > CatalogItem.MaxCodeLength + 8 ||
            name.Length > CatalogItem.MaxNameLength + 8)
        {
            return Results.BadRequest();
        }

        string saveMode = fields["saveMode"].ToString();
        if (saveMode is not ("" or "close" or "new"))
        {
            return Results.BadRequest();
        }

        var logger = loggerFactory.CreateLogger("Aenaon.Api.Ui.CatalogManagement");
        if (!string.IsNullOrEmpty(invalidField))
        {
            return await FailedAsync(invalidField);
        }

        CatalogChangeCommand command = new(entity, action, id,
            writingFields && autoCode ? null : code,
            name, categoryId, tracksStock, parentCategoryId, autoCode, requestId);
        CatalogChangeResult result = await manager.ApplyAsync(
            userId, tenantId, command, context.RequestAborted);

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
                "Catalog mutation by {UserId} for {TenantId} {Entity} {Action} {RecordId}",
                userId, tenantId, entity, action, result.Id);
            context.Response.Headers.CacheControl = "no-store";
            string savedCode = Uri.EscapeDataString(result.Code ?? "");
            string destination = saveMode == "new" &&
                action is CatalogChangeAction.Create or CatalogChangeAction.Update
                ? $"/catalog/manage?view={view}&tenantId={tenantId:D}&savedCode={savedCode}"
                : $"/catalog?view={view}&tenantId={tenantId:D}&saved=1&savedCode={savedCode}";
            return Results.Redirect(destination, permanent: false);
        }

        string error = result.Status == CatalogChangeStatus.Duplicate ? "duplicate" :
            result.Error switch
            {
                "catalog.parent.invalid" => "parent",
                "catalog.category.in_use" => "inuse",
                "catalog.category.inactive_or_missing" => "category",
                "catalog.code.exhausted" => "code",
                _ => "invalid"
            };
        logger.LogInformation("Catalog change rejected for {UserId} {TenantId} {Entity} {Status}",
            userId, tenantId, entity, result.Status);
        return await FailedAsync(error);

        async Task<IResult> FailedAsync(string codeError)
        {
            // Ακόμη και οι αποτυχημένες υποβολές απαιτούν Manage πριν δημιουργηθεί draft.
            CatalogManageResult authorized = await manager.GetDetailsAsync(
                userId, tenantId, entity, id, context.RequestAborted);
            if (!authorized.IsAuthorized)
            {
                return Results.Forbid();
            }

            // Η προεπισκόπηση/επεξεργασία απαιτεί ξανά authorization μετά το redirect.
            var draft = new CatalogEditorDraft(userId, tenantId, id, view,
                code, name, categoryId, parentCategoryId, tracksStock, autoCode, codeError);
            Guid token = drafts.Save(draft);
            return Results.Redirect(
                $"/catalog/manage?view={view}&tenantId={tenantId:D}" +
                (id.HasValue ? $"&id={id.Value:D}" : "") +
                $"&draft={token:D}", permanent: false);
        }
    }

    private static bool TryIdentity(HttpContext context, out Guid userId)
    {
        userId = Guid.Empty;
        return context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
            userId != Guid.Empty;
    }
}
