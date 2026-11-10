namespace Aenaon.Infrastructure.Fiscal;

/// <summary>Διατηρεί τη μία τελική απόφαση και το αποτύπωμα του περιεχομένου που ελέγχθηκε.</summary>
public sealed class FiscalImportReviewRecord
{
    private FiscalImportReviewRecord() { }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public DateTimeOffset ReviewedAtUtc { get; private set; }
    public FiscalImportReviewStatus Decision { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;

    public static FiscalImportReviewRecord Create(Guid batchId, Guid submitterId, Guid reviewerId,
        DateTimeOffset reviewedAtUtc, FiscalImportReviewStatus decision, string reason, string contentSha256) =>
        new()
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            SubmittedByUserId = submitterId,
            ReviewerUserId = reviewerId,
            ReviewedAtUtc = reviewedAtUtc,
            Decision = decision,
            Reason = reason,
            ContentSha256 = contentSha256
        };
}
