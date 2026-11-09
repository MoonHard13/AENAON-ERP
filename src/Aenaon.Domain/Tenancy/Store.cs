using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Tenancy;

/// <summary>Κατάστημα με σταθερή ιδιοκτησία σε συγκεκριμένη επιχείρηση.</summary>
public sealed class Store
{
    public const int MaxCodeLength = 30;
    public const int MaxNameLength = 160;

    // Χρησιμοποιείται μόνο για ανασύσταση αποθηκευμένων δεδομένων από το EF Core.
    private Store()
    {
        Code = "";
        Name = "";
    }

    internal Store(Guid tenantId, string? code, string? name)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("store.tenant.required", "Το κατάστημα πρέπει να ανήκει σε επιχείρηση.");
        }

        Code = OrganizationText.StoreCode(code);
        Name = OrganizationText.RequiredName(name, MaxNameLength, "Όνομα καταστήματος", "store.name.invalid");
        Id = Guid.NewGuid();
        TenantId = tenantId;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
}
