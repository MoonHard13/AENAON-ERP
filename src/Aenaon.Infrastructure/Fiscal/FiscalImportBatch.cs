using System.Security.Cryptography;
using System.Text;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;

namespace Aenaon.Infrastructure.Fiscal;

/// <summary>Μη ενεργό staging στιγμιότυπο αναφοράς, ανεξάρτητο από το παραγωγικό fiscal registry.</summary>
public sealed class FiscalImportBatch
{
    private readonly List<FiscalImportBatchRow> _rows = [];

    private FiscalImportBatch() { }

    public Guid Id { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public FiscalReferenceSource Source { get; private set; }
    public FiscalCodeSystem CodeSystem { get; private set; }
    public string SourceVersion { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string ArtifactSha256 { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;
    public DateTimeOffset VerifiedAtUtc { get; private set; }
    public FiscalImportReviewStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? ReviewReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<FiscalImportBatchRow> Rows => _rows.AsReadOnly();

    public static FiscalImportBatch Create(FiscalImportInspection inspection, Guid submitterId,
        DateTimeOffset submittedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (submitterId == Guid.Empty || submittedAtUtc.Offset != TimeSpan.Zero ||
            submittedAtUtc == default || inspection.VerifiedAtUtc > submittedAtUtc ||
            inspection.InspectedRows.Count == 0 || inspection.InspectedRows.Count != inspection.CandidateCount)
        {
            throw new DomainRuleException("fiscal.staging.submission.invalid",
                "Η υποβολή απαιτεί ταυτοποιημένο χρήστη, έγκυρες UTC ημερομηνίες και επιθεωρημένες γραμμές.");
        }

        var batch = new FiscalImportBatch
        {
            Id = Guid.NewGuid(),
            SubmittedByUserId = submitterId,
            SubmittedAtUtc = submittedAtUtc,
            Source = inspection.Source,
            CodeSystem = inspection.CodeSystem,
            SourceVersion = inspection.SourceVersion,
            SourceUrl = inspection.SourceUrl,
            ArtifactSha256 = inspection.ArtifactSha256,
            VerifiedAtUtc = inspection.VerifiedAtUtc,
            Status = FiscalImportReviewStatus.Staged
        };

        foreach (FiscalInspectedRow row in inspection.InspectedRows)
        {
            batch._rows.Add(FiscalImportBatchRow.Create(batch.Id, row));
        }

        batch.ContentSha256 = CalculateContentSha256(batch.Source, batch.CodeSystem,
            batch.SourceVersion, batch.SourceUrl, batch.ArtifactSha256, batch.VerifiedAtUtc, batch._rows);
        return batch;
    }

    /// <summary>Η επανεξέταση γίνεται πάνω στο ακριβές αποθηκευμένο περιεχόμενο.</summary>
    public void VerifyContentFingerprint()
    {
        string recalculated = CalculateContentSha256(Source, CodeSystem, SourceVersion, SourceUrl,
            ArtifactSha256, VerifiedAtUtc, _rows);
        if (!string.Equals(ContentSha256, recalculated, StringComparison.Ordinal))
        {
            throw new DomainRuleException("fiscal.staging.integrity.invalid",
                "Το περιεχόμενο της προσωρινής υποβολής έχει αλλοιωθεί.");
        }
    }

    public FiscalImportReviewRecord RecordDecision(Guid reviewerId, DateTimeOffset reviewedAtUtc,
        bool accepted, string? reason)
    {
        VerifyContentFingerprint();
        string explanation = (reason ?? string.Empty).Trim();
        if (Status != FiscalImportReviewStatus.Staged)
        {
            throw new DomainRuleException("fiscal.staging.review.completed",
                "Η υποβολή έχει ήδη τελική απόφαση.");
        }

        if (reviewerId == Guid.Empty || reviewerId == SubmittedByUserId ||
            reviewedAtUtc.Offset != TimeSpan.Zero || reviewedAtUtc < SubmittedAtUtc ||
            explanation.Length is 0 or > 500 || explanation.Any(char.IsControl))
        {
            throw new DomainRuleException("fiscal.staging.review.invalid",
                "Απαιτείται διαφορετικός ελεγκτής, UTC χρόνος και τεκμηριωμένη απόφαση.");
        }

        Status = accepted ? FiscalImportReviewStatus.SourceReviewed : FiscalImportReviewStatus.Rejected;
        ReviewedByUserId = reviewerId;
        ReviewedAtUtc = reviewedAtUtc;
        ReviewReason = explanation;
        return FiscalImportReviewRecord.Create(Id, SubmittedByUserId, reviewerId, reviewedAtUtc,
            Status, explanation, ContentSha256);
    }

    private static string CalculateContentSha256(FiscalReferenceSource source, FiscalCodeSystem system,
        string version, string url, string artifactHash, DateTimeOffset verifiedAtUtc,
        IEnumerable<FiscalImportBatchRow> rows)
    {
        // Το BinaryWriter γράφει μήκη και σταθερά πεδία ώστε να αποφεύγονται αμφίσημα delimiters.
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((int)source);
            writer.Write((int)system);
            writer.Write(version);
            writer.Write(url);
            writer.Write(artifactHash);
            writer.Write(verifiedAtUtc.UtcTicks);
            foreach (FiscalImportBatchRow row in rows.OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.EffectiveFrom))
            {
                writer.Write(row.Code);
                writer.Write(row.Description);
                writer.Write(row.EffectiveFrom.DayNumber);
                writer.Write(row.EffectiveUntil?.DayNumber ?? int.MinValue);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

/// <summary>SourceReviewed σημαίνει έλεγχο τεκμηρίου, όχι νόμιμη φορολογική έγκριση.</summary>
public enum FiscalImportReviewStatus
{
    Staged = 1,
    SourceReviewed = 2,
    Rejected = 3
}
