using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Tenancy;

namespace Aenaon.Application.Tenancy;

/// <summary>Επικυρώνει την πρώτη επιχείρηση και αναθέτει την ατομική αποθήκευσή της στην υποδομή.</summary>
public sealed class InitialBusinessService(IInitialBusinessStore store)
{
    public async Task<InitialBusinessResult> ExecuteAsync(string? tenantName, string? storeCode, string? storeName,
        bool apply, string? confirmedDatabase, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Tenant tenant;
        Store initialStore;
        try
        {
            tenant = Tenant.Create(tenantName);
            initialStore = tenant.AddStore(storeCode, storeName);
        }
        catch (DomainRuleException error)
        {
            return new(InitialBusinessStatus.ValidationFailed, ValidationMessage: error.Message);
        }

        var result = await store.ExecuteAsync(tenant, apply, confirmedDatabase, cancellationToken);
        return result.Status is InitialBusinessStatus.Ready or InitialBusinessStatus.Created
            ? result with { Summary = new(tenant.Name, initialStore.Code, initialStore.Name) }
            : result;
    }
}
