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
        endpoints.MapPost("/ui/price-lists/remove-price", RemovePriceAsync)
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
        Guid? expectedStore = null;
        PriceListChannel? expectedChannel = null;
        bool? expectedIncludeVat = null;
        bool confirmPolicyChange = false;
        if (action is PriceListAction.Create or PriceListAction.Update)
        {
            if (!TrySingle(form, "name", out string nameValue) ||
                !TrySingle(form, "validFrom", out string fromValue) ||
                !DateOnly.TryParseExact(fromValue, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out from) ||
                nameValue.Length > PriceList.MaxNameLength + 8 ||
                !TrySingle(form, "validUntil", out string untilText))
            {
                return Results.BadRequest();
            }

            name = nameValue;
            if (untilText.Length > 0)
            {
                if (!DateOnly.TryParseExact(untilText, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly end))
                {
                    return Results.BadRequest();
                }
                until = end;
            }

            if (!TrySingle(form, "channel", out string channelText) ||
                !Enum.TryParse(channelText, false, out channel) || !Enum.IsDefined(channel) ||
                !TrySingle(form, "includeVat", out string includeText) ||
                !bool.TryParse(includeText, out includeVat))
            {
                return Results.BadRequest();
            }

            if (action == PriceListAction.Create)
            {
                if (!TrySingle(form, "code", out string codeValue) ||
                    codeValue.Length > PriceList.MaxCodeLength + 8)
                {
                    return Results.BadRequest();
                }

                code = codeValue;
            }
            else
            {
                if (!form.ContainsKey("expectedScopeStoreId") ||
                    !TryGuid(form, "expectedScopeStoreId", required: false, out expectedStore) ||
                    !TrySingle(form, "expectedChannel", out string previousChannel) ||
                    !Enum.TryParse(previousChannel, false, out PriceListChannel parsedChannel) ||
                    !Enum.IsDefined(parsedChannel) ||
                    !TrySingle(form, "expectedIncludeVat", out string previousBasis) ||
                    !bool.TryParse(previousBasis, out bool parsedBasis))
                {
                    return Results.BadRequest();
                }

                expectedChannel = parsedChannel;
                expectedIncludeVat = parsedBasis;
                if (form.ContainsKey("confirmPolicyChange"))
                {
                    if (!TrySingle(form, "confirmPolicyChange", out string confirmText) ||
                        confirmText != "yes")
                    {
                        return Results.BadRequest();
                    }

                    confirmPolicyChange = true;
                }
            }
        }

        PriceListResult result = await service.ApplyAsync(userId, tenant.Value,
            new PriceListChange(action, id, code, name,
                action is PriceListAction.Create or PriceListAction.Update ? scopeStore : null,
                channel, includeVat, from, until,
                EditCommercialProfile: action == PriceListAction.Update,
                ConfirmPolicyChange: confirmPolicyChange,
                ExpectedStoreId: expectedStore,
                ExpectedChannel: expectedChannel,
                ExpectedPricesIncludeVat: expectedIncludeVat), context.RequestAborted);
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

        string error = result.Status switch
        {
            PriceListStatus.Duplicate => "duplicate",
            _ when result.Error == "pricelist.overlap.conflict" => "overlap",
            PriceListStatus.Conflict => "conflict",
            _ when result.Error == "pricelist.policy.confirm_required" => "confirm-policy",
            _ => "invalid"
        };
        return Results.Redirect(url + "&error=" + error);
    }

    private static async Task<IResult> SavePricesAsync(HttpContext context,
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

            // Ελέγχουμε την αρχική τιμή για να μην αντικατασταθούν ξένες νεότερες αλλαγές.
            string expectedField = "expected_" + field.Key["price_".Length..];
            if (!form.ContainsKey(expectedField) ||
                !TrySingle(form, expectedField, out string originalText))
            {
                return Results.BadRequest();
            }

            decimal? expected = null;
            if (!string.IsNullOrWhiteSpace(originalText))
            {
                if (!decimal.TryParse(originalText,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out decimal parsedExpected))
                {
                    return Results.BadRequest();
                }

                expected = parsedExpected;
            }

            string amountText = field.Value.ToString();
            if (string.IsNullOrWhiteSpace(amountText))
            {
                // Ένα κενό πεδίο δεν αφαιρεί ούτε αντικαθιστά αποθηκευμένη τιμή.
                continue;
            }

            if (!decimal.TryParse(amountText,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal price))
            {
                return Results.Redirect(EditorUrl(tenant.Value, currentStore, id) +
                    $"&page={page}&error=invalid");
            }

            if (expected == price)
            {
                continue;
            }

            changes.Add(new(itemId, price, expected, CheckExpected: true));
        }

        string destination = EditorUrl(tenant.Value, currentStore, id) +
            $"&page={page}" + (search.Length > 0 ? "&search=" + Uri.EscapeDataString(search) : "");
        if (changes.Count == 0)
        {
            return Results.Redirect(destination + "&unchanged=1");
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
            "&saved=1" : result.Error == "pricelist.overlap.conflict" ?
                "&error=overlap" : result.Status == PriceListStatus.Conflict ?
                "&error=conflict" : result.Error == "pricelist.inactive" ?
                    "&error=inactive" : "&error=invalid"));
    }

    /// <summary>Ρητή αφαίρεση μίας τιμής με Owner, CSRF και έλεγχο παρωχημένων δεδομένων.</summary>
    private static async Task<IResult> RemovePriceAsync(HttpContext context,
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
            !TryGuid(form, "id", required: true, out Guid? id) ||
            !TryGuid(form, "catalogItemId", required: true, out Guid? itemId) ||
            !TrySingle(form, "expectedPrice", out string priceText) ||
            !decimal.TryParse(priceText,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal expectedPrice) ||
            !TrySingle(form, "search", out string search) ||
            search.Length > 80 || search.Any(char.IsControl) ||
            !TrySingle(form, "page", out string pageText) ||
            !int.TryParse(pageText, NumberStyles.None, CultureInfo.InvariantCulture, out int page) ||
            page is < 1 or > 100_000 || tenant is null || id is null || itemId is null)
        {
            return Results.BadRequest();
        }

        TenantAccessResult permission = await authorization.CheckAsync(userId,
            tenant.Value, currentStore, TenantPermission.Manage, context.RequestAborted);
        if (permission.Status != TenantAccessStatus.Granted)
        {
            return Results.Forbid();
        }

        PriceListResult result = await service.RemovePriceAsync(userId, tenant.Value,
            id.Value, itemId.Value, expectedPrice, context.RequestAborted);
        loggerFactory.CreateLogger(nameof(PriceListManagementEndpoints)).LogInformation(
            "Price list price removal {Status} by {UserId} tenant {TenantId} list {PriceListId} item {CatalogItemId}",
            result.Status, userId, tenant.Value, id.Value, itemId.Value);
        if (result.Status == PriceListStatus.Denied)
        {
            return Results.Forbid();
        }

        string destination = EditorUrl(tenant.Value, currentStore, id) +
            $"&page={page}" + (search.Length > 0 ? "&search=" + Uri.EscapeDataString(search) : "");
        context.Response.Headers.CacheControl = "no-store";
        return Results.Redirect(destination + (result.Status switch
        {
            PriceListStatus.Saved => "&removed=1",
            PriceListStatus.Conflict or PriceListStatus.Missing => "&error=conflict",
            _ => "&error=invalid"
        }));
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
