namespace Aenaon.Domain.Menus;

/// <summary>Κατηγορία παρουσίασης ενός συγκεκριμένου MenuDraft, ανεξάρτητη από CatalogCategory.</summary>
public sealed class MenuDraftCategory
{
    public const int MaxNameLength = 160;

    private MenuDraftCategory()
    {
        Name = string.Empty;
    }

    private MenuDraftCategory(Guid tenantId, Guid menuId, string? name, int position)
    {
        MenuText.Identity(tenantId, menuId, "μενού");
        MenuText.Position(position);
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MenuId = menuId;
        Name = MenuText.Required(name, MaxNameLength, "menu.category.name.invalid", "Κατηγορία");
        Position = position;
        IsVisible = true;
    }

    public static MenuDraftCategory Create(Guid tenantId, Guid menuId, string? name, int position) =>
        new(tenantId, menuId, name, position);

    public void Update(string? name, bool isVisible)
    {
        Name = MenuText.Required(name, MaxNameLength, "menu.category.name.invalid", "Κατηγορία");
        IsVisible = isVisible;
    }

    public void SetPosition(int position)
    {
        MenuText.Position(position);
        Position = position;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid MenuId { get; private set; }
    public string Name { get; private set; }
    public int Position { get; private set; }
    public bool IsVisible { get; private set; }
}
