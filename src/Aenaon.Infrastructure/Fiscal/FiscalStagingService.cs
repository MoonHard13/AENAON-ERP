using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Fiscal;

/// <summary>Αποθηκεύει απομονωμένες υποβολές και μία τελική ανεξάρτητη κρίση πηγής.</summary>
public sealed class FiscalStagingService(
    AenaonDbContext dbContext,
    FiscalImportInspectionService inspectionService,
    IFiscalStagingAuthorizationPolicy accessPolicy,
    TimeProvider timeProvider,
    ILogger<FiscalStagingService> logger)
{
    public async Task<Guid> StageAsync(string manifestPath, string evidencePath,
        Guid actorId, CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || !await accessPolicy.CanSubmitAsync(actorId, cancellationToken))
        {
            throw new DomainRuleException("fiscal.staging.access.denied",
                "Δεν επιτρέπεται υποβολή φορολογικού τεκμηρίου.");
        }

        FiscalImportInspection inspection = await inspectionService.InspectAsync(
            manifestPath, evidencePath, cancellationToken);
        FiscalImportBatch batch = FiscalImportBatch.Create(inspection, actorId,
            timeProvider.GetUtcNow());
        dbContext.FiscalImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Fiscal evidence staged: {BatchId}, {CodeSystem}, {Rows}",
            batch.Id, batch.CodeSystem, batch.Rows.Count);
        return batch.Id;
    }

    public async Task<FiscalImportReviewStatus> ReviewAsync(Guid batchId, Guid actorId,
        bool acceptSourceEvidence, string? reason, CancellationToken cancellationToken = default)
    {
        if (batchId == Guid.Empty || actorId == Guid.Empty ||
            !await accessPolicy.CanReviewAsync(actorId, cancellationToken))
        {
            throw new DomainRuleException("fiscal.staging.access.denied",
                "Δεν επιτρέπεται έλεγχος φορολογικού τεκμηρίου.");
        }

        FiscalImportBatch? batch = await dbContext.FiscalImportBatches
            .Include(value => value.Rows)
            .SingleOrDefaultAsync(value => value.Id == batchId, cancellationToken);
        if (batch is null)
        {
            throw new DomainRuleException("fiscal.staging.not_found",
                "Η υποβολή δεν βρέθηκε.");
        }

        FiscalImportReviewRecord review = batch.RecordDecision(actorId,
            timeProvider.GetUtcNow(), acceptSourceEvidence, reason);
        dbContext.FiscalImportReviews.Add(review);

        // Ένα SaveChanges επιτρέπει ενιαία συναλλαγή για απόφαση και audit row.
        // Το rowversion της υποβολής αποτρέπει ανταγωνιστικές εγκρίσεις.
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Fiscal source review recorded: {BatchId}, {Decision}",
            batchId, batch.Status);
        return batch.Status;
    }
}
