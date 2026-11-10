using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Fiscal;

/// <summary>Κανάλι πελάτη για μελλοντική αξιολόγηση φορολογικών κανόνων.</summary>
public enum FiscalCustomerChannel
{
    B2C = 1,
    B2B = 2,
    B2G = 3
}

/// <summary>Διαχωρίζει την απλή παραγγελία από την πρόθεση έκδοσης φορολογικού παραστατικού.</summary>
public enum FiscalOperationKind
{
    Order = 1,
    FiscalDocument = 2
}

/// <summary>Αμετάβλητο πλαίσιο συναλλαγής χωρίς υπολογισμό ή επιλογή φορολογικής μεταχείρισης.</summary>
public sealed record FiscalTransactionContext
{
    private FiscalTransactionContext(Guid tenantId, Guid storeId, Guid catalogItemId,
        CatalogItemKind itemKind, DateOnly transactionDate, FiscalCustomerChannel customerChannel,
        FiscalOperationKind operationKind)
    {
        TenantId = tenantId;
        StoreId = storeId;
        CatalogItemId = catalogItemId;
        ItemKind = itemKind;
        TransactionDate = transactionDate;
        CustomerChannel = customerChannel;
        OperationKind = operationKind;
    }

    public Guid TenantId { get; }
    public Guid StoreId { get; }
    public Guid CatalogItemId { get; }
    public CatalogItemKind ItemKind { get; }
    public DateOnly TransactionDate { get; }
    public FiscalCustomerChannel CustomerChannel { get; }
    public FiscalOperationKind OperationKind { get; }

    public static FiscalTransactionContext Create(Guid tenantId, Guid storeId, Guid catalogItemId,
        CatalogItemKind itemKind, DateOnly transactionDate, FiscalCustomerChannel customerChannel,
        FiscalOperationKind operationKind)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty || catalogItemId == Guid.Empty)
        {
            throw new DomainRuleException("fiscal.context.identity.required",
                "Απαιτούνται αναγνωριστικά επιχείρησης, καταστήματος και είδους.");
        }

        if (transactionDate == default || !Enum.IsDefined(itemKind) ||
            !Enum.IsDefined(customerChannel) || !Enum.IsDefined(operationKind))
        {
            throw new DomainRuleException("fiscal.context.invalid",
                "Ο τύπος συναλλαγής, το κανάλι, το είδος ή η ημερομηνία δεν είναι έγκυρα.");
        }

        return new FiscalTransactionContext(tenantId, storeId, catalogItemId, itemKind,
            transactionDate, customerChannel, operationKind);
    }
}
