using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;

namespace Aenaon.Application.Catalog;

/// <summary>Αποτέλεσμα ανάγνωσης τιμής χωρίς φορολογική ή οικονομική εγγραφή.</summary>
public enum PriceResolutionStatus
{
    Resolved,
    NotPriced,
    Ambiguous,
    ItemUnavailable,
    Denied,
    Invalid
}

/// <summary>Μη αποθηκευμένη προεπισκόπηση αμετάβλητης εμπορικής τιμής.</summary>
public sealed record PriceSnapshotPreview(PriceResolutionResult Resolution,
    CommercialPriceSnapshot? Snapshot);

public sealed record PriceResolutionResult(
    PriceResolutionStatus Status,
    Guid? PriceListId = null,
    string? PriceListCode = null,
    decimal? UnitPrice = null,
    bool? PricesIncludeVat = null,
    string? Currency = null,
    IReadOnlyList<Guid>? ConflictingPriceListIds = null,
    Guid? PriceListStoreId = null,
    PriceListChannel? PriceListChannel = null);

/// <summary>Μόνο ανάγνωση υποψήφιων τιμών εντός συγκεκριμένου Tenant.</summary>
public interface IPriceResolutionStore
{
    Task<bool> IsActiveCatalogItemAsync(Guid tenantId, Guid itemId, CancellationToken token);

    Task<IReadOnlyList<PriceResolutionCandidate>> FindCandidatesAsync(
        Guid tenantId, Guid itemId, Guid? storeId, PriceListChannel channel,
        DateOnly businessDate, CancellationToken token);
}

/// <summary>
/// Επιλύει εμπορική τιμή μόνο για προεπισκόπηση, με δικαιώματα Tenant/Store.
/// Δεν εφαρμόζει ΦΠΑ, δεν δημιουργεί παραγγελία και δεν δεσμεύει ιστορική τιμή.
/// </summary>
public sealed class PriceResolutionService(
    TenantAuthorizationService authorization, IPriceResolutionStore store)
{
    /// <summary>
    /// Προβάλλει immutable τιμή μόνο μετά την εξουσιοδοτημένη επίλυση.
    /// Η δημιουργία αντικειμένου δεν καταχωρίζει παραγγελία ούτε δεσμεύει SQL δεδομένα.
    /// </summary>
    public async Task<PriceSnapshotPreview> PreviewSnapshotAsync(
        Guid userId, Guid tenantId, Guid itemId, Guid? storeId,
        PriceListChannel channel, DateOnly businessDate,
        CancellationToken token = default)
    {
        PriceResolutionResult resolution = await ResolveAsync(
            userId, tenantId, itemId, storeId, channel, businessDate, token);
        if (resolution.Status != PriceResolutionStatus.Resolved)
        {
            return new(resolution, null);
        }

        CommercialPriceSnapshot snapshot = CommercialPriceSnapshot.Capture(
            tenantId, storeId, itemId,
            resolution.PriceListId!.Value, resolution.PriceListCode,
            resolution.PriceListStoreId, resolution.PriceListChannel!.Value,
            channel, businessDate, resolution.UnitPrice!.Value,
            resolution.PricesIncludeVat!.Value, resolution.Currency,
            DateTimeOffset.UtcNow);
        return new(resolution, snapshot);
    }

    public async Task<PriceResolutionResult> ResolveAsync(
        Guid userId, Guid tenantId, Guid itemId, Guid? storeId,
        PriceListChannel channel, DateOnly businessDate,
        CancellationToken token = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty || itemId == Guid.Empty ||
            storeId == Guid.Empty || !Enum.IsDefined(channel) || businessDate == default)
        {
            return new(PriceResolutionStatus.Invalid);
        }

        TenantAccessResult permission = await authorization.CheckAsync(
            userId, tenantId, storeId, TenantPermission.Read, token);
        if (permission.Status != TenantAccessStatus.Granted)
        {
            return new(PriceResolutionStatus.Denied);
        }

        if (!await store.IsActiveCatalogItemAsync(tenantId, itemId, token))
        {
            return new(PriceResolutionStatus.ItemUnavailable);
        }

        IReadOnlyList<PriceResolutionCandidate> candidates =
            await store.FindCandidatesAsync(tenantId, itemId, storeId,
                channel, businessDate, token);

        PriceSelectionResult selected = PriceResolutionPolicy.Select(
            candidates, storeId, channel, businessDate);

        return selected.Status switch
        {
            PriceSelectionStatus.Unique => new(
                PriceResolutionStatus.Resolved,
                selected.Selected!.PriceListId,
                selected.Selected.PriceListCode,
                selected.Selected.UnitPrice,
                selected.Selected.PricesIncludeVat,
                selected.Selected.Currency,
                PriceListStoreId: selected.Selected.StoreId,
                PriceListChannel: selected.Selected.Channel),

            PriceSelectionStatus.Ambiguous => new(
                PriceResolutionStatus.Ambiguous,
                ConflictingPriceListIds: selected.ConflictingPriceListIds),

            _ => new(PriceResolutionStatus.NotPriced)
        };
    }
}
