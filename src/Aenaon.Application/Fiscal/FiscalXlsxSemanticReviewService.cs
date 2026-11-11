using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Fiscal;

/// <summary>Ενδεικτική ταξινόμηση οδηγίας· δεν αποτελεί ερμηνεία του φορολογικού νόμου.</summary>
public enum FiscalSemanticReviewKind
{
    Unclassified = 0,
    AssociatedDocumentDependency = 1,
    PreviousFiscalYearTransfer = 2,
    NextFiscalYearTransfer = 3,
    InheritedByColumnNine = 4,
    NoE3Update = 5,
    OptionalBlank = 6,
    DocumentHeader = 7,
    PublicSectorSpecialCase = 8,
    AccountingBookException = 9
}

/// <summary>Προτεραιότητα για ανθρώπινη εξέταση, όχι φορολογικός κανόνας.</summary>
public enum FiscalSemanticReviewPriority
{
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>Πραγματικό γειτονικό κελί, χωρίς συμπέρασμα για επιτρεπόμενο συνδυασμό.</summary>
public sealed record FiscalSemanticContextCell(
    string Address, string Text, bool IsFormula, int? StyleIndex);

/// <summary>Μία επακριβώς εντοπίσιμη παρατήρηση με εκκρεμή ανθρώπινη αξιολόγηση.</summary>
public sealed record FiscalSemanticReviewItem(
    string EvidenceId,
    string Sheet,
    FiscalXlsxClassificationDirection Direction,
    string Address,
    string ExactText,
    string OriginalReason,
    FiscalSemanticReviewKind SuggestedKind,
    FiscalSemanticReviewPriority Priority,
    string RequiredHumanCheck,
    IReadOnlyList<FiscalSemanticContextCell> NearbyCells)
{
    public bool IsHumanReviewed => false;
    public bool IsFiscalRuleApproved => false;
}

/// <summary>Πλήρης κατάλογος εκκρεμοτήτων για μία συγκεκριμένη έκδοση XLSX.</summary>
public sealed record FiscalSemanticReviewDossier(
    string OriginalXlsxSha256,
    int SheetCount,
    int OriginalCellCount,
    int CandidatePairCount,
    IReadOnlyList<FiscalSemanticReviewItem> Items,
    IReadOnlyList<FiscalSemanticReviewGroup> Groups)
{
    public int PendingHumanReviewCount => Items.Count;
    public int UnclassifiedCount => Items.Count(item =>
        item.SuggestedKind == FiscalSemanticReviewKind.Unclassified);
    public bool RequiresHumanReview => Items.Count != 0;
    public bool IsOriginalAuthenticityVerified => false;
    public bool IsImpactContractValidated => false;
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>Συνοπτικό πλήθος ανά μη δεσμευτική κατηγορία ελέγχου.</summary>
public sealed record FiscalSemanticReviewGroup(
    FiscalSemanticReviewKind Kind,
    FiscalSemanticReviewPriority Priority,
    int Count);

/// <summary>
/// Δημιουργεί τεχνικό φάκελο εκκρεμοτήτων από όλα τα πραγματικά κελιά.
/// Δεν εγκρίνει, απορρίπτει ή ενεργοποιεί ζεύγη myDATA/IMPACT.
/// </summary>
public sealed class FiscalXlsxSemanticReviewService(
    FiscalXlsxClassificationEvidenceAnalyzer analyzer)
{
    public FiscalSemanticReviewDossier Build(FiscalXlsxEvidenceInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!inspection.ExportIsComplete ||
            inspection.ArtifactSha256 is not { Length: 64 } ||
            !inspection.ArtifactSha256.All(Uri.IsHexDigit))
        {
            throw new DomainRuleException("fiscal.semantics.evidence.invalid",
                "Απαιτείται πλήρες τεκμήριο XLSX με πραγματικό SHA-256.");
        }

        FiscalXlsxCandidateAnalysis analysis = analyzer.Analyze(inspection);
        var sheets = inspection.Sheets.ToDictionary(sheet => sheet.Name, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<FiscalSemanticReviewItem>(analysis.RequiresReview.Count);

        foreach (FiscalXlsxReviewObservation observation in analysis.RequiresReview)
        {
            if (!sheets.TryGetValue(observation.Sheet, out FiscalXlsxSheetOverview? sheet) ||
                !sheet.AllCells.Any(cell =>
                    cell.Address == observation.Address &&
                    cell.Text == observation.Text))
            {
                throw new DomainRuleException("fiscal.semantics.evidence.invalid",
                    "Μια παρατήρηση δεν αντιστοιχεί σε πραγματικό κελί του πρωτοτύπου.");
            }

            string key = $"{observation.Sheet}:{(int)observation.Direction}:{observation.Address}";
            if (!seen.Add(key))
            {
                throw new DomainRuleException("fiscal.semantics.evidence.duplicate",
                    "Διπλή παρατήρηση για το ίδιο κελί και κατεύθυνση.");
            }

            FiscalSemanticReviewKind kind = SuggestKind(observation.Text);
            FiscalSemanticReviewPriority priority = Priority(kind);
            string evidenceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{inspection.ArtifactSha256}|{key}|{observation.Text}")))[..24];

            items.Add(new FiscalSemanticReviewItem(
                evidenceId, observation.Sheet, observation.Direction,
                observation.Address, observation.Text, observation.Reason,
                kind, priority, RequiredCheck(kind),
                CollectContext(sheet, observation.Address, observation.Direction)));
        }

        FiscalSemanticReviewGroup[] groups = items
            .GroupBy(item => item.SuggestedKind)
            .Select(group => new FiscalSemanticReviewGroup(
                group.Key, Priority(group.Key), group.Count()))
            .OrderByDescending(group => group.Priority)
            .ThenBy(group => group.Kind)
            .ToArray();

        return new FiscalSemanticReviewDossier(
            inspection.ArtifactSha256,
            inspection.Sheets.Count,
            inspection.TotalNonEmptyCellCount,
            analysis.CandidatePairs.Count,
            items.AsReadOnly(),
            Array.AsReadOnly(groups));
    }

    public static FiscalSemanticReviewKind SuggestKind(string text)
    {
        string normalized = Normalize(text ?? string.Empty);
        if (normalized.Contains("ΣΥΣΧΕΤΙΖΟΜΕΝ", StringComparison.Ordinal) ||
            normalized.Contains("ΣΥΜΠΛΗΡΩΝΕΤΑΙ ΑΝΑ ΠΕΡΙΠΤΩΣΗ ΤΥΠΟΥ ΠΑΡΑΣΤΑΤΙΚΩΝ",
                StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.AssociatedDocumentDependency;
        }

        if (normalized.Contains("ΜΕΤΑΦΟΡΑ ΠΡΟΣ ΥΠΟΛΟΓΙΣΜΟ", StringComparison.Ordinal) &&
            normalized.Contains("ΠΡΟΗΓΟΥΜΕΝΗ ΧΡΗΣΗ", StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.PreviousFiscalYearTransfer;
        }

        if (normalized.Contains("ΜΕΤΑΦΟΡΑ ΠΡΟΣ ΥΠΟΛΟΓΙΣΜΟ", StringComparison.Ordinal) &&
            normalized.Contains("ΕΠΟΜΕΝΗ ΧΡΗΣΗ", StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.NextFiscalYearTransfer;
        }

        if (normalized.Contains("ΕΠΙΤΡΕΠΤΟΙ ΟΙ ΠΑΡΑΠΑΝΩ ΧΑΡΑΚΤΗΡΙΣΜΟΙ",
            StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.InheritedByColumnNine;
        }

        if (normalized.Contains("E3", StringComparison.Ordinal) &&
            (normalized.Contains("ΔΕΝ ΕΝΗΜΕΡΩΝΕΙ", StringComparison.Ordinal) ||
             normalized.Contains("ΔΕΝ ΓΙΝΕΤΑΙ ΧΑΡΑΚΤΗΡΙΣΜΟΣ", StringComparison.Ordinal)))
        {
            return FiscalSemanticReviewKind.NoE3Update;
        }

        if (normalized is "Η ΚΕΝΟ" or "ΚΕΝΟ")
        {
            return FiscalSemanticReviewKind.OptionalBlank;
        }

        if (normalized.Contains("ΔΗΜΟΣΙΟ", StringComparison.Ordinal) &&
            normalized.Contains("ΠΟΣΟ", StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.PublicSectorSpecialCase;
        }

        if (normalized.Contains("ΔΕΝ ΕΝΗΜΕΡΩΝΕΙ ΤΟ ΑΝΑΛΥΤΙΚΟ ΒΙΒΛΙΟ",
            StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.AccountingBookException;
        }

        if (normalized.StartsWith("ΕΠΙΤΡΕΠΤΕΣ ΤΙΜΕΣ", StringComparison.Ordinal) ||
            normalized.StartsWith("ΧΑΡΑΚΤΗΡΙΣΜΟΙ ΕΣΟΔΩΝ", StringComparison.Ordinal) ||
            normalized.StartsWith("ΧΑΡΑΚΤΗΡΙΣΜΟΙ ΕΞΟΔΩΝ", StringComparison.Ordinal) ||
            normalized.StartsWith("ΤΙΜΟΛΟΓΙΟ ΠΩΛΗΣΗΣ", StringComparison.Ordinal))
        {
            return FiscalSemanticReviewKind.DocumentHeader;
        }

        return FiscalSemanticReviewKind.Unclassified;
    }

    private static FiscalSemanticReviewPriority Priority(FiscalSemanticReviewKind kind) =>
        kind switch
        {
            FiscalSemanticReviewKind.AssociatedDocumentDependency or
            FiscalSemanticReviewKind.PreviousFiscalYearTransfer or
            FiscalSemanticReviewKind.NextFiscalYearTransfer or
            FiscalSemanticReviewKind.InheritedByColumnNine or
            FiscalSemanticReviewKind.Unclassified => FiscalSemanticReviewPriority.Critical,
            FiscalSemanticReviewKind.NoE3Update or
            FiscalSemanticReviewKind.PublicSectorSpecialCase or
            FiscalSemanticReviewKind.AccountingBookException => FiscalSemanticReviewPriority.High,
            _ => FiscalSemanticReviewPriority.Normal
        };

    private static string RequiredCheck(FiscalSemanticReviewKind kind) => kind switch
    {
        FiscalSemanticReviewKind.AssociatedDocumentDependency =>
            "Επιβεβαίωση τύπου και πραγματικού συσχετιζόμενου παραστατικού από λογιστή και contract ΑΑΔΕ/IMPACT.",
        FiscalSemanticReviewKind.PreviousFiscalYearTransfer =>
            "Χειροκίνητος έλεγχος περιόδου και μεταφοράς στην προηγούμενη φορολογική χρήση.",
        FiscalSemanticReviewKind.NextFiscalYearTransfer =>
            "Χειροκίνητος έλεγχος περιόδου και μεταφοράς στην επόμενη φορολογική χρήση.",
        FiscalSemanticReviewKind.InheritedByColumnNine =>
            "Ερμηνεία της στήλης 9 και έλεγχος προηγούμενων χαρακτηρισμών, χωρίς αυτόματη κληρονομιά.",
        FiscalSemanticReviewKind.NoE3Update =>
            "Επιβεβαίωση ότι δεν ενημερώνεται E3 και διαχωρισμός από υποχρεωτικό myDATA payload.",
        FiscalSemanticReviewKind.OptionalBlank =>
            "Έλεγχος πότε επιτρέπεται κενό πεδίο και τι απαιτεί το αντίστοιχο API.",
        FiscalSemanticReviewKind.DocumentHeader =>
            "Αναγνώριση επικεφαλίδας/ορίου ενότητας, όχι δημιουργία φορολογικού κανόνα.",
        FiscalSemanticReviewKind.PublicSectorSpecialCase =>
            "Επιθεώρηση ειδικής συναλλαγής Δημοσίου και χωριστών απαιτήσεων B2G.",
        FiscalSemanticReviewKind.AccountingBookException =>
            "Ανθρώπινος έλεγχος επίδρασης στο Αναλυτικό Βιβλίο ΑΑΔΕ.",
        _ => "Μη αναγνωρισμένη οδηγία: απαιτείται ρητή τεχνική και φοροτεχνική εξέταση."
    };

    private static IReadOnlyList<FiscalSemanticContextCell> CollectContext(
        FiscalXlsxSheetOverview sheet, string address, FiscalXlsxClassificationDirection direction)
    {
        int row = ParseRow(address);
        string[] columns = direction == FiscalXlsxClassificationDirection.Income
            ? ["B", "C"]
            : ["E", "F"];
        FiscalSemanticContextCell[] nearby = sheet.AllCells
            .Where(cell => Math.Abs(ParseRow(cell.Address) - row) <= 2 &&
                columns.Any(col => cell.Address.StartsWith(col, StringComparison.Ordinal)))
            .Take(12)
            .Select(cell => new FiscalSemanticContextCell(
                cell.Address, cell.Text, cell.IsFormula, cell.StyleIndex))
            .ToArray();
        return Array.AsReadOnly(nearby);
    }

    private static int ParseRow(string address)
    {
        int start = 0;
        while (start < address.Length && address[start] is >= 'A' and <= 'Z')
        {
            start++;
        }

        return int.TryParse(address[start..], NumberStyles.None,
            CultureInfo.InvariantCulture, out int row) ? row : 0;
    }

    private static string Normalize(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}
