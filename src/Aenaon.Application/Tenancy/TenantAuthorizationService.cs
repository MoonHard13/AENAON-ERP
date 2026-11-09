using Aenaon.Domain.Tenancy;

namespace Aenaon.Application.Tenancy;

/// <summary>Εφαρμόζει τον κανόνα άρνησης πρόσβασης εξ ορισμού για αναγνωρισμένο χρήστη και επιχείρηση.</summary>
public sealed class TenantAuthorizationService(ITenantAccessStore store)
{
    /// <summary>Το userId προέρχεται αποκλειστικά από επιβεβαιωμένη ταυτότητα, ποτέ από εισαγόμενο πεδίο πελάτη.</summary>
    public async Task<TenantAccessResult> CheckAsync(Guid authenticatedUserId, Guid tenantId,
        Guid? storeId, TenantPermission permission, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authenticatedUserId == Guid.Empty || tenantId == Guid.Empty ||
            storeId == Guid.Empty || !Enum.IsDefined(permission))
        {
            return new(TenantAccessStatus.Denied);
        }

        TenantMembershipRole? role =
            await store.GetMembershipRoleAsync(authenticatedUserId, tenantId, cancellationToken);
        if (role is not (TenantMembershipRole.Owner or TenantMembershipRole.Member) ||
            (permission == TenantPermission.Manage && role != TenantMembershipRole.Owner))
        {
            return new(TenantAccessStatus.Denied);
        }

        if (storeId.HasValue &&
            !await store.StoreBelongsToTenantAsync(tenantId, storeId.Value, cancellationToken))
        {
            return new(TenantAccessStatus.Denied);
        }

        return new(TenantAccessStatus.Granted, role);
    }

    /// <summary>Επιστρέφει μόνο τις επιχειρήσεις στις οποίες υπάρχει ενεργή συμμετοχή του χρήστη.</summary>
    public async Task<IReadOnlyList<AccessibleTenant>> GetAccessibleTenantsAsync(
        Guid authenticatedUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authenticatedUserId == Guid.Empty)
        {
            return [];
        }

        return await store.ListUserTenantsAsync(authenticatedUserId, cancellationToken);
    }
}
