using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace Aenaon.Application.Catalog;

/// <summary>Ενέργειες συντήρησης τιμοκαταλόγων χωρίς φορολογική επεξεργασία.</summary>
public enum PriceListAction { Create, Update, Activate, Deactivate }

public enum PriceListStatus { Saved, Denied, Missing, Invalid, Duplicate }

public sealed record PriceListChange(PriceListAction Action, Guid? Id = null,
    string? Code = null, string? Name = null, Guid? StoreId = null,
    PriceListChannel Channel = PriceListChannel.All, bool PricesIncludeVat = true,
    DateOnly ValidFrom = default, DateOnly? ValidUntil = null);

public sealed record PriceListPriceChange(Guid CatalogItemId, decimal UnitPrice);

public sealed record PriceListResult(PriceListStatus Status, Guid? Id = null, string? Error = null);

public sealed record PriceListSummary(Guid Id, string Code, string Name, Guid? StoreId,
    PriceListChannel Channel, bool PricesIncludeVat, DateOnly ValidFrom,
    DateOnly? ValidUntil, bool IsActive, int ItemCount);

public sealed record PriceListPage(IReadOnlyList<PriceListSummary> Rows,
    int Page, bool HasMore);

public sealed record PriceListItemRow(Guid Id, string Code, string Name,
    CatalogItemKind Kind, decimal? UnitPrice);

public sealed record PriceListItemsPage(IReadOnlyList<PriceListItemRow> Rows,
    int Page, bool HasMore);

/// <summary>Η υποδομή επιβάλλει TenantId και επαληθεύει αναφορές στη βάση.</summary>
public interface IPriceListManagementStore
{
    Task<PriceListPage> ListAsync(Guid tenantId, string? search,
        bool includeInactive, int page, int pageSize, CancellationToken token);
    Task<PriceListSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken token);
    Task<PriceListItemsPage?> ReadItemsAsync(Guid tenantId, Guid listId,
        string? search, int page, int pageSize, CancellationToken token);
    Task<PriceListResult> ApplyAsync(Guid tenantId, PriceListChange change, CancellationToken token);
    Task<PriceListResult> SetPricesAsync(Guid tenantId, Guid priceListId,
        IReadOnlyList<PriceListPriceChange> changes, CancellationToken token);
}

/// <summary>Ενιαίο application service για Blazor και CLI, με fail-closed δικαιώματα.</summary>
public sealed class PriceListManagementService(
    TenantAuthorizationService authorization, IPriceListManagementStore store,
    ILogger<PriceListManagementService> logger)
{
    public async Task<PriceListPage?> ListAsync(Guid userId, Guid tenantId,
        string? search = null, bool includeInactive = false, int page = 1, int pageSize = 25,
        CancellationToken token = default)
    {
        if (!await AllowedAsync(userId, tenantId, TenantPermission.Read, token))
        {
            return null;
        }

        if (!ValidQuery(search, page, pageSize))
        {
            throw new DomainRuleException("pricelist.query.invalid", "Μη έγκυρη αναζήτηση ή σελίδα.");
        }

        return await store.ListAsync(tenantId, search?.Trim(), includeInactive, page, pageSize, token);
    }

    public async Task<PriceListSummary?> GetAsync(Guid userId, Guid tenantId,
        Guid listId, CancellationToken token = default)
    {
        if (listId == Guid.Empty ||
            !await AllowedAsync(userId, tenantId, TenantPermission.Read, token))
        {
            return null;
        }

        return await store.GetAsync(tenantId, listId, token);
    }

    public async Task<PriceListItemsPage?> ReadItemsAsync(Guid userId, Guid tenantId,
        Guid listId, string? search = null, int page = 1, int pageSize = 25,
        CancellationToken token = default)
    {
        if (listId == Guid.Empty ||
            !await AllowedAsync(userId, tenantId, TenantPermission.Read, token))
        {
            return null;
        }

        if (!ValidQuery(search, page, pageSize))
        {
            throw new DomainRuleException("pricelist.query.invalid", "Μη έγκυρη αναζήτηση ή σελίδα.");
        }

        return await store.ReadItemsAsync(tenantId, listId, search?.Trim(), page, pageSize, token);
    }

    public async Task<PriceListResult> ApplyAsync(Guid userId, Guid tenantId,
        PriceListChange change, CancellationToken token = default)
    {
        if (!await AllowedAsync(userId, tenantId, TenantPermission.Manage, token))
        {
            return new(PriceListStatus.Denied);
        }

        if (!Enum.IsDefined(change.Action) ||
            (change.Action == PriceListAction.Create && change.Id.HasValue) ||
            (change.Action != PriceListAction.Create &&
             (!change.Id.HasValue || change.Id == Guid.Empty)) ||
            change.StoreId == Guid.Empty ||
            (change.Action is PriceListAction.Create or PriceListAction.Update &&
             string.IsNullOrWhiteSpace(change.Name)))
        {
            return new(PriceListStatus.Invalid);
        }

        try
        {
            if (change.Action == PriceListAction.Create)
            {
                _ = PriceList.Create(tenantId, change.StoreId, change.Code, change.Name,
                    change.Channel, change.PricesIncludeVat, change.ValidFrom, change.ValidUntil);
            }
            else if (change.Action == PriceListAction.Update)
            {
                if (change.ValidFrom == default ||
                    (change.ValidUntil is { } end && end < change.ValidFrom))
                {
                    return new(PriceListStatus.Invalid, Error: "pricelist.period.invalid");
                }
            }

            PriceListResult result = await store.ApplyAsync(tenantId, change, token);
            logger.LogInformation("Price list action {Action} status {Status} by {UserId} in {TenantId}, list {Id}",
                change.Action, result.Status, userId, tenantId, result.Id ?? change.Id);
            return result;
        }
        catch (DomainRuleException ex)
        {
            return new(PriceListStatus.Invalid, Error: ex.Code);
        }
    }

    public async Task<PriceListResult> SetPricesAsync(Guid userId, Guid tenantId,
        Guid listId, IReadOnlyList<PriceListPriceChange> changes,
        CancellationToken token = default)
    {
        if (!await AllowedAsync(userId, tenantId, TenantPermission.Manage, token))
        {
            return new(PriceListStatus.Denied);
        }

        if (listId == Guid.Empty || changes.Count is < 1 or > 50 ||
            changes.Any(x => x.CatalogItemId == Guid.Empty ||
                x.UnitPrice < 0 || x.UnitPrice > PriceListItem.MaxUnitPrice ||
                decimal.Round(x.UnitPrice, 4) != x.UnitPrice) ||
            changes.Select(x => x.CatalogItemId).Distinct().Count() != changes.Count)
        {
            return new(PriceListStatus.Invalid);
        }

        PriceListResult result = await store.SetPricesAsync(tenantId, listId, changes, token);
        logger.LogInformation("Price list prices update {Status} for {ListId} in {TenantId} by {UserId}, rows {Count}",
            result.Status, listId, tenantId, userId, changes.Count);
        return result;
    }

    public async Task<bool> CanManageAsync(Guid userId, Guid tenantId,
        CancellationToken token = default) =>
        await AllowedAsync(userId, tenantId, TenantPermission.Manage, token);

    private async Task<bool> AllowedAsync(Guid userId, Guid tenantId,
        TenantPermission permission, CancellationToken token)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
        {
            return false;
        }

        TenantAccessResult result = await authorization.CheckAsync(
            userId, tenantId, null, permission, token);
        return result.Status == TenantAccessStatus.Granted;
    }

    private static bool ValidQuery(string? search, int page, int pageSize) =>
        page is >= 1 and <= 100_000 &&
        pageSize is 25 or 50 &&
        search is null or { Length: <= 80 } &&
        search?.Any(char.IsControl) != true;
}
