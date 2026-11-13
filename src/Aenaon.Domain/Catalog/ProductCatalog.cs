using System.Collections.ObjectModel;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Διαχειρίζεται κατηγορίες και είδη αποκλειστικά μίας επιχείρησης.</summary>
public sealed class ProductCatalog
{
    private readonly List<CatalogCategory> _categories = [];
    private readonly List<CatalogItem> _items = [];
    private readonly ReadOnlyCollection<CatalogCategory> _categoryView;
    private readonly ReadOnlyCollection<CatalogItem> _itemView;

    private ProductCatalog(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.tenant.required",
                "Απαιτείται έγκυρο αναγνωριστικό επιχείρησης.");
        }

        TenantId = tenantId;
        _categoryView = _categories.AsReadOnly();
        _itemView = _items.AsReadOnly();
    }

    public Guid TenantId { get; }
    public IReadOnlyList<CatalogCategory> Categories => _categoryView;
    public IReadOnlyList<CatalogItem> Items => _itemView;

    public static ProductCatalog Create(Guid tenantId) => new(tenantId);

    /// <summary>Προσθέτει κατηγορία με μοναδικό κωδικό στο συγκεκριμένο catalog.</summary>
    public CatalogCategory AddCategory(string? code, string? name)
    {
        var category = new CatalogCategory(TenantId, code, name);

        if (_categories.Any(existing =>
            StringComparer.OrdinalIgnoreCase.Equals(existing.Code, category.Code)))
        {
            throw new DomainRuleException("catalog.category.code.duplicate",
                "Υπάρχει ήδη κατηγορία με αυτόν τον κωδικό στην επιχείρηση.");
        }

        _categories.Add(category);
        return category;
    }

    /// <summary>Προσθέτει είδος ή υπηρεσία μόνο σε ενεργή κατηγορία της ίδιας επιχείρησης.</summary>
    public CatalogItem AddItem(Guid categoryId, string? code, string? name,
        CatalogItemKind kind, bool tracksStock = false,
        CatalogUnitOfMeasure? unitOfMeasure = null,
        CatalogAccountingType? accountingType = null)
    {
        CatalogCategory category = RequireCategory(categoryId);
        EnsureActive(category);

        var item = new CatalogItem(TenantId, category.Id, code, name, kind,
            tracksStock, unitOfMeasure, accountingType);

        if (_items.Any(existing =>
            StringComparer.OrdinalIgnoreCase.Equals(existing.Code, item.Code)))
        {
            throw new DomainRuleException("catalog.item.code.duplicate",
                "Υπάρχει ήδη είδος ή υπηρεσία με αυτόν τον κωδικό στην επιχείρηση.");
        }

        _items.Add(item);
        return item;
    }

    public void ChangeCategoryCode(Guid categoryId, string? code)
    {
        CatalogCategory category = RequireCategory(categoryId);
        string normalized = CatalogText.RequiredCode(code, CatalogCategory.MaxCodeLength,
            "Κωδικός κατηγορίας", "catalog.category.code.invalid");

        if (_categories.Any(existing => existing.Id != categoryId &&
            StringComparer.OrdinalIgnoreCase.Equals(existing.Code, normalized)))
        {
            throw new DomainRuleException("catalog.category.code.duplicate",
                "Υπάρχει ήδη κατηγορία με αυτόν τον κωδικό στην επιχείρηση.");
        }

        category.ChangeCode(normalized);
    }

    public void RenameCategory(Guid categoryId, string? name)
    {
        RequireCategory(categoryId).Rename(name);
    }

    /// <summary>Η απενεργοποίηση κατηγορίας δεν αλλάζει σιωπηρά τα υπάρχοντα είδη.</summary>
    public void SetCategoryActive(Guid categoryId, bool isActive)
    {
        RequireCategory(categoryId).SetActive(isActive);
    }

    public void ChangeItemCode(Guid itemId, string? code)
    {
        CatalogItem item = RequireItem(itemId);
        string normalized = CatalogText.RequiredCode(code, CatalogItem.MaxCodeLength,
            "Κωδικός είδους", "catalog.item.code.invalid");

        if (_items.Any(existing => existing.Id != itemId &&
            StringComparer.OrdinalIgnoreCase.Equals(existing.Code, normalized)))
        {
            throw new DomainRuleException("catalog.item.code.duplicate",
                "Υπάρχει ήδη είδος ή υπηρεσία με αυτόν τον κωδικό στην επιχείρηση.");
        }

        item.ChangeCode(normalized);
    }

    public void RenameItem(Guid itemId, string? name)
    {
        RequireItem(itemId).Rename(name);
    }

    public void ChangeItemCategory(Guid itemId, Guid categoryId)
    {
        CatalogItem item = RequireItem(itemId);
        CatalogCategory category = RequireCategory(categoryId);
        EnsureActive(category);
        item.MoveToCategory(category.Id);
    }

    public void SetItemActive(Guid itemId, bool isActive)
    {
        RequireItem(itemId).SetActive(isActive);
    }

    public void SetItemStockTracking(Guid itemId, bool tracksStock)
    {
        RequireItem(itemId).SetStockTracking(tracksStock);
    }

    private CatalogCategory RequireCategory(Guid categoryId)
    {
        return _categories.FirstOrDefault(category => category.Id == categoryId)
            ?? throw new DomainRuleException("catalog.category.not_found",
                "Η κατηγορία δεν βρέθηκε στην επιλεγμένη επιχείρηση.");
    }

    private CatalogItem RequireItem(Guid itemId)
    {
        return _items.FirstOrDefault(item => item.Id == itemId)
            ?? throw new DomainRuleException("catalog.item.not_found",
                "Το είδος δεν βρέθηκε στην επιλεγμένη επιχείρηση.");
    }

    private static void EnsureActive(CatalogCategory category)
    {
        if (!category.IsActive)
        {
            throw new DomainRuleException("catalog.category.inactive",
                "Δεν επιτρέπεται η καταχώρηση ή μετακίνηση είδους σε ανενεργή κατηγορία.");
        }
    }
}
