using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;

namespace Aenaon.Application.Menus;

/// <summary>Αποτέλεσμα εμπορικής προεπισκόπησης προσχεδίου χωρίς δημοσίευση.</summary>
public enum MenuPreviewStatus
{
    Ready,
    Denied,
    Invalid,
    Missing,
    TooLarge
}

/// <summary>Κατάσταση ενός είδους στο επιλεγμένο Store/Channel/BusinessDate.</summary>
public enum MenuPreviewItemStatus
{
    Resolved,
    MissingPrice,
    AmbiguousPrice,
    InactiveCatalogItem,
    HiddenCategory,
    InvalidCategory
}

public sealed record MenuPreviewPriceCandidate(Guid CatalogItemId,
    PriceResolutionCandidate Candidate);

/// <summary>Ομαδική αναζήτηση τιμών για αποφυγή N+1 αιτημάτων στη SQL.</summary>
public interface IMenuPreviewPricingStore
{
    Task<IReadOnlyList<MenuPreviewPriceCandidate>> FindCandidatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> catalogItemIds,
        Guid storeId, PriceListChannel channel, DateOnly businessDate,
        CancellationToken cancellationToken = default);
}

public sealed record MenuPreviewItem(
    Guid ItemId, Guid CatalogItemId, Guid? CategoryId, string Code, string Name,
    int Position, MenuPreviewItemStatus Status,
    decimal? UnitPrice = null, string? Currency = null, bool? PricesIncludeVat = null,
    Guid? PriceListId = null, string? PriceListCode = null,
    IReadOnlyList<Guid>? ConflictingPriceListIds = null);

public sealed record MenuPreviewResult(
    MenuPreviewStatus Status, MenuDraftSummary? Menu = null,
    IReadOnlyList<MenuDraftCategoryRecord>? Categories = null,
    IReadOnlyList<MenuPreviewItem>? Items = null)
{
    public int ResolvedCount => Items?.Count(x => x.Status == MenuPreviewItemStatus.Resolved) ?? 0;
    public int BlockedCount => (Items?.Count ?? 0) - ResolvedCount;
    public bool HasBlockingIssues => BlockedCount > 0;
}

/// <summary>
/// Προεπισκόπηση υφιστάμενου προσχεδίου με τιμές του κεντρικού ERP.
/// Δεν εφαρμόζει ωράρια, φορολογική πολιτική, δημοσίευση ή SQL εγγραφές.
/// </summary>
public sealed class MenuPreviewService(
    TenantAuthorizationService authorization, IMenuDraftStore menus,
    IMenuPreviewPricingStore pricing)
{
    public async Task<MenuPreviewResult> PreviewAsync(
        Guid userId, Guid tenantId, Guid menuId, Guid storeId,
        PriceListChannel channel, DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty || tenantId == Guid.Empty || menuId == Guid.Empty ||
            storeId == Guid.Empty || businessDate == default ||
            !Enum.IsDefined(channel) || channel == PriceListChannel.All)
        {
            return new(MenuPreviewStatus.Invalid);
        }

        TenantAccessResult access = await authorization.CheckAsync(
            userId, tenantId, storeId, TenantPermission.Read, cancellationToken);
        if (access.Status != TenantAccessStatus.Granted)
        {
            return new(MenuPreviewStatus.Denied);
        }

        MenuDraftDetails? draft = await menus.GetAsync(tenantId, menuId, cancellationToken);
        if (draft is null)
        {
            return new(MenuPreviewStatus.Missing);
        }
        if (draft.Items.Count > 10000 || draft.Categories.Count > 10000)
        {
            return new(MenuPreviewStatus.TooLarge);
        }

        Dictionary<Guid, MenuDraftCategoryRecord> categories =
            draft.Categories.ToDictionary(x => x.CategoryId);

        bool IsVisible(Guid? categoryId, out bool valid)
        {
            valid = true;
            var seen = new HashSet<Guid>();
            int depth = 0;
            while (categoryId.HasValue)
            {
                if (depth++ >= 10000 || !seen.Add(categoryId.Value) ||
                    !categories.TryGetValue(categoryId.Value,
                        out MenuDraftCategoryRecord? category))
                {
                    valid = false;
                    return false;
                }
                if (!category.IsVisible)
                {
                    return false;
                }
                categoryId = category.ParentCategoryId;
            }
            return true;
        }

        // Προτεραιότητα σε ακεραιότητα/ορατότητα πριν από οποιαδήποτε ανάγνωση τιμών.
        var itemState = new Dictionary<Guid, MenuPreviewItemStatus>();
        var pricedIds = new HashSet<Guid>();
        foreach (MenuDraftItemRecord item in draft.Items)
        {
            bool visible = IsVisible(item.CategoryId, out bool valid);
            MenuPreviewItemStatus status = !valid ? MenuPreviewItemStatus.InvalidCategory
                : !visible ? MenuPreviewItemStatus.HiddenCategory
                : !item.IsCatalogActive ? MenuPreviewItemStatus.InactiveCatalogItem
                : MenuPreviewItemStatus.MissingPrice;
            itemState[item.ItemId] = status;
            if (status == MenuPreviewItemStatus.MissingPrice)
            {
                pricedIds.Add(item.CatalogItemId);
            }
        }

        IReadOnlyList<MenuPreviewPriceCandidate> candidates = pricedIds.Count == 0
            ? []
            : await pricing.FindCandidatesAsync(tenantId, pricedIds, storeId,
                channel, businessDate, cancellationToken);
        ILookup<Guid, PriceResolutionCandidate> byItem =
            candidates.Where(x => pricedIds.Contains(x.CatalogItemId))
                .ToLookup(x => x.CatalogItemId, x => x.Candidate);

        var rows = new List<MenuPreviewItem>(draft.Items.Count);
        foreach (MenuDraftItemRecord item in draft.Items)
        {
            MenuPreviewItemStatus status = itemState[item.ItemId];
            PriceSelectionResult? selected = null;
            if (status == MenuPreviewItemStatus.MissingPrice)
            {
                selected = PriceResolutionPolicy.Select(byItem[item.CatalogItemId],
                    storeId, channel, businessDate);
                status = selected.Status switch
                {
                    PriceSelectionStatus.Unique => MenuPreviewItemStatus.Resolved,
                    PriceSelectionStatus.Ambiguous => MenuPreviewItemStatus.AmbiguousPrice,
                    _ => MenuPreviewItemStatus.MissingPrice
                };
            }
            rows.Add(new(item.ItemId, item.CatalogItemId, item.CategoryId,
                item.CatalogCode, item.NameOverride ?? item.CatalogName, item.Position,
                status, selected?.Selected?.UnitPrice, selected?.Selected?.Currency,
                selected?.Selected?.PricesIncludeVat, selected?.Selected?.PriceListId,
                selected?.Selected?.PriceListCode, selected?.ConflictingPriceListIds));
        }
        return new(MenuPreviewStatus.Ready, draft.Menu, draft.Categories,
            rows.OrderBy(x => x.CategoryId).ThenBy(x => x.Position)
                .ThenBy(x => x.ItemId).ToArray());
    }
}
