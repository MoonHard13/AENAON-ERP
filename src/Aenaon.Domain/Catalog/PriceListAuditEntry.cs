using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Τύπος μεταβολής εμπορικής πολιτικής ή τιμής, ανεξάρτητος από φορολογική έκδοση.</summary>
public enum PriceListAuditAction
{
    CreateList = 1,
    UpdateList = 2,
    ActivateList = 3,
    DeactivateList = 4,
    AddPrice = 5,
    UpdatePrice = 6,
    RemovePrice = 7
}

/// <summary>Μη μεταβαλλόμενο συμβάν ελέγχου: κάθε επιτυχής μεταβολή γράφεται στην ίδια συναλλαγή.</summary>
public sealed class PriceListAuditEntry
{
    private PriceListAuditEntry() { Source = string.Empty; }

    private PriceListAuditEntry(Guid tenantId, Guid priceListId, Guid? catalogItemId,
        Guid? actorUserId, string source, PriceListAuditAction action,
        decimal? beforePrice, decimal? afterPrice, string? beforePolicy, string? afterPolicy)
    {
        if (tenantId == Guid.Empty || priceListId == Guid.Empty ||
            catalogItemId == Guid.Empty || actorUserId == Guid.Empty ||
            !Enum.IsDefined(action) ||
            source is not ("Web" or "CLI" or "System") ||
            beforePolicy?.Length > 1024 || afterPolicy?.Length > 1024 ||
            (catalogItemId.HasValue != (action is PriceListAuditAction.AddPrice or
                PriceListAuditAction.UpdatePrice or PriceListAuditAction.RemovePrice)))
        {
            throw new DomainRuleException("pricelist.audit.invalid",
                "Μη έγκυρο συμβάν ιστορικού τιμοκαταλόγου.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        PriceListId = priceListId;
        CatalogItemId = catalogItemId;
        ActorUserId = actorUserId;
        Source = source;
        Action = action;
        OccurredUtc = DateTime.UtcNow;
        BeforePrice = beforePrice;
        AfterPrice = afterPrice;
        BeforePolicy = beforePolicy;
        AfterPolicy = afterPolicy;
    }

    public static PriceListAuditEntry Create(Guid tenantId, Guid priceListId,
        Guid? catalogItemId, Guid? actorUserId, string source, PriceListAuditAction action,
        decimal? beforePrice = null, decimal? afterPrice = null,
        string? beforePolicy = null, string? afterPolicy = null) =>
        new(tenantId, priceListId, catalogItemId, actorUserId,
            source, action, beforePrice, afterPrice, beforePolicy, afterPolicy);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid? CatalogItemId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Source { get; private set; }
    public PriceListAuditAction Action { get; private set; }
    public DateTime OccurredUtc { get; private set; }
    public decimal? BeforePrice { get; private set; }
    public decimal? AfterPrice { get; private set; }
    public string? BeforePolicy { get; private set; }
    public string? AfterPolicy { get; private set; }
}
