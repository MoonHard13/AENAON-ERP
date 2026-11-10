using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Fiscal;

/// <summary>
/// Υποψήφια, μη εγκεκριμένη φορολογική αντιστοίχιση είδους.
/// Η επικύρωση δομής και χρονικής ισχύος δεν ισοδυναμεί με φορολογική συμμόρφωση.
/// </summary>
public sealed class FiscalProfileCandidate
{
    private FiscalProfileCandidate(Guid tenantId, Guid catalogItemId, DateOnly effectiveFrom,
        DateOnly? effectiveUntil, FiscalReferenceCode vatCategory,
        FiscalReferenceCode incomeCategory, FiscalReferenceCode incomeType,
        FiscalReferenceCode? vatExemption, FiscalReferenceCode? measurementUnit,
        FiscalReferenceCode? measurementUnitEn)
    {
        TenantId = tenantId;
        CatalogItemId = catalogItemId;
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
        VatCategory = vatCategory;
        IncomeCategory = incomeCategory;
        IncomeType = incomeType;
        VatExemption = vatExemption;
        MeasurementUnit = measurementUnit;
        MeasurementUnitEn = measurementUnitEn;
    }

    public Guid TenantId { get; }
    public Guid CatalogItemId { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveUntil { get; }
    public FiscalReferenceCode VatCategory { get; }
    public FiscalReferenceCode IncomeCategory { get; }
    public FiscalReferenceCode IncomeType { get; }
    public FiscalReferenceCode? VatExemption { get; }
    public FiscalReferenceCode? MeasurementUnit { get; }
    public FiscalReferenceCode? MeasurementUnitEn { get; }

    public static FiscalProfileCandidate Create(Guid tenantId, Guid catalogItemId,
        DateOnly effectiveFrom, DateOnly? effectiveUntil, FiscalReferenceCode? vatCategory,
        FiscalReferenceCode? incomeCategory, FiscalReferenceCode? incomeType,
        FiscalReferenceCode? vatExemption = null, FiscalReferenceCode? measurementUnit = null,
        FiscalReferenceCode? measurementUnitEn = null)
    {
        if (tenantId == Guid.Empty || catalogItemId == Guid.Empty)
        {
            throw new DomainRuleException("fiscal.profile.identity.required",
                "Απαιτούνται επιχείρηση και είδος για τη φορολογική πρόταση.");
        }

        if (effectiveFrom == default || (effectiveUntil is DateOnly until && until < effectiveFrom))
        {
            throw new DomainRuleException("fiscal.profile.period.invalid",
                "Το διάστημα ισχύος της φορολογικής πρότασης δεν είναι έγκυρο.");
        }

        RequireSystem(vatCategory, FiscalCodeSystem.MyDataVatCategory);
        RequireSystem(incomeCategory, FiscalCodeSystem.MyDataIncomeCategory);
        RequireSystem(incomeType, FiscalCodeSystem.MyDataIncomeType);
        ValidateOptionalSystem(vatExemption, FiscalCodeSystem.MyDataVatExemption);
        ValidateOptionalSystem(measurementUnit, FiscalCodeSystem.MyDataMeasurementUnit);
        ValidateOptionalSystem(measurementUnitEn, FiscalCodeSystem.En16931MeasurementUnit);

        return new FiscalProfileCandidate(tenantId, catalogItemId, effectiveFrom, effectiveUntil,
            vatCategory!, incomeCategory!, incomeType!, vatExemption, measurementUnit, measurementUnitEn);
    }

    /// <summary>
    /// Ελέγχει μόνο ταυτοποίηση, χρονική ισχύ και διαχωρισμό παραγγελίας/παραστατικού.
    /// Δεν αξιολογεί την εγκυρότητα φορολογικών συνδυασμών και δεν εγκρίνει έκδοση.
    /// </summary>
    public void ValidateScope(FiscalTransactionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TenantId != TenantId || context.CatalogItemId != CatalogItemId)
        {
            throw new DomainRuleException("fiscal.profile.scope.mismatch",
                "Η φορολογική πρόταση δεν αντιστοιχεί στην επιχείρηση ή στο είδος.");
        }

        if (context.OperationKind != FiscalOperationKind.FiscalDocument)
        {
            throw new DomainRuleException("fiscal.profile.order.not_fiscal",
                "Η παραγγελία δεν αντιμετωπίζεται ως φορολογικό παραστατικό.");
        }

        DateOnly date = context.TransactionDate;
        if (date < EffectiveFrom || (EffectiveUntil is DateOnly until && date > until) ||
            !VatCategory.IsEffectiveOn(date) || !IncomeCategory.IsEffectiveOn(date) ||
            !IncomeType.IsEffectiveOn(date) ||
            (VatExemption is not null && !VatExemption.IsEffectiveOn(date)) ||
            (MeasurementUnit is not null && !MeasurementUnit.IsEffectiveOn(date)) ||
            (MeasurementUnitEn is not null && !MeasurementUnitEn.IsEffectiveOn(date)))
        {
            throw new DomainRuleException("fiscal.profile.period.inactive",
                "Η φορολογική πρόταση ή κωδικός αναφοράς δεν ισχύει για την ημερομηνία συναλλαγής.");
        }
    }

    private static void RequireSystem(FiscalReferenceCode? reference, FiscalCodeSystem expected)
    {
        if (reference is null || reference.System != expected)
        {
            throw new DomainRuleException("fiscal.profile.reference.system.invalid",
                "Απαιτείται ο κατάλληλος κωδικός από το σωστό φορολογικό σύστημα αναφοράς.");
        }
    }

    private static void ValidateOptionalSystem(FiscalReferenceCode? reference, FiscalCodeSystem expected)
    {
        if (reference is not null)
        {
            RequireSystem(reference, expected);
        }
    }
}
