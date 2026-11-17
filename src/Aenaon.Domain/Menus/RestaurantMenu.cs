using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Menus;

/// <summary>Η ταυτότητα μενού ανήκει σε έναν μόνο Tenant και δεν διατηρεί τιμές πώλησης.</summary>
public sealed class RestaurantMenu
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 160;

    private RestaurantMenu()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private RestaurantMenu(Guid tenantId, string? code, string? name)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("menu.tenant.required", "Απαιτείται επιχείρηση.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        Code = MenuText.Required(code, MaxCodeLength, "menu.code.invalid", "Κωδικός", true);
        Name = MenuText.Required(name, MaxNameLength, "menu.name.invalid", "Όνομα");
    }

    public static RestaurantMenu Create(Guid tenantId, string? code, string? name) =>
        new(tenantId, code, name);

    /// <summary>Ο κωδικός και ο Tenant δεν μεταβάλλονται έπειτα από δημιουργία.</summary>
    public void Rename(string? name) =>
        Name = MenuText.Required(name, MaxNameLength, "menu.name.invalid", "Όνομα");

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsArchived { get; private set; }
}
