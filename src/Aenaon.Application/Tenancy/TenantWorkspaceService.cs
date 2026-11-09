namespace Aenaon.Application.Tenancy;

/// <summary>Ελέγχει δικαίωμα πριν από την ανάκτηση οποιουδήποτε καταστήματος της επιχείρησης.</summary>
public sealed class TenantWorkspaceService(TenantAuthorizationService authorization, ITenantStoreCatalog stores)
{
    public async Task<IReadOnlyList<TenantStoreSummary>> ListAuthorizedStoresAsync(
        Guid authenticatedUserId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var access = await authorization.CheckAsync(authenticatedUserId, tenantId, null,
            TenantPermission.Read, cancellationToken);
        if (access.Status != TenantAccessStatus.Granted)
        {
            return [];
        }

        return await stores.ListStoresAsync(tenantId, cancellationToken);
    }
}
