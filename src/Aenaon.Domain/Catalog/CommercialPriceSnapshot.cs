using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>
/// Αμετάβλητη εμπορική εικόνα επιλεγμένης τιμής για μελλοντική γραμμή παραγγελίας.
/// Δεν αποτελεί εγγραφή SQL, φορολογικό παραστατικό ή εγκεκριμένο υπολογισμό ΦΠΑ.
/// </summary>
public sealed class CommercialPriceSnapshot
{
    public const int CurrentContractVersion = 1;

    private CommercialPriceSnapshot(Guid tenantId, Guid? storeId, Guid catalogItemId,
        Guid priceListId, string priceListCode, Guid? priceListStoreId,
        PriceListChannel priceListChannel, PriceListChannel requestedChannel,
        DateOnly businessDate, decimal unitPrice, bool pricesIncludeVat,
        string currency, DateTimeOffset capturedUtc)
    {
        ContractVersion = CurrentContractVersion;
        SelectionPolicyVersion = PriceResolutionPolicy.PolicyVersion;
        TenantId = tenantId;
        StoreId = storeId;
        CatalogItemId = catalogItemId;
        PriceListId = priceListId;
        PriceListCode = priceListCode;
        PriceListStoreId = priceListStoreId;
        PriceListChannel = priceListChannel;
        RequestedChannel = requestedChannel;
        BusinessDate = businessDate;
        UnitPrice = unitPrice;
        PricesIncludeVat = pricesIncludeVat;
        Currency = currency;
        CapturedUtc = capturedUtc;
    }

    /// <summary>Αποτυπώνει τιμή που έχει ήδη επιλυθεί από εξουσιοδοτημένη υπηρεσία.</summary>
    public static CommercialPriceSnapshot Capture(Guid tenantId, Guid? storeId,
        Guid catalogItemId, Guid priceListId, string? priceListCode,
        Guid? priceListStoreId, PriceListChannel priceListChannel,
        PriceListChannel requestedChannel, DateOnly businessDate,
        decimal unitPrice, bool pricesIncludeVat, string? currency,
        DateTimeOffset capturedUtc)
    {
        if (tenantId == Guid.Empty || catalogItemId == Guid.Empty ||
            priceListId == Guid.Empty || storeId == Guid.Empty ||
            priceListStoreId == Guid.Empty || businessDate == default ||
            string.IsNullOrWhiteSpace(priceListCode) ||
            priceListCode.Length > PriceList.MaxCodeLength ||
            !string.Equals(currency, PriceList.SupportedCurrency, StringComparison.Ordinal) ||
            !Enum.IsDefined(priceListChannel) || !Enum.IsDefined(requestedChannel) ||
            (priceListStoreId.HasValue && priceListStoreId != storeId) ||
            (priceListChannel != PriceListChannel.All && priceListChannel != requestedChannel) ||
            unitPrice < 0 || unitPrice > PriceListItem.MaxUnitPrice ||
            decimal.Round(unitPrice, 4) != unitPrice ||
            capturedUtc == default || capturedUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("pricing.snapshot.invalid",
                "Μη έγκυρα στοιχεία αμετάβλητης εμπορικής τιμής.");
        }

        return new CommercialPriceSnapshot(tenantId, storeId, catalogItemId,
            priceListId, priceListCode, priceListStoreId, priceListChannel,
            requestedChannel, businessDate, unitPrice, pricesIncludeVat,
            currency, capturedUtc);
    }

    public int ContractVersion { get; }
    public string SelectionPolicyVersion { get; }
    public Guid TenantId { get; }
    public Guid? StoreId { get; }
    public Guid CatalogItemId { get; }
    public Guid PriceListId { get; }
    public string PriceListCode { get; }
    public Guid? PriceListStoreId { get; }
    public PriceListChannel PriceListChannel { get; }
    public PriceListChannel RequestedChannel { get; }
    public DateOnly BusinessDate { get; }
    public decimal UnitPrice { get; }
    public bool PricesIncludeVat { get; }
    public string Currency { get; }
    public DateTimeOffset CapturedUtc { get; }
}
