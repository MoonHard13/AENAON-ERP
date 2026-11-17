using Aenaon.Domain.Menus;
using Aenaon.Domain.Tenancy;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Κοινός δημιουργός default Draft ανά Store. Η κλήση γίνεται πάντοτε μέσα
/// στην ίδια συναλλαγή με τη δημιουργία καταστήματος ή ελεγχόμενη αποκατάσταση.
/// </summary>
internal static class StoreDefaultMenuFactory
{
    public static RestaurantMenu Add(AenaonDbContext context, Store store, DateTime utcNow)
    {
        RestaurantMenu menu = RestaurantMenu.Create(store.TenantId,
            "DEFAULT-" + store.Code, "Προεπιλεγμένο Μενού");
        context.RestaurantMenus.Add(menu);
        context.MenuDrafts.Add(MenuDraft.Create(store.TenantId, menu.Id, utcNow));
        context.StoreDefaultMenus.Add(new StoreDefaultMenu(store.TenantId, store.Id, menu.Id));
        return menu;
    }
}
