using System.Security.Claims;
using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
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
        endpoints.MapPost("/ui/catalog/new-category", NewCategoryAsync)
            .WithMetadata(new RequestSizeLimitAttribute(8192))
            .RequireAuthorization();
        endpoints.MapGet("/ui/catalog/categories", SearchCategoriesAsync)
            .RequireAuthorization();
        endpoints.MapGet("/ui/catalog/children", ReadChildrenAsync)
            .RequireAuthorization();
        return endpoints;
    }

    /// <summary>Κρατά προσωρινά τη φόρμα είδους και ανοίγει νέα κατηγορία, χωρίς αποθήκευση είδους.</summary>
    private static async Task<IResult> NewCategoryAsync(HttpContext context,
        IAntiforgery antiforgery, CatalogWriteService manager, CatalogEditorDraftStore drafts,
        TenantAuthorizationService authorization)
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
            "products" => CatalogManagedEntity.Product,
            "services" => CatalogManagedEntity.Service,
            _ => (CatalogManagedEntity)(-1)
        };
        if (!Enum.IsDefined(entity) ||
            !Guid.TryParse(fields["tenantId"], out Guid tenantId) || tenantId == Guid.Empty)
        {
            return Results.BadRequest();
        }

        // Οποιοδήποτε κατάστημα διατηρείται μόνο μετά από server-side validation.
        Guid? storeId = null;
        string storeText = fields["storeId"].ToString();
        if (!string.IsNullOrWhiteSpace(storeText))
        {
            if (!Guid.TryParse(storeText, out Guid selectedStore) || selectedStore == Guid.Empty)
            {
                return Results.BadRequest();
            }

            storeId = selectedStore;
        }

        TenantAccessResult permission = await authorization.CheckAsync(
            userId, tenantId, storeId, TenantPermission.Manage, context.RequestAborted);
        if (permission.Status != TenantAccessStatus.Granted)
        {
            return Results.Forbid();
        }

        Guid? id = null;
        string idText = fields["id"].ToString();
        if (!string.IsNullOrEmpty(idText))
        {
            if (!Guid.TryParse(idText, out Guid existingId) || existingId == Guid.Empty)
            {
                return Results.BadRequest();
            }

            id = existingId;
        }

        string code = fields["code"].ToString();
        string name = fields["name"].ToString();
        string barcode = fields["barcode"].ToString();
        string longDescription = fields["longDescription"].ToString();
        if (code.Length > CatalogItem.MaxCodeLength + 8 ||
            name.Length > CatalogItem.MaxNameLength + 8 ||
            barcode.Length > CatalogItemDetails.MaxBarcodeLength + 8 ||
            longDescription.Length > CatalogItemDetails.MaxLongDescriptionLength + 8)
        {
            return Results.BadRequest();
        }

        Guid? categoryId = null;
        string selectedText = fields["categoryId"].ToString();
        if (!string.IsNullOrWhiteSpace(selectedText))
        {
            if (!Guid.TryParse(selectedText, out Guid selectedId) || selectedId == Guid.Empty)
            {
                return Results.BadRequest();
            }

            categoryId = selectedId;
        }

        CatalogManageResult access = await manager.GetDetailsAsync(
            userId, tenantId, entity, id, context.RequestAborted);
        if (!access.IsAuthorized)
        {
            return Results.Forbid();
        }

        if (id.HasValue && access.Details?.Record is null)
        {
            return Results.NotFound();
        }

        bool autoCode = !id.HasValue &&
            string.Equals(fields["codeMode"], "auto", StringComparison.Ordinal);
        bool tracksStock = entity == CatalogManagedEntity.Product &&
            string.Equals(fields["tracksStock"], "true", StringComparison.Ordinal);
        if (!TryCommercialFields(fields, entity, out var unitOfMeasure,
            out var accountingType))
        {
            return Results.BadRequest();
        }

        var draft = new CatalogEditorDraft(userId, tenantId, id, view, code, name,
            categoryId, null, tracksStock, autoCode, "", storeId,
            unitOfMeasure, accountingType, barcode, longDescription);
        Guid returnDraft = drafts.Save(draft);
        context.Response.Headers.CacheControl = "no-store";
        string storeQuery = storeId.HasValue ? $"&storeId={storeId.Value:D}" : "";
        return Results.Redirect(
            $"/catalog/manage?view=categories&tenantId={tenantId:D}" +
            storeQuery + $"&returnDraft={returnDraft:D}", permanent: false);
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
        CatalogEditorDraftStore drafts, ILoggerFactory loggerFactory,
        TenantAuthorizationService authorization)
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

        Guid? storeId = null;
        string storeText = fields["storeId"].ToString();
        if (!string.IsNullOrWhiteSpace(storeText))
        {
            if (!Guid.TryParse(storeText, out Guid selectedStore) || selectedStore == Guid.Empty)
            {
                return Results.BadRequest();
            }

            storeId = selectedStore;
        }

        TenantAccessResult storePermission = await authorization.CheckAsync(
            userId, tenantId, storeId, TenantPermission.Manage, context.RequestAborted);
        if (storePermission.Status != TenantAccessStatus.Granted)
        {
            return Results.Forbid();
        }

        // Η γρήγορη δημιουργία κατηγορίας επιστρέφει μόνο στον αρχικό εξουσιοδοτημένο editor.
        Guid? returnDraftToken = null;
        CatalogEditorDraft? returnEditor = null;
        string returnText = fields["returnDraft"].ToString();
        if (!string.IsNullOrWhiteSpace(returnText))
        {
            if (entity != CatalogManagedEntity.Category ||
                action != CatalogChangeAction.Create ||
                !Guid.TryParse(returnText, out Guid token) || token == Guid.Empty)
            {
                return Results.BadRequest();
            }

            returnEditor = drafts.ReadCategoryReturn(token, userId, tenantId);
            if (returnEditor is null)
            {
                return Results.BadRequest();
            }

            returnDraftToken = token;
        }

        bool writingFields = action is CatalogChangeAction.Create or CatalogChangeAction.Update;
        string code = fields["code"].ToString();
        string name = fields["name"].ToString();
        string barcode = fields["barcode"].ToString();
        string longDescription = fields["longDescription"].ToString();
        bool setBarcode = writingFields && fields.ContainsKey("barcode");
        bool setLongDescription = writingFields && fields.ContainsKey("longDescription");
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
        CatalogUnitOfMeasure? unitOfMeasure = null;
        CatalogAccountingType? accountingType = null;
        if (writingFields && !TryCommercialFields(fields, entity,
            out unitOfMeasure, out accountingType))
        {
            invalidField = "accounting";
        }

        if (code.Length > CatalogItem.MaxCodeLength + 8 ||
            name.Length > CatalogItem.MaxNameLength + 8 ||
            barcode.Length > CatalogItemDetails.MaxBarcodeLength + 8 ||
            longDescription.Length > CatalogItemDetails.MaxLongDescriptionLength + 8)
        {
            return Results.BadRequest();
        }

        // Συμβατότητα με ήδη ανοικτές φόρμες που έστελναν δύο ίδιες τιμές.
        // Διαφορετικές ή περισσότερες από δύο τιμές απορρίπτονται ως αμφίσημες.
        var saveModes = fields["saveMode"];
        string saveMode = saveModes.Count == 0 ? "" : saveModes[0] ?? "";
        if (saveModes.Count > 2 ||
            (saveModes.Count == 2 &&
                !string.Equals(saveModes[1], saveMode, StringComparison.Ordinal)) ||
            saveMode is not ("" or "close" or "new"))
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
            name, categoryId, tracksStock, parentCategoryId, autoCode, requestId,
            unitOfMeasure, accountingType,
            setBarcode ? barcode : null,
            setLongDescription ? longDescription : null,
            setBarcode, setLongDescription);
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
            if (returnEditor is not null && result.Id is { } createdCategoryId)
            {
                // Το νέο είδος/υπηρεσία δεν καταχωρείται ακόμη: απλώς προεπιλέγεται η κατηγορία.
                var resumed = returnEditor with { CategoryId = createdCategoryId, ErrorCode = "" };
                Guid resumedToken = drafts.Save(resumed);
                string originalId = resumed.Id.HasValue ? $"&id={resumed.Id.Value:D}" : "";
                string resumeStore = resumed.StoreId.HasValue
                    ? $"&storeId={resumed.StoreId.Value:D}" : "";
                return Results.Redirect(
                    $"/catalog/manage?view={resumed.View}&tenantId={tenantId:D}" +
                    resumeStore + originalId + $"&draft={resumedToken:D}", permanent: false);
            }

            string storeQuery = storeId.HasValue ? $"&storeId={storeId.Value:D}" : "";
            string destination = saveMode == "new" &&
                action is CatalogChangeAction.Create or CatalogChangeAction.Update
                ? $"/catalog/manage?view={view}&tenantId={tenantId:D}{storeQuery}&savedCode={savedCode}"
                : $"/catalog?view={view}&tenantId={tenantId:D}{storeQuery}&saved=1&savedCode={savedCode}";
            return Results.Redirect(destination, permanent: false);
        }

        string error = result.Error == "catalog.item.barcode.duplicate" ? "duplicateBarcode" :
            result.Status == CatalogChangeStatus.Duplicate ? "duplicate" :
            result.Error switch
            {
                "catalog.parent.invalid" => "parent",
                "catalog.category.in_use" => "inuse",
                "catalog.category.inactive_or_missing" => "category",
                "catalog.item.barcode.invalid" or "catalog.item.barcode.check_digit" => "barcode",
                "catalog.item.description.invalid" => "description",
                "catalog.item.unit.invalid" => "unit",
                "catalog.item.accounting.invalid" or
                "catalog.item.service.profile.invalid" or
                "catalog.item.product.profile.invalid" => "accounting",
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
                code, name, categoryId, parentCategoryId, tracksStock, autoCode,
                codeError, storeId, unitOfMeasure, accountingType,
                barcode, longDescription);
            Guid token = drafts.Save(draft);
            return Results.Redirect(
                $"/catalog/manage?view={view}&tenantId={tenantId:D}" +
                (id.HasValue ? $"&id={id.Value:D}" : "") +
                (storeId.HasValue ? $"&storeId={storeId.Value:D}" : "") +
                $"&draft={token:D}" +
                (returnDraftToken.HasValue ? $"&returnDraft={returnDraftToken.Value:D}" : ""),
                permanent: false);
        }
    }

    /// <summary>Αποδέχεται μόνο καθορισμένες μονάδες και τύπους, χωρίς φορολογική αντιστοίχιση.</summary>
    private static bool TryCommercialFields(IFormCollection fields,
        CatalogManagedEntity entity, out CatalogUnitOfMeasure? unit,
        out CatalogAccountingType? accounting)
    {
        unit = null;
        accounting = null;
        string unitText = fields["unitOfMeasure"].ToString();
        string accountingText = fields["accountingType"].ToString();

        if (entity == CatalogManagedEntity.Category)
        {
            return unitText.Length == 0 && accountingText.Length == 0;
        }

        // Συμβατότητα παλιών φορμών/CLI: ο Domain διατηρεί τις προεπιλογές.
        if (unitText.Length > 0)
        {
            if (!int.TryParse(unitText, out int value) ||
                !Enum.IsDefined((CatalogUnitOfMeasure)value))
            {
                return false;
            }

            unit = (CatalogUnitOfMeasure)value;
        }

        if (accountingText.Length > 0)
        {
            if (!int.TryParse(accountingText, out int value) ||
                !Enum.IsDefined((CatalogAccountingType)value))
            {
                return false;
            }

            accounting = (CatalogAccountingType)value;
        }

        return true;
    }

    private static bool TryIdentity(HttpContext context, out Guid userId)
    {
        userId = Guid.Empty;
        return context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
            userId != Guid.Empty;
    }
}
