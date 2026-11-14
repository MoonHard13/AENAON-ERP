using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Τα κανάλια εμπορικής πώλησης δεν συνεπάγονται φορολογικό χαρακτηρισμό.</summary>
public enum PriceListChannel
{
    All = 0,
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3,
    Retail = 4,
    Online = 5
}

/// <summary>Ανεξάρτητος τιμοκατάλογος επιχείρησης με ρητή ημερομηνία ισχύος.</summary>
public sealed class PriceList
{
    public const int MaxCodeLength = 30;
    public const int MaxNameLength = 160;
    public const string SupportedCurrency = "EUR";

    // Απαιτείται αποκλειστικά για την ανασύσταση από EF Core.
    private PriceList()
    {
        Code = string.Empty;
        Name = string.Empty;
        Currency = SupportedCurrency;
    }

    private PriceList(Guid tenantId, Guid? storeId, string? code, string? name,
        PriceListChannel channel, bool pricesIncludeVat, DateOnly validFrom, DateOnly? validUntil)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
        {
            throw new DomainRuleException("pricelist.scope.invalid",
                "Απαιτείται έγκυρη επιχείρηση και, προαιρετικά, έγκυρο κατάστημα.");
        }

        ValidatePeriod(validFrom, validUntil);
        if (!Enum.IsDefined(channel))
        {
            throw new DomainRuleException("pricelist.channel.invalid", "Μη έγκυρο κανάλι πώλησης.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        StoreId = storeId;
        Code = CatalogText.RequiredCode(code, MaxCodeLength,
            "Κωδικός τιμοκαταλόγου", "pricelist.code.invalid");
        Name = CatalogText.RequiredName(name, MaxNameLength,
            "Τίτλος τιμοκαταλόγου", "pricelist.name.invalid");
        Channel = channel;
        PricesIncludeVat = pricesIncludeVat;
        Currency = SupportedCurrency;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        IsActive = true;
    }

    public static PriceList Create(Guid tenantId, Guid? storeId, string? code,
        string? name, PriceListChannel channel, bool pricesIncludeVat,
        DateOnly validFrom, DateOnly? validUntil = null) =>
        new(tenantId, storeId, code, name, channel, pricesIncludeVat, validFrom, validUntil);

    /// <summary>Το κατάστημα, το κανάλι και η ερμηνεία τιμής παραμένουν αμετάβλητα.</summary>
    public void Update(string? name, DateOnly validFrom, DateOnly? validUntil)
    {
        ValidatePeriod(validFrom, validUntil);
        Name = CatalogText.RequiredName(name, MaxNameLength,
            "Τίτλος τιμοκαταλόγου", "pricelist.name.invalid");
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    /// <summary>Αλλάζει το εμπορικό πλαίσιο χωρίς αλλαγή της ταυτότητας ή των τιμών.</summary>
    public void UpdateCommercialProfile(string? name, Guid? storeId,
        PriceListChannel channel, bool pricesIncludeVat,
        DateOnly validFrom, DateOnly? validUntil)
    {
        if (storeId == Guid.Empty)
        {
            throw new DomainRuleException("pricelist.scope.invalid",
                "Το κατάστημα πρέπει να ανήκει στην επιχείρηση.");
        }

        if (!Enum.IsDefined(channel))
        {
            throw new DomainRuleException("pricelist.channel.invalid",
                "Μη έγκυρο κανάλι πώλησης.");
        }

        Update(name, validFrom, validUntil);
        StoreId = storeId;
        Channel = channel;
        PricesIncludeVat = pricesIncludeVat;
    }

    public void ChangeActive(bool active) => IsActive = active;

    public bool IsValidOn(DateOnly day) =>
        IsActive && day >= ValidFrom && (!ValidUntil.HasValue || day <= ValidUntil.Value);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? StoreId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public PriceListChannel Channel { get; private set; }
    public string Currency { get; private set; }
    public bool PricesIncludeVat { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public bool IsActive { get; private set; }

    private static void ValidatePeriod(DateOnly validFrom, DateOnly? validUntil)
    {
        if (validFrom == default || validUntil is { } end && end < validFrom)
        {
            throw new DomainRuleException("pricelist.period.invalid",
                "Η ημερομηνία λήξης δεν μπορεί να προηγείται της έναρξης.");
        }
    }
}
