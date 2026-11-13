using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Μία ακριβής εμπορική τιμή ανά είδος και τιμοκατάλογο.</summary>
public sealed class PriceListItem
{
    public const decimal MaxUnitPrice = 999_999_999_999.9999m;

    // Χρησιμοποιείται μόνο για ανάγνωση από EF Core.
    private PriceListItem() { }

    private PriceListItem(Guid tenantId, Guid priceListId, Guid catalogItemId, decimal unitPrice)
    {
        if (tenantId == Guid.Empty || priceListId == Guid.Empty || catalogItemId == Guid.Empty)
        {
            throw new DomainRuleException("pricelist.item.reference.invalid",
                "Απαιτούνται έγκυρα αναγνωριστικά επιχείρησης, τιμοκαταλόγου και είδους.");
        }

        ValidatePrice(unitPrice);
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PriceListId = priceListId;
        CatalogItemId = catalogItemId;
        UnitPrice = unitPrice;
    }

    public static PriceListItem Create(Guid tenantId, Guid priceListId,
        Guid catalogItemId, decimal unitPrice) =>
        new(tenantId, priceListId, catalogItemId, unitPrice);

    public void ChangePrice(decimal unitPrice)
    {
        ValidatePrice(unitPrice);
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public decimal UnitPrice { get; private set; }

    private static void ValidatePrice(decimal value)
    {
        if (value < 0 || value > MaxUnitPrice ||
            decimal.Round(value, 4, MidpointRounding.ToEven) != value)
        {
            throw new DomainRuleException("pricelist.price.invalid",
                "Η τιμή πρέπει να είναι μη αρνητική και να έχει έως τέσσερα δεκαδικά ψηφία.");
        }
    }
}
