using System.Globalization;
using System.Security.Claims;
using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Aenaon.Api.Ui;

/// <summary>Αυστηρά SSR, CSRF-protected endpoints διαχείρισης εμπορικών τιμών.</summary>
public static class PriceListManagementEndpoints
{
    public static IEndpointRouteBuilder MapPriceListManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ui/price-lists/save", SaveAsync)
            .WithMetadata(new RequestSizeLimitAttribute(16384))
            .RequireAuthorization();
        endpoints.MapPost("/ui/price-lists/prices", SavePricesAsync)
            .WithMetadata(new RequestSizeLimitAttribute(16384))
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SaveAsync(HttpContext context,
        IAntiforgery csrf, PriceListManagementService service,
        TenantAuthorizationService authorization, ILoggerFactory loggerFactory)
    {
        if (!await VerifyRequestAsync(context, csrf) ||
            !TryIdentity(context, out Guid userId))
        {
            return Results.BadRequest();
        }

        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!TryGuid(form, "tenantId", required: true, out Guid? tenant) ||
            !TryGuid(form, "storeId", required: false, out Guid? currentStore) ||
            !TryGuid(form, "id", required: false, out Guid? id) ||
            !TryGuid(form, "scopeStoreId", required: false, out Guid? scopeStore))
        {
            return Results.BadRequest();
        }

        if (tenant is null)
        {
            return Results.BadRequest();
        }
        if (!TrySingle(form, "action", out string actionText))
        {
            return Results.BadRequest();
        }
        PriceListAction action = actionText switch
        {
            "create" => PriceListAction.Create,
            "update" => PriceListAction.Update,
            "activate" => PriceListAction.Activate,
            "deactivate" => PriceListAction.Deactivate,
            _ => (PriceListAction)(-1)
        };
        if (!Enum.IsDefined(action) ||
            (action == PriceListAction.Create && id.HasValue) ||
            (action != PriceListAction.Create && !id.HasValue))
        {
            return Results.BadRequest();
        }

        TenantAccessResult permission = await authorization.CheckAsync(userId,
            tenant.Value, currentStore, TenantPermission.Manage, context.RequestAborted);
        if (permission.Status != TenantAccessStatus.Granted)
        {
            return Results.Forbid();
        }

        string? code = null;
        string? name = null;
        DateOnly from = default;
        DateOnly? until = null;
        PriceListChannel channel = PriceListChannel.All;
        bool includeVat = true;
        if (action is PriceListAction.Create or PriceListAction.Update)
        {
            if (!TrySingle(form, "name", out string nameValue) ||
                !TrySingle(form, "validFrom", out string fromValue) ||
                !DateOnly.TryParseExact(fromValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out from) ||
                nameValue.Length > PriceList.MaxNameLength + 8 ||
                !TrySingle(form, "validUntil", out string untilText))
            {
                return Results.BadRequest();
            }

            name = nameValue;
            if (untilText.Length > 0)
            {
                if (!DateOnly.TryParseExact(untilText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly end))
                {
                    return Results.BadRequest();
                }
                until = end;
            }

            if (action == PriceListAction.Create)
            {
                if (!TrySingle(form, "code", out string codeValue) ||
                    codeValue.Length > PriceList.MaxCodeLength + 8 ||
                    !TrySingle(form, "channel", out string channelText) ||
                    !Enum.TryParse(channelText, false, out channel) || !Enum.IsDefined(channel) ||
                    !TrySingle(form, "includeVat", out string includeText) ||
                    !bool.TryParse(includeText, out includeVat))
                {
                    return Results.BadRequest();
                }

                code = codeValue;
            }
            else if (scopeStore.HasValue)
            {
                // Η επιχείρηση/κατάστημα δεν μπορούν να μεταβληθούν μέσω update.
                return Results.BadRequest();
            }
        }

        PriceListResult result = await service.ApplyAsync(userId, tenant.Value,
            new PriceListChange(action, id, code, name,
                action == PriceListAction.Create ? scopeStore : null,
                channel, includeVat, from, until), context.RequestAborted);
        loggerFactory.CreateLogger(nameof(PriceListManagementEndpoints)).LogInformation(
            "Price list SSR action {Action} {Status} user {UserId} tenant {TenantId} list {ListId}",
            action, result.Status, userId, tenant.Value, result.Id ?? id);

        if (result.Status == PriceListStatus.Denied)
        {
            return Results.Forbid();
        }
        if (result.Status == PriceListStatus.Missing)
        {
            return Results.NotFound();
        }
        context.Response.Headers.CacheControl = "no-store";
        string url = EditorUrl(tenant.Value, currentStore, result.Id ?? id);
        if (result.Status == PriceListStatus.Saved)
        {
            return Results.Redirect(url + "&saved=1");
        }

        string error = result.Status == PriceListStatus.Duplicate ? "duplicate" : "invalid";
        return Results.Redirect(url + "&error=" + error);
    }

    private static async Task<IResult> SavePricesAsync(HttpContext context,
        IAntiforgery csrf, PriceListManagementService service,
        TenantAuthorizationService authorization)
    {
        if (!await VerifyRequestAsync(context, csrf) ||
            !TryIdentity(context, out Guid userId))
        {
            return Results.BadRequest();
        }

        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!TryGuid(form, "tenantId", required: true, out Guid? tenant) ||
            !TryGuid(form, "storeId", required: false, out Guid? currentStore) ||
            !TryGuid(form, "id", required: true, out Guid? id) ||
            tenant is null || id is null ||
            !TrySingle(form, "search", out string search) ||
            !TrySingle(form, "page", out string pageText) ||
            !int.TryParse(pageText, NumberStyles.None, CultureInfo.InvariantCulture, out int page) ||
            page is < 1 or > 100_000 || search.Length > 80 || search.Any(char.IsControl))
        {
            return Results.BadRequest();
        }

        TenantAccessResult permission = await authorization.CheckAsync(userId,
            tenant.Value, currentStore, TenantPermission.Manage, context.RequestAborted);
        if (permission.Status != TenantAccessStatus.Granted)
        {
            return Results.Forbid();
        }

        var changes = new List<PriceListPriceChange>();
        foreach (KeyValuePair<string, StringValues> field in form)
        {
            if (!field.Key.StartsWith("price_", StringComparison.Ordinal))
            {
                continue;
            }

            if (field.Value.Count != 1 ||
                !Guid.TryParseExact(field.Key["price_".Length..], "D", out Guid itemId) ||
                itemId == Guid.Empty || changes.Count >= 50)
            {
                return Results.BadRequest();
            }

            string amountText = field.Value.ToString();
            if (string.IsNullOrWhiteSpace(amountText))
            {
                continue;
            }
            if (!decimal.TryParse(amountText,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal price))
            {
                return Results.Redirect(EditorUrl(tenant.Value, currentStore, id) +
                    $"&page={page}&error=invalid");
            }

            changes.Add(new(itemId, price));
        }

        string destination = EditorUrl(tenant.Value, currentStore, id) +
            $"&page={page}" + (search.Length > 0 ? "&search=" + Uri.EscapeDataString(search) : "");
        if (changes.Count == 0)
        {
            return Results.Redirect(destination + "&error=invalid");
        }

        PriceListResult result = await service.SetPricesAsync(
            userId, tenant.Value, id.Value, changes, context.RequestAborted);
        loggerFactory.CreateLogger(nameof(PriceListManagementEndpoints)).LogInformation(
            "Price list SSR prices {Status} user {UserId} tenant {TenantId} list {ListId} rows {Count}",
            result.Status, userId, tenant.Value, id.Value, changes.Count);
        if (result.Status == PriceListStatus.Denied)
        {
            return Results.Forbid();
        }
        if (result.Status == PriceListStatus.Missing)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "no-store";
        return Results.Redirect(destination + (result.Status == PriceListStatus.Saved ?
            "&saved=1" : result.Error == "pricelist.inactive" ?
                "&error=inactive" : "&error=invalid"));
    }

    private static async Task<bool> VerifyRequestAsync(HttpContext context,
        IAntiforgery csrf) =>
        context.Request.IsHttps && context.Request.HasFormContentType &&
        context.Request.ContentLength <= 16384 &&
        await csrf.IsRequestValidAsync(context);

    private static bool TrySingle(IFormCollection form, string key, out string value)
    {
        StringValues values = form[key];
        value = values.ToString();
        return values.Count <= 1;
    }

    private static bool TryGuid(IFormCollection form, string key, bool required, out Guid? id)
    {
        id = null;
        if (!TrySingle(form, key, out string value))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            return !required;
        }
        if (!Guid.TryParse(value, out Guid parsed) || parsed == Guid.Empty)
        {
            return false;
        }
        id = parsed;
        return true;
    }

    private static bool TryIdentity(HttpContext context, out Guid userId) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
        userId != Guid.Empty;

    private static string EditorUrl(Guid tenant, Guid? store, Guid? id)
    {
        string url = $"/price-lists/manage?tenantId={tenant:D}";
        if (store.HasValue)
        {
            url += $"&storeId={store.Value:D}";
        }
        if (id.HasValue)
        {
            url += $"&id={id.Value:D}";
        }
        return url;
    }
}
