namespace Aenaon.Domain.Fiscal;

/// <summary>Ανεξάρτητα συστήματα αναφοράς φορολογικών κωδικών.</summary>
public enum FiscalCodeSystem
{
    MyDataVatCategory = 1,
    MyDataVatExemption = 2,
    MyDataIncomeCategory = 3,
    MyDataIncomeType = 4,
    MyDataMeasurementUnit = 5,
    En16931MeasurementUnit = 6,
    ImpactDocumentType = 7,
    MyDataInvoiceType = 8
}

/// <summary>Επίσημη πηγή από την οποία τεκμηριώθηκε ο κωδικός.</summary>
public enum FiscalReferenceSource
{
    Aade = 1,
    Impact = 2,
    En16931 = 3
}
