using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Κατηγορία ειδών που ανήκει σε μία συγκεκριμένη επιχείρηση.</summary>
public sealed class CatalogCategory
{
    public const int MaxCodeLength = 30;
    public const int MaxNameLength = 160;

    // Χρησιμοποιείται αποκλειστικά από μηχανισμό ανασύστασης αποθηκευμένων δεδομένων.
    private CatalogCategory()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    internal CatalogCategory(Guid tenantId, string? code, string? name)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.tenant.required", "Απαιτείται επιχείρηση για την κατηγορία.");
        }

        string normalizedCode = CatalogText.RequiredCode(code, MaxCodeLength,
            "Κωδικός κατηγορίας", "catalog.category.code.invalid");
        string normalizedName = CatalogText.RequiredName(name, MaxNameLength,
            "Όνομα κατηγορίας", "catalog.category.name.invalid");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        Code = normalizedCode;
        Name = normalizedName;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    internal void ChangeCode(string? code)
    {
        Code = CatalogText.RequiredCode(code, MaxCodeLength,
            "Κωδικός κατηγορίας", "catalog.category.code.invalid");
    }

    internal void Rename(string? name)
    {
        Name = CatalogText.RequiredName(name, MaxNameLength,
            "Όνομα κατηγορίας", "catalog.category.name.invalid");
    }

    internal void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
