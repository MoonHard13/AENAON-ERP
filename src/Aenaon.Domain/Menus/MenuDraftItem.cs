namespace Aenaon.Domain.Menus;

/// <summary>Σύνδεση παρουσίασης με υπάρχον CatalogItem του ίδιου Tenant, χωρίς δική της τιμή.</summary>
public sealed class MenuDraftItem
{
    public const int MaxNameOverrideLength = 200;

    private MenuDraftItem()
    {
    }

    private MenuDraftItem(Guid tenantId, Guid menuId, Guid categoryId,
        Guid catalogItemId, int position, string? nameOverride)
    {
        MenuText.Identity(tenantId, menuId, "μενού");
        MenuText.Identity(tenantId, categoryId, "κατηγορία");
        MenuText.Identity(tenantId, catalogItemId, "είδος καταλόγου");
        MenuText.Position(position);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        MenuId = menuId;
        MenuCategoryId = categoryId;
        CatalogItemId = catalogItemId;
        Position = position;
        NameOverride = MenuText.Optional(nameOverride, MaxNameOverrideLength,
            "menu.item.name.invalid", "Εμφανιζόμενο όνομα");
    }

    public static MenuDraftItem Create(Guid tenantId, Guid menuId, Guid categoryId,
        Guid catalogItemId, int position, string? nameOverride = null) =>
        new(tenantId, menuId, categoryId, catalogItemId, position, nameOverride);

    public void Rename(string? nameOverride) =>
        NameOverride = MenuText.Optional(nameOverride, MaxNameOverrideLength,
            "menu.item.name.invalid", "Εμφανιζόμενο όνομα");

    public void SetPosition(int position)
    {
        MenuText.Position(position);
        Position = position;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid MenuId { get; private set; }
    public Guid MenuCategoryId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public int Position { get; private set; }
    public string? NameOverride { get; private set; }
}
