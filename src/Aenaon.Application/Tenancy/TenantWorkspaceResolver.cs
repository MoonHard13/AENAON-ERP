namespace Aenaon.Application.Tenancy;

/// <summary>Αποτελέσματα επιλογής χωρίς σιωπηλή μεταφορά σε διαφορετική επιχείρηση ή κατάστημα.</summary>
public enum TenantWorkspaceStatus
{
    Ready,
    NoTenants,
    InvalidTenant,
    NoStores,
    InvalidStore
}

public sealed record TenantWorkspaceSnapshot(
    TenantWorkspaceStatus Status,
    IReadOnlyList<AccessibleTenant> Tenants,
    AccessibleTenant? Tenant,
    IReadOnlyList<TenantStoreSummary> Stores,
    TenantStoreSummary? Store);

/// <summary>Επιλύει το ενεργό πλαίσιο εργασίας αποκλειστικά για πιστοποιημένο χρήστη.</summary>
public sealed class TenantWorkspaceResolver(
    TenantAuthorizationService authorization,
    TenantWorkspaceService workspace)
{
    public async Task<TenantWorkspaceSnapshot> ResolveAsync(
        Guid authenticatedUserId,
        Guid? requestedTenantId,
        Guid? requestedStoreId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authenticatedUserId == Guid.Empty)
        {
            return new(TenantWorkspaceStatus.NoTenants, [], null, [], null);
        }

        IReadOnlyList<AccessibleTenant> tenants =
            await authorization.GetAccessibleTenantsAsync(authenticatedUserId, cancellationToken);

        if (tenants.Count == 0)
        {
            return new(TenantWorkspaceStatus.NoTenants, tenants, null, [], null);
        }

        // Δεν επιτρέπεται επιλογή καταστήματος χωρίς ρητό TenantId.
        if (requestedStoreId.HasValue && !requestedTenantId.HasValue)
        {
            return new(TenantWorkspaceStatus.InvalidStore, tenants, null, [], null);
        }

        AccessibleTenant? tenant = requestedTenantId.HasValue
            ? tenants.FirstOrDefault(item => item.TenantId == requestedTenantId.Value)
            : tenants[0];

        if (tenant is null)
        {
            // Δεν αποκαλύπτεται αν η ζητούμενη επιχείρηση υπάρχει στη βάση.
            return new(TenantWorkspaceStatus.InvalidTenant, tenants, null, [], null);
        }

        IReadOnlyList<TenantStoreSummary> stores = await workspace.ListAuthorizedStoresAsync(
            authenticatedUserId, tenant.TenantId, cancellationToken);

        if (stores.Count == 0)
        {
            return new(TenantWorkspaceStatus.NoStores, tenants, tenant, stores, null);
        }

        TenantStoreSummary? store = requestedStoreId.HasValue
            ? stores.FirstOrDefault(item => item.StoreId == requestedStoreId.Value)
            : stores[0];

        if (store is null)
        {
            return new(TenantWorkspaceStatus.InvalidStore, tenants, tenant, stores, null);
        }

        return new(TenantWorkspaceStatus.Ready, tenants, tenant, stores, store);
    }
}
