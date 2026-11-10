namespace Aenaon.Application.Tenancy;

/// <summary>Αναγνωστικό συμβόλαιο καταστημάτων με πάντοτε υποχρεωτικό tenant φίλτρο.</summary>
public interface ITenantStoreCatalog
{
    Task<IReadOnlyList<TenantStoreSummary>> ListStoresAsync(Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record TenantStoreSummary(Guid StoreId, Guid TenantId, string Code, string Name);
