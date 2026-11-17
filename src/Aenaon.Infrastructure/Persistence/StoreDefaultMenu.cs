namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Δείχνει το ένα προεπιλεγμένο μενού ενός συγκεκριμένου καταστήματος.
/// Το ίδιο Menu παραμένει Tenant-scoped και μπορεί μελλοντικά να στοχεύει πολλά stores.
/// </summary>
public sealed class StoreDefaultMenu
{
    private StoreDefaultMenu()
    {
    }

    public StoreDefaultMenu(Guid tenantId, Guid storeId, Guid menuId)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty || menuId == Guid.Empty)
        {
            throw new ArgumentException("Απαιτείται έγκυρος Tenant, Store και Menu.");
        }

        TenantId = tenantId;
        StoreId = storeId;
        MenuId = menuId;
    }

    public Guid TenantId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid MenuId { get; private set; }
}
