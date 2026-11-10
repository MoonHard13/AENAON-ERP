using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;

namespace Aenaon.Application.Fiscal;

/// <summary>
/// Υποψήφια χειροκίνητη αποδελτίωση του επίσημου XLS συνδυασμών της ΑΑΔΕ.
/// Δεν συνιστά επιβεβαίωση ότι η αποδελτίωση αντιστοιχεί πράγματι στα κελιά του XLS.
/// </summary>
public sealed class FiscalClassificationMatrixManifest
{
    public string SourceVersion { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string ArtifactSha256 { get; init; } = string.Empty;
    public DateTimeOffset VerifiedAtUtc { get; init; }
    public List<FiscalClassificationMatrixRow>? Rows { get; init; }
}

/// <summary>Μια υποψήφια τριάδα τύπου παραστατικού, κατηγορίας και τύπου εσόδου.</summary>
public sealed class FiscalClassificationMatrixRow
{
    public string InvoiceType { get; init; } = string.Empty;
    public string IncomeCategory { get; init; } = string.Empty;
    public string IncomeType { get; init; } = string.Empty;
    public string SourceSheet { get; init; } = string.Empty;
    public string SourceCell { get; init; } = string.Empty;
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveUntil { get; init; }
}

/// <summary>Μη εγκεκριμένη τριάδα με διακριτή προέλευση ανά κωδικό.</summary>
public sealed record FiscalClassificationCandidateRule(
    FiscalReferenceCode InvoiceType,
    FiscalReferenceCode IncomeCategory,
    FiscalReferenceCode IncomeType,
    string SourceSheet,
    string SourceCell);

/// <summary>Αποτέλεσμα ακριβούς αναζήτησης στο υποψήφιο, όχι ακόμη ελεγμένο, manifest.</summary>
public sealed record FiscalClassificationCandidateMatch(bool PresentInCandidateManifest,
    string? SourceSheet, string? SourceCell)
{
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>Στιγμιότυπο επιθεώρησης με αποκλειστικά μη παραγωγικές δυνατότητες.</summary>
public sealed class FiscalClassificationMatrixInspection
{
    public required string SourceVersion { get; init; }
    public required string SourceUrl { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required DateTimeOffset VerifiedAtUtc { get; init; }
    public required IReadOnlyList<FiscalClassificationCandidateRule> CandidateRules { get; init; }

    public int CandidateCount => CandidateRules.Count;
    public bool IsSourceTranscriptionVerified => false;
    public bool IsImpactContractValidated => false;
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;

    /// <summary>
    /// Εντοπίζει μόνο παρουσία τριάδας στο χειροκίνητο manifest.
    /// Το αποτέλεσμα δεν επιτρέπει χαρακτηρισμό παραστατικού.
    /// </summary>
    public FiscalClassificationCandidateMatch FindCandidate(string invoiceType, string incomeCategory,
        string incomeType, DateOnly date)
    {
        if (date == default)
        {
            throw new DomainRuleException("fiscal.matrix.query.invalid", "Απαιτείται έγκυρη ημερομηνία.");
        }

        string invoice = NormalizeSearch(invoiceType);
        string category = NormalizeSearch(incomeCategory);
        string type = NormalizeSearch(incomeType);
        FiscalClassificationCandidateRule[] matches = CandidateRules.Where(row =>
            row.InvoiceType.Code == invoice && row.IncomeCategory.Code == category &&
            row.IncomeType.Code == type && row.InvoiceType.IsEffectiveOn(date)).Take(2).ToArray();

        if (matches.Length > 1)
        {
            throw new DomainRuleException("fiscal.matrix.ambiguous",
                "Πολλαπλές υποψήφιες εγγραφές καλύπτουν την ίδια ημερομηνία.");
        }

        return matches.Length == 1
            ? new FiscalClassificationCandidateMatch(true, matches[0].SourceSheet, matches[0].SourceCell)
            : new FiscalClassificationCandidateMatch(false, null, null);
    }

    private static string NormalizeSearch(string code)
    {
        string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length is 0 or > FiscalReferenceCode.MaxCodeLength ||
            normalized.Any(char.IsWhiteSpace) || normalized.Any(char.IsControl))
        {
            throw new DomainRuleException("fiscal.matrix.query.invalid",
                "Οι κωδικοί αναζήτησης πρέπει να είναι σύντομοι και χωρίς κενά.");
        }

        return normalized;
    }
}

/// <summary>Offline επιθεώρηση τεκμηρίου XLS και αυστηρά ορισμένων υποψήφιων συνδυασμών.</summary>
public sealed class FiscalClassificationMatrixInspectionService(TimeProvider timeProvider)
{
    private const long MaximumManifestBytes = 4 * 1024 * 1024;
    private const long MaximumEvidenceBytes = 30 * 1024 * 1024;
    private const int MaximumRows = 10000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    public async Task<FiscalClassificationMatrixInspection> InspectAsync(string manifestPath,
        string originalSpreadsheetPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(manifestPath) || string.IsNullOrWhiteSpace(originalSpreadsheetPath))
        {
            throw Invalid("Απαιτούνται δύο διαφορετικά αρχεία: manifest και επίσημο πρωτότυπο XLS/XLSX.");
        }

        string manifestFullPath = Path.GetFullPath(manifestPath);
        string evidenceFullPath = Path.GetFullPath(originalSpreadsheetPath);
        if (string.Equals(manifestFullPath, evidenceFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Το manifest και το πρωτότυπο XLS δεν μπορούν να είναι το ίδιο αρχείο.");
        }

        var manifestInfo = new FileInfo(manifestFullPath);
        var evidenceInfo = new FileInfo(evidenceFullPath);
        if (!manifestInfo.Exists || manifestInfo.Length is <= 0 or > MaximumManifestBytes ||
            !evidenceInfo.Exists || evidenceInfo.Length is <= 0 or > MaximumEvidenceBytes ||
            Path.GetExtension(evidenceFullPath) is not (".xlsx" or ".xls"))
        {
            throw Invalid("Λείπει αρχείο XLS/XLSX ή υπερβαίνει το επιτρεπόμενο μέγεθος.");
        }

        FiscalClassificationMatrixManifest manifest;
        await using (var stream = new FileStream(manifestFullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            try
            {
                manifest = await JsonSerializer.DeserializeAsync<FiscalClassificationMatrixManifest>(
                    stream, JsonOptions, cancellationToken)
                    ?? throw Invalid("Το manifest είναι κενό.");
            }
            catch (JsonException)
            {
                throw Invalid("Το JSON περιέχει άγνωστα ή μη έγκυρα πεδία.");
            }
        }

        if (!Uri.TryCreate(manifest.SourceUrl, UriKind.Absolute, out Uri? sourceUri) ||
            sourceUri.Scheme != Uri.UriSchemeHttps || sourceUri.UserInfo.Length != 0 ||
            sourceUri.Host is not ("aade.gr" or "www.aade.gr") ||
            !sourceUri.AbsolutePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !sourceUri.AbsolutePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
            sourceUri.Query.Length != 0 || sourceUri.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(manifest.SourceVersion) ||
            manifest.SourceVersion.Length > FiscalReferenceCode.MaxSourceVersionLength ||
            manifest.VerifiedAtUtc == default || manifest.VerifiedAtUtc.Offset != TimeSpan.Zero ||
            manifest.VerifiedAtUtc > timeProvider.GetUtcNow() ||
            manifest.ArtifactSha256 is not { Length: 64 } || !manifest.ArtifactSha256.All(Uri.IsHexDigit) ||
            manifest.Rows is not { Count: > 0 and <= MaximumRows })
        {
            throw Invalid("Η επίσημη πηγή, η έκδοση, το UTC timestamp ή το αποτύπωμα δεν είναι έγκυρα.");
        }

        // Το hash συνδέει τα παρακάτω υποψήφια δεδομένα με συγκεκριμένα bytes, όχι με την αλήθεια της αποδελτίωσης.
        string actualHash;
        await using (var stream = new FileStream(evidenceFullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        }

        if (!string.Equals(actualHash, manifest.ArtifactSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("fiscal.matrix.hash.mismatch",
                "Το πρωτότυπο αρχείο δεν συμφωνεί με το αποτύπωμα SHA-256 του manifest.");
        }

        var rows = new List<FiscalClassificationCandidateRule>(manifest.Rows.Count);
        var seen = new Dictionary<(string, string, string), List<FiscalClassificationCandidateRule>>();
        foreach (FiscalClassificationMatrixRow? item in manifest.Rows)
        {
            if (item is null || !ValidSheet(item.SourceSheet) || !ValidCell(item.SourceCell))
            {
                throw Invalid("Κάθε συνδυασμός απαιτεί έγκυρη αναφορά φύλλου και κελιού XLS.");
            }

            FiscalReferenceCode invoice = FiscalReferenceCode.Create(FiscalReferenceSource.Aade,
                FiscalCodeSystem.MyDataInvoiceType, item.InvoiceType, manifest.SourceVersion,
                item.EffectiveFrom, item.EffectiveUntil);
            FiscalReferenceCode category = FiscalReferenceCode.Create(FiscalReferenceSource.Aade,
                FiscalCodeSystem.MyDataIncomeCategory, item.IncomeCategory, manifest.SourceVersion,
                item.EffectiveFrom, item.EffectiveUntil);
            FiscalReferenceCode type = FiscalReferenceCode.Create(FiscalReferenceSource.Aade,
                FiscalCodeSystem.MyDataIncomeType, item.IncomeType, manifest.SourceVersion,
                item.EffectiveFrom, item.EffectiveUntil);

            var row = new FiscalClassificationCandidateRule(invoice, category, type,
                item.SourceSheet, item.SourceCell);
            var key = (invoice.Code, category.Code, type.Code);
            if (!seen.TryGetValue(key, out List<FiscalClassificationCandidateRule>? existing))
            {
                existing = [];
                seen.Add(key, existing);
            }

            if (existing.Any(previous =>
                (previous.InvoiceType.EffectiveUntil is null ||
                    previous.InvoiceType.EffectiveUntil >= invoice.EffectiveFrom) &&
                (invoice.EffectiveUntil is null ||
                    invoice.EffectiveUntil >= previous.InvoiceType.EffectiveFrom)))
            {
                throw new DomainRuleException("fiscal.matrix.period.overlap",
                    "Υπάρχει διπλός συνδυασμός με επικαλυπτόμενη περίοδο ισχύος.");
            }

            existing.Add(row);
            rows.Add(row);
        }

        return new FiscalClassificationMatrixInspection
        {
            SourceVersion = manifest.SourceVersion.Trim(),
            SourceUrl = sourceUri.AbsoluteUri,
            ArtifactSha256 = actualHash,
            VerifiedAtUtc = manifest.VerifiedAtUtc,
            CandidateRules = Array.AsReadOnly(rows.ToArray())
        };
    }

    private static bool ValidSheet(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 31 &&
        !value.Any(ch => char.IsControl(ch) || ":\\/?!*[]".Contains(ch));

    private static bool ValidCell(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 10)
        {
            return false;
        }

        int i = 0;
        while (i < value.Length && i < 3 && value[i] is >= 'A' and <= 'Z')
        {
            i++;
        }

        if (i == 0 || i == value.Length || value[i] == '0')
        {
            return false;
        }

        return value[i..].All(char.IsAsciiDigit);
    }

    private static DomainRuleException Invalid(string message) =>
        new("fiscal.matrix.manifest.invalid", message);
}
