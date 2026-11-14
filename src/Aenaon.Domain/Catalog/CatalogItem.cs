using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Είδος ή υπηρεσία με αμετάβλητη ιδιοκτησία επιχείρησης.</summary>
public sealed class CatalogItem
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 200;

    // Χρησιμοποιείται αποκλειστικά από μηχανισμό ανασύστασης αποθηκευμένων δεδομένων.
    private CatalogItem()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    internal CatalogItem(Guid tenantId, Guid categoryId, string? code, string? name,
        CatalogItemKind kind, bool tracksStock,
        CatalogUnitOfMeasure? unitOfMeasure = null,
        CatalogAccountingType? accountingType = null,
        string? barcode = null, string? longDescription = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.tenant.required", "Απαιτείται επιχείρηση για το είδος.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.category.not_found", "Η κατηγορία δεν βρέθηκε.");
        }

        string normalizedCode = CatalogText.RequiredCode(code, MaxCodeLength,
            "Κωδικός είδους", "catalog.item.code.invalid");
        string normalizedName = CatalogText.RequiredName(name, MaxNameLength,
            "Περιγραφή είδους", "catalog.item.name.invalid");

        if (!Enum.IsDefined(kind))
        {
            throw new DomainRuleException("catalog.item.kind.invalid", "Ο τύπος είδους δεν είναι έγκυρος.");
        }

        if (kind == CatalogItemKind.Service && tracksStock)
        {
            throw new DomainRuleException("catalog.item.service.stock_forbidden",
                "Οι υπηρεσίες δεν μπορούν να παρακολουθούν απόθεμα.");
        }

        CatalogUnitOfMeasure selectedUnit = unitOfMeasure ??
            (kind == CatalogItemKind.Service ? CatalogUnitOfMeasure.Service : CatalogUnitOfMeasure.Piece);
        CatalogAccountingType selectedAccounting = accountingType ??
            (kind == CatalogItemKind.Service ? CatalogAccountingType.Service : CatalogAccountingType.Merchandise);
        ValidateCommercial(kind, selectedUnit, selectedAccounting);
        string? normalizedBarcode = CatalogItemDetails.NormalizeBarcode(barcode);
        string? normalizedDescription = CatalogItemDetails.NormalizeLongDescription(longDescription);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CategoryId = categoryId;
        Code = normalizedCode;
        Name = normalizedName;
        Kind = kind;
        TracksStock = tracksStock;
        UnitOfMeasure = selectedUnit;
        AccountingType = selectedAccounting;
        Barcode = normalizedBarcode;
        LongDescription = normalizedDescription;
        IsActive = true;
    }

    /// <summary>Δημιουργεί προϊόν ή υπηρεσία με αμετάβλητο Tenant, κατηγορία και τύπο.</summary>
    public static CatalogItem Create(Guid tenantId, Guid categoryId,
        string? code, string? name, CatalogItemKind kind, bool tracksStock,
        CatalogUnitOfMeasure? unitOfMeasure = null,
        CatalogAccountingType? accountingType = null,
        string? barcode = null, string? longDescription = null) =>
        new(tenantId, categoryId, code, name, kind, tracksStock,
            unitOfMeasure, accountingType, barcode, longDescription);

    /// <summary>Ελέγχει όλα τα πεδία πριν τροποποιηθεί η καταχωρημένη οντότητα.</summary>
    public void Update(Guid categoryId, string? code, string? name, bool tracksStock,
        CatalogUnitOfMeasure? unitOfMeasure = null,
        CatalogAccountingType? accountingType = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.category.not_found",
                "Απαιτείται κατηγορία είδους.");
        }

        string normalizedCode = CatalogText.RequiredCode(code, MaxCodeLength,
            "Κωδικός είδους", "catalog.item.code.invalid");
        string normalizedName = CatalogText.RequiredName(name, MaxNameLength,
            "Περιγραφή είδους", "catalog.item.name.invalid");
        if (Kind == CatalogItemKind.Service && tracksStock)
        {
            throw new DomainRuleException("catalog.item.service.stock_forbidden",
                "Οι υπηρεσίες δεν μπορούν να παρακολουθούν απόθεμα.");
        }

        CatalogUnitOfMeasure selectedUnit = unitOfMeasure ?? UnitOfMeasure;
        CatalogAccountingType selectedAccounting = accountingType ?? AccountingType;
        ValidateCommercial(Kind, selectedUnit, selectedAccounting);

        CategoryId = categoryId;
        Code = normalizedCode;
        Name = normalizedName;
        TracksStock = tracksStock;
        UnitOfMeasure = selectedUnit;
        AccountingType = selectedAccounting;
    }

    /// <summary>Ενημερώνει ατομικά τα δύο προαιρετικά πεδία, χωρίς αλλαγή κωδικού ή τύπου.</summary>
    public void UpdateDetails(string? barcode, string? longDescription)
    {
        string? normalizedBarcode = CatalogItemDetails.NormalizeBarcode(barcode);
        string? normalizedDescription = CatalogItemDetails.NormalizeLongDescription(longDescription);
        Barcode = normalizedBarcode;
        LongDescription = normalizedDescription;
    }

    /// <summary>Αλλάζει μόνο την κατάσταση και ποτέ τον τύπο του είδους.</summary>
    public void ChangeActive(bool active) => SetActive(active);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public CatalogItemKind Kind { get; private set; }
    public bool TracksStock { get; private set; }
    public CatalogUnitOfMeasure UnitOfMeasure { get; private set; }
    public CatalogAccountingType AccountingType { get; private set; }
    public string? Barcode { get; private set; }
    public string? LongDescription { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Επικυρώνει εμπορικές επιλογές χωρίς αυτόματες φορολογικές αντιστοιχίσεις.</summary>
    private static void ValidateCommercial(CatalogItemKind kind,
        CatalogUnitOfMeasure unit, CatalogAccountingType accounting)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new DomainRuleException("catalog.item.unit.invalid",
                "Η μονάδα μέτρησης δεν είναι έγκυρη.");
        }

        if (!Enum.IsDefined(accounting))
        {
            throw new DomainRuleException("catalog.item.accounting.invalid",
                "Η λογιστική κατηγορία δεν είναι έγκυρη.");
        }

        if (kind == CatalogItemKind.Service &&
            (accounting != CatalogAccountingType.Service ||
             unit is not (CatalogUnitOfMeasure.Service or CatalogUnitOfMeasure.Hour)))
        {
            throw new DomainRuleException("catalog.item.service.profile.invalid",
                "Οι υπηρεσίες απαιτούν λογιστικό τύπο Υπηρεσία και μονάδα Υπηρεσία ή Ώρα.");
        }

        if (kind == CatalogItemKind.Product &&
            (accounting == CatalogAccountingType.Service || unit == CatalogUnitOfMeasure.Service))
        {
            throw new DomainRuleException("catalog.item.product.profile.invalid",
                "Τα είδη δεν μπορούν να έχουν λογιστικό τύπο ή μονάδα Υπηρεσία.");
        }
    }

    internal void ChangeCode(string? code)
    {
        Code = CatalogText.RequiredCode(code, MaxCodeLength,
            "Κωδικός είδους", "catalog.item.code.invalid");
    }

    internal void Rename(string? name)
    {
        Name = CatalogText.RequiredName(name, MaxNameLength,
            "Περιγραφή είδους", "catalog.item.name.invalid");
    }

    internal void MoveToCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.category.not_found", "Η κατηγορία δεν βρέθηκε.");
        }

        CategoryId = categoryId;
    }

    internal void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    internal void SetStockTracking(bool tracksStock)
    {
        if (Kind == CatalogItemKind.Service && tracksStock)
        {
            throw new DomainRuleException("catalog.item.service.stock_forbidden",
                "Οι υπηρεσίες δεν μπορούν να παρακολουθούν απόθεμα.");
        }

        TracksStock = tracksStock;
    }
}
