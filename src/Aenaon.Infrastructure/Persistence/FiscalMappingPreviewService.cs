using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Μόνο μη εγκεκριμένη προεπισκόπηση· δεν επιτρέπεται χρήση για έκδοση παραστατικού.</summary>
public sealed class FiscalMappingPreviewService(AenaonDbContext dbContext,
    ILogger<FiscalMappingPreviewService> logger)
{
    public async Task<FiscalMappingPreview> PreviewAsync(FiscalTransactionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.OperationKind != FiscalOperationKind.FiscalDocument)
        {
            throw new DomainRuleException("fiscal.profile.order.not_fiscal",
                "Οι παραγγελίες δεν είναι φορολογικά παραστατικά.");
        }

        // Το B2G απαιτεί επιπλέον στοιχεία λήπτη, σύμβασης και γραμμών.
        if (context.CustomerChannel == FiscalCustomerChannel.B2G)
        {
            throw new DomainRuleException("fiscal.mapping.b2g.pending",
                "Η φορολογική αξιολόγηση B2G δεν έχει ακόμη πιστοποιηθεί.");
        }

        var matched = await dbContext.FiscalTaxMappings.AsNoTracking()
            .Where(value => value.TenantId == context.TenantId &&
                value.CatalogItemId == context.CatalogItemId &&
                value.CustomerChannel == context.CustomerChannel &&
                value.EffectiveFrom <= context.TransactionDate &&
                (value.EffectiveUntil == null || value.EffectiveUntil >= context.TransactionDate))
            .Take(2).ToArrayAsync(cancellationToken);

        if (matched.Length == 0)
        {
            throw new DomainRuleException("fiscal.mapping.not_found", "Δεν βρέθηκε αντιστοίχιση για τη συναλλαγή.");
        }

        if (matched.Length != 1)
        {
            throw new DomainRuleException("fiscal.mapping.ambiguous",
                "Υπάρχουν πολλαπλές αντιστοιχίσεις για την ίδια περίοδο.");
        }

        FiscalTaxMappingRecord mapping = matched[0];
        Guid[] requiredIds = [mapping.VatCategoryId, mapping.IncomeCategoryId, mapping.IncomeTypeId];
        Guid[] allIds = requiredIds.Concat(
            new Guid?[] { mapping.VatExemptionId, mapping.MeasurementUnitId, mapping.MeasurementUnitEnId }
                .Where(value => value.HasValue).Select(value => value!.Value)).Distinct().ToArray();

        var references = await dbContext.FiscalReferenceCodes.AsNoTracking()
            .Where(value => allIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        FiscalReferenceCode Required(Guid id, FiscalCodeSystem expected)
        {
            if (!references.TryGetValue(id, out FiscalReferenceRecord? value) || value.CodeSystem != expected)
            {
                throw new DomainRuleException("fiscal.mapping.reference.invalid",
                    "Λείπει η σωστή έκδοση ή το σωστό namespace κωδικού.");
            }

            return value.ToReference();
        }

        FiscalReferenceCode? Optional(Guid? id, FiscalCodeSystem expected) =>
            id is Guid present ? Required(present, expected) : null;

        var candidate = FiscalProfileCandidate.Create(mapping.TenantId, mapping.CatalogItemId,
            mapping.EffectiveFrom, mapping.EffectiveUntil,
            Required(mapping.VatCategoryId, FiscalCodeSystem.MyDataVatCategory),
            Required(mapping.IncomeCategoryId, FiscalCodeSystem.MyDataIncomeCategory),
            Required(mapping.IncomeTypeId, FiscalCodeSystem.MyDataIncomeType),
            Optional(mapping.VatExemptionId, FiscalCodeSystem.MyDataVatExemption),
            Optional(mapping.MeasurementUnitId, FiscalCodeSystem.MyDataMeasurementUnit),
            Optional(mapping.MeasurementUnitEnId, FiscalCodeSystem.En16931MeasurementUnit));
        candidate.ValidateScope(context);

        logger.LogInformation("Fiscal mapping preview evaluated; mapping {MappingId} requires fiscal review", mapping.Id);
        return new FiscalMappingPreview(mapping.Id, candidate);
    }
}

/// <summary>Αποτελεί μόνο εσωτερική πρόταση και ποτέ εντολή τιμολόγησης.</summary>
public sealed record FiscalMappingPreview(Guid MappingId, FiscalProfileCandidate Candidate)
{
    public bool CanIssueFiscalDocument => false;
}
