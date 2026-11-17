using System.Globalization;
using System.Security.Claims;
using Aenaon.Application.Menus;
using Aenaon.Application.Tenancy;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Aenaon.Api.Ui;

/// <summary>Μόνο ασφαλείς HTTPS/CSRF POST ενέργειες πρόχειρου μενού.</summary>
public static class MenuDraftManagementEndpoints
{
    public static IEndpointRouteBuilder MapMenuDraftManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ui/menus/draft", SaveAsync)
            .WithMetadata(new RequestSizeLimitAttribute(16384))
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SaveAsync(
        HttpContext context, IAntiforgery csrf,
        MenuDraftManagementService service, TenantAuthorizationService authorization,
        ILoggerFactory loggerFactory)
    {
        if (!context.Request.IsHttps || !context.Request.HasFormContentType ||
            context.Request.ContentLength > 16384 ||
            !await csrf.IsRequestValidAsync(context) ||
            !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId) ||
            userId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!TryGuid(form, "tenantId", required: true, out Guid? tenantId) ||
            !TryGuid(form, "storeId", required: false, out Guid? storeId) ||
            !TryGuid(form, "menuId", required: false, out Guid? menuId) ||
            !TryGuid(form, "categoryId", required: false, out Guid? categoryId) ||
             !TryGuid(form, "parentCategoryId", required: false, out Guid? parentCategoryId) ||
             !TryGuid(form, "returnCategoryId", required: false, out Guid? returnCategoryId) ||
            !TryGuid(form, "itemId", required: false, out Guid? itemId) ||
            !TryGuid(form, "catalogItemId", required: false, out Guid? catalogItemId) ||
            !TryGuid(form, "requestId", required: true, out Guid? requestId) ||
            !TrySingle(form, "action", out string actionText) ||
            !TrySingle(form, "name", out string name) ||
            !TrySingle(form, "code", out string code) ||
            !TrySingle(form, "nameOverride", out string nameOverride) ||
            !TrySingle(form, "expectedRevision", out string revisionText) ||
            !TrySingle(form, "targetPosition", out string positionText) ||
            !TrySingle(form, "isVisible", out string visibleText) ||
            tenantId is null || requestId is null)
        {
            return Results.BadRequest();
        }

        MenuDraftAction action = actionText switch
        {
            "create-menu" => MenuDraftAction.CreateMenu,
            "import-catalog" => MenuDraftAction.ImportCatalogContents,
            "update-menu" => MenuDraftAction.UpdateMenu,
            "add-category" => MenuDraftAction.AddCategory,
            "update-category" => MenuDraftAction.UpdateCategory,
            "move-category" => MenuDraftAction.MoveCategory,
            "remove-category" => MenuDraftAction.RemoveCategory,
            "add-item" => MenuDraftAction.AddItem,
            "rename-item" => MenuDraftAction.RenameItem,
            "move-item" => MenuDraftAction.MoveItem,
            "remove-item" => MenuDraftAction.RemoveItem,
            _ => (MenuDraftAction)(-1)
        };
        if (!Enum.IsDefined(action))
        {
            return Results.BadRequest();
        }

        long? revision = null;
        if (!string.IsNullOrWhiteSpace(revisionText))
        {
            if (!long.TryParse(revisionText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long parsedRevision) ||
                parsedRevision < 1)
            {
                return Results.BadRequest();
            }
            revision = parsedRevision;
        }

        int? position = null;
        if (!string.IsNullOrWhiteSpace(positionText))
        {
            if (!int.TryParse(positionText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int parsedPosition) ||
                parsedPosition < 1 || parsedPosition > 10000)
            {
                return Results.BadRequest();
            }
            position = parsedPosition;
        }

        bool? visible = null;
        if (!string.IsNullOrEmpty(visibleText))
        {
            if (!bool.TryParse(visibleText, out bool parsedVisible))
            {
                return Results.BadRequest();
            }
            visible = parsedVisible;
        }

        TenantAccessResult access = await authorization.CheckAsync(
            userId, tenantId.Value, storeId, TenantPermission.Manage, context.RequestAborted);
        if (access.Status != TenantAccessStatus.Granted)
        {
            return Results.Forbid();
        }

        var command = new MenuDraftCommand(
            action, requestId.Value, menuId, revision, categoryId, itemId,
            catalogItemId, code, name, nameOverride, position, visible,
            ParentCategoryId: parentCategoryId);

        try
        {
            MenuDraftMutationResult result = await service.ApplyAsync(
                userId, tenantId.Value, command, context.RequestAborted);
            if (result.Status == MenuDraftStatus.Denied)
            {
                return Results.Forbid();
            }

            string state = result.Status switch
            {
                MenuDraftStatus.Saved => "saved",
                MenuDraftStatus.Duplicate when result.Error == "menu.catalog.import_not_empty" => "import-not-empty",
                MenuDraftStatus.Duplicate => "duplicate",
                MenuDraftStatus.Conflict => "conflict",
                MenuDraftStatus.Missing => "missing",
                MenuDraftStatus.Invalid when result.Error == "menu.catalog.no_active_categories" => "empty-catalog",
                MenuDraftStatus.Invalid when result.Error == "menu.catalog.import_limit" => "import-limit",
                _ => "invalid"
            };

            string tab = action switch
            {
                MenuDraftAction.AddItem or MenuDraftAction.RenameItem or
                    MenuDraftAction.MoveItem or MenuDraftAction.RemoveItem => "items",
                _ => "categories"
            };

            Guid? targetMenu = result.Status == MenuDraftStatus.Saved
                ? result.MenuId ?? menuId : menuId;
            Guid? destinationCategory = action == MenuDraftAction.AddCategory
                ? parentCategoryId : categoryId;
            if (action is MenuDraftAction.UpdateCategory or MenuDraftAction.MoveCategory or
                MenuDraftAction.RemoveCategory && form.ContainsKey("returnCategoryId"))
            {
                destinationCategory = returnCategoryId;
            }
            if (action is MenuDraftAction.AddItem or MenuDraftAction.RenameItem or
                MenuDraftAction.MoveItem or MenuDraftAction.RemoveItem)
            {
                tab = "items";
            }
            else if (action is MenuDraftAction.AddCategory or MenuDraftAction.UpdateCategory or
                MenuDraftAction.MoveCategory or MenuDraftAction.RemoveCategory)
            {
                tab = destinationCategory.HasValue ? "items" : "categories";
            }
            return Results.Redirect(BuildUrl(tenantId.Value, storeId, targetMenu,
                destinationCategory, tab, state));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("Aenaon.MenuDraftManagementEndpoints")
                .LogError(exception,
                    "Menu draft endpoint failed {UserId} {TenantId} {Action}",
                    userId, tenantId.Value, action);
            return Results.Problem("Δεν ήταν δυνατή η αποθήκευση του μενού.");
        }
    }

    private static string BuildUrl(Guid tenantId, Guid? storeId, Guid? menuId,
        Guid? categoryId, string tab, string result)
    {
        string url = $"/menus?tenantId={tenantId:D}";
        if (storeId.HasValue)
        {
            url += $"&storeId={storeId.Value:D}";
        }
        if (menuId.HasValue)
        {
            url += $"&menuId={menuId.Value:D}";
        }
        if (categoryId.HasValue)
        {
            url += $"&categoryId={categoryId.Value:D}";
        }
        return url + $"&tab={tab}&result={result}";
    }

    private static bool TrySingle(IFormCollection form, string field, out string value)
    {
        StringValues values = form[field];
        value = values.ToString();
        return values.Count <= 1;
    }

    private static bool TryGuid(IFormCollection form, string field, bool required, out Guid? value)
    {
        value = null;
        if (!TrySingle(form, field, out string text))
        {
            return false;
        }
        if (text.Length == 0)
        {
            return !required;
        }
        if (!Guid.TryParse(text, out Guid parsed) || parsed == Guid.Empty)
        {
            return false;
        }
        value = parsed;
        return true;
    }
}
