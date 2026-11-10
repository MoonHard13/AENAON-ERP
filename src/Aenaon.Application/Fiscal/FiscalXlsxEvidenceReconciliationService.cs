using System.Text.Json;
using System.Text.Json.Serialization;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Fiscal;

/// <summary>
/// Τεχνική συμφωνία πλήρους JSON με το ακριβές πρωτότυπο XLSX.
/// Δεν επαληθεύει την αυθεντικότητα της προέλευσης ή τη φορολογική ορθότητα.
/// </summary>
public sealed record FiscalXlsxEvidenceReconciliationResult(
    string ArtifactSha256,
    int SheetCount,
    int CellCount,
    int CandidatePairCount,
    int ManualReviewCount)
{
    public bool IsOriginalFileContentMatched => true;
    public bool IsClassificationMatrixVerified => false;
    public bool IsImpactContractValidated => false;
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>
/// Αναπαράγει ανεξάρτητα την επιθεώρηση XLSX και συγκρίνει ακριβώς κάθε JSON κελί,
/// συγχώνευση, υποψήφιο ζεύγος και παρατήρηση χωρίς SQL ή φορολογική ενεργοποίηση.
/// </summary>
public sealed class FiscalXlsxEvidenceReconciliationService(
    FiscalXlsxEvidenceInspector inspector,
    FiscalXlsxEvidenceExportService exporter)
{
    private const long MaximumJsonBytes = 32L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public async Task<FiscalXlsxEvidenceReconciliationResult> VerifyAsync(
        string originalXlsxPath,
        string fullJsonPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(originalXlsxPath) ||
            string.IsNullOrWhiteSpace(fullJsonPath) ||
            !string.Equals(Path.GetExtension(fullJsonPath), ".json",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(originalXlsxPath), Path.GetFullPath(fullJsonPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Απαιτείται διαφορετικό, υπάρχον αρχείο JSON με κατάληξη .json.");
        }

        var jsonInfo = new FileInfo(Path.GetFullPath(fullJsonPath));
        if (!jsonInfo.Exists || jsonInfo.Length is <= 0 or > MaximumJsonBytes)
        {
            throw Invalid("Το JSON δεν υπάρχει ή υπερβαίνει το επιτρεπόμενο μέγεθος.");
        }

        FiscalXlsxEvidenceExport supplied;
        try
        {
            await using var stream = new FileStream(jsonInfo.FullName, FileMode.Open,
                FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            using JsonDocument document = await JsonDocument.ParseAsync(stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32
                }, cancellationToken);

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !FlagIsFalse(root, "IsClassificationMatrixVerified") ||
                !FlagIsFalse(root, "IsFiscalRuleApproved") ||
                !FlagIsFalse(root, "CanIssueFiscalDocument") ||
                !root.TryGetProperty("ClassificationEvidence", out JsonElement classification) ||
                classification.ValueKind != JsonValueKind.Object ||
                !FlagIsFalse(classification, "IsFiscalRuleApproved") ||
                !FlagIsFalse(classification, "CanIssueFiscalDocument"))
            {
                throw Invalid("Η εξαγωγή απαιτεί ρητά μη εγκεκριμένες φορολογικές καταστάσεις.");
            }

            supplied = root.Deserialize<FiscalXlsxEvidenceExport>(JsonOptions) ??
                throw Invalid("Αδύνατη η ανάγνωση πλήρους JSON τεκμηρίου.");
        }
        catch (JsonException)
        {
            throw Invalid("Το JSON περιέχει άγνωστα, ελλιπή ή μη έγκυρα δεδομένα.");
        }

        // Ξαναδιαβάζουμε το πραγματικό αρχείο, δεν θεωρούμε έμπιστους τους μετρητές JSON.
        FiscalXlsxEvidenceInspection original = await inspector.InspectAsync(
            originalXlsxPath, cancellationToken);
        FiscalXlsxEvidenceExport expected = exporter.Build(original);

        if (!string.Equals(expected.ArtifactSha256, supplied.ArtifactSha256,
                StringComparison.Ordinal) ||
            expected.FileSizeBytes != supplied.FileSizeBytes ||
            expected.SheetCount != supplied.SheetCount ||
            expected.NonEmptyCellCount != supplied.NonEmptyCellCount ||
            expected.ExportedCellCount != supplied.ExportedCellCount ||
            !supplied.IsComplete || supplied.Sheets is null ||
            supplied.Sheets.Count != expected.Sheets.Count ||
            supplied.ClassificationEvidence is null ||
            supplied.ClassificationEvidence.CandidatePairs is null ||
            supplied.ClassificationEvidence.RequiresReview is null)
        {
            throw Mismatch();
        }

        for (int index = 0; index < expected.Sheets.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FiscalXlsxSheetExport source = expected.Sheets[index];
            FiscalXlsxSheetExport candidate = supplied.Sheets[index];
            if (candidate is null ||
                !string.Equals(source.Name, candidate.Name, StringComparison.Ordinal) ||
                source.NonEmptyCellCount != candidate.NonEmptyCellCount ||
                source.ExportedCellCount != candidate.ExportedCellCount ||
                !candidate.IsComplete ||
                source.HasStyledCells != candidate.HasStyledCells ||
                candidate.MergedRanges is null ||
                !source.MergedRanges.SequenceEqual(candidate.MergedRanges,
                    StringComparer.Ordinal) ||
                candidate.Cells is null ||
                !source.Cells.SequenceEqual(candidate.Cells))
            {
                throw Mismatch();
            }
        }

        if (!expected.ClassificationEvidence.CandidatePairs.SequenceEqual(
                supplied.ClassificationEvidence.CandidatePairs) ||
            !expected.ClassificationEvidence.RequiresReview.SequenceEqual(
                supplied.ClassificationEvidence.RequiresReview))
        {
            throw Mismatch();
        }

        return new FiscalXlsxEvidenceReconciliationResult(expected.ArtifactSha256,
            expected.SheetCount, expected.ExportedCellCount,
            expected.ClassificationEvidence.CandidatePairs.Count,
            expected.ClassificationEvidence.RequiresReview.Count);
    }

    private static bool FlagIsFalse(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.False;

    private static DomainRuleException Invalid(string message) =>
        new("fiscal.xlsx.reconciliation.invalid", message);

    private static DomainRuleException Mismatch() =>
        new("fiscal.xlsx.reconciliation.mismatch",
            "Το JSON διαφέρει από την πραγματική δομή/τιμές του XLSX ή τις παραγόμενες παρατηρήσεις.");
}
