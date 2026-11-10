using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Προσωρινό, μη εγκεκριμένο προφίλ φορολογικής αντιστοίχισης ανά Tenant και είδος.</summary>
public sealed class FiscalTaxMappingRecord
{
    private FiscalTaxMappingRecord() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public FiscalCustomerChannel CustomerChannel { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveUntil { get; private set; }
    public Guid VatCategoryId { get; private set; }
    public Guid IncomeCategoryId { get; private set; }
    public Guid IncomeTypeId { get; private set; }
    public Guid? VatExemptionId { get; private set; }
    public Guid? MeasurementUnitId { get; private set; }
    public Guid? MeasurementUnitEnId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FiscalTaxMappingRecord Create(Guid tenantId, Guid catalogItemId,
        FiscalCustomerChannel channel, DateOnly from, DateOnly? until, Guid vatCategoryId,
        Guid incomeCategoryId, Guid incomeTypeId, Guid? vatExemptionId = null,
        Guid? measurementUnitId = null, Guid? measurementUnitEnId = null)
    {
        if (tenantId == Guid.Empty || catalogItemId == Guid.Empty ||
            !Enum.IsDefined(channel) || from == default || (until is DateOnly end && end < from) ||
            vatCategoryId == Guid.Empty || incomeCategoryId == Guid.Empty || incomeTypeId == Guid.Empty ||
            vatExemptionId == Guid.Empty || measurementUnitId == Guid.Empty || measurementUnitEnId == Guid.Empty)
        {
            throw new DomainRuleException("fiscal.mapping.invalid",
                "Λείπουν αναγνωριστικά, κανάλι ή έγκυρο χρονικό διάστημα.");
        }

        return new FiscalTaxMappingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CatalogItemId = catalogItemId,
            CustomerChannel = channel,
            EffectiveFrom = from,
            EffectiveUntil = until,
            VatCategoryId = vatCategoryId,
            IncomeCategoryId = incomeCategoryId,
            IncomeTypeId = incomeTypeId,
            VatExemptionId = vatExemptionId,
            MeasurementUnitId = measurementUnitId,
            MeasurementUnitEnId = measurementUnitEnId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
