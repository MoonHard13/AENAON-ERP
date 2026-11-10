namespace Aenaon.Application.Fiscal;

/// <summary>Διακριτή κατεύθυνση της χειροκίνητης αποτύπωσης χαρακτηρισμών.</summary>
public enum FiscalXlsxClassificationDirection
{
    Income = 1,
    Expense = 2
}

/// <summary>Πιθανός συνδυασμός κελιών, ποτέ εγκεκριμένος φορολογικός κανόνας.</summary>
public sealed record FiscalXlsxCandidatePair(
    string Sheet,
    FiscalXlsxClassificationDirection Direction,
    string CategoryCode,
    string ClassificationCode,
    string CategoryCell,
    string ClassificationCell);

/// <summary>Κελί που απαιτεί φορολογική ερμηνεία ή έλεγχο διάταξης.</summary>
public sealed record FiscalXlsxReviewObservation(
    string Sheet,
    FiscalXlsxClassificationDirection Direction,
    string Address,
    string Text,
    string Reason);

/// <summary>Αποτέλεσμα προσεκτικής εξαγωγής υποψηφίων χωρίς δικαίωμα φορολογικής χρήσης.</summary>
public sealed record FiscalXlsxCandidateAnalysis(
    IReadOnlyList<FiscalXlsxCandidatePair> CandidatePairs,
    IReadOnlyList<FiscalXlsxReviewObservation> RequiresReview)
{
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>
/// Απομονώνει τις στήλες B/C (έσοδα) και E/F (έξοδα) όπως παρατηρήθηκαν στο XLSX.
/// Η διάταξη δεν έχει ακόμη επαληθευτεί ως καθολικός φορολογικός κανόνας ΑΑΔΕ.
/// </summary>
public sealed class FiscalXlsxClassificationEvidenceAnalyzer
{
    public FiscalXlsxCandidateAnalysis Analyze(FiscalXlsxEvidenceInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        var candidates = new List<FiscalXlsxCandidatePair>();
        var observations = new List<FiscalXlsxReviewObservation>();
        foreach (FiscalXlsxSheetOverview sheet in inspection.Sheets)
        {
            // Κρατάμε πλήρως διακριτές τις δύο πλευρές, χωρίς ανάμειξη εσόδων/εξόδων.
            InspectSide(sheet, "B", "C", FiscalXlsxClassificationDirection.Income,
                candidates, observations);
            InspectSide(sheet, "E", "F", FiscalXlsxClassificationDirection.Expense,
                candidates, observations);
        }

        return new FiscalXlsxCandidateAnalysis(candidates.AsReadOnly(), observations.AsReadOnly());
    }

    private static void InspectSide(FiscalXlsxSheetOverview sheet, string categoryColumn,
        string codeColumn, FiscalXlsxClassificationDirection direction,
        List<FiscalXlsxCandidatePair> candidates,
        List<FiscalXlsxReviewObservation> observations)
    {
        var cells = sheet.AllCells.Select(cell => new
            {
                Cell = cell,
                Column = CellColumn(cell.Address),
                Row = CellRow(cell.Address)
            })
            .Where(item => item.Row >= 7 &&
                (item.Column == categoryColumn || item.Column == codeColumn))
            .GroupBy(item => item.Row)
            .OrderBy(group => group.Key);

        string? category = null;
        string? categoryAddress = null;
        foreach (var group in cells)
        {
            FiscalXlsxCellSample? categoryCell = group
                .FirstOrDefault(cell => cell.Column == categoryColumn)?.Cell;
            FiscalXlsxCellSample? codeCell = group
                .FirstOrDefault(cell => cell.Column == codeColumn)?.Cell;

            if (categoryCell is not null)
            {
                string value = categoryCell.Text.Trim();
                if (!categoryCell.IsFormula && IsCategory(value, direction))
                {
                    category = value.ToUpperInvariant();
                    categoryAddress = categoryCell.Address;
                }
                else
                {
                    category = null;
                    categoryAddress = null;
                    observations.Add(new FiscalXlsxReviewObservation(sheet.Name, direction,
                        categoryCell.Address, categoryCell.Text,
                        "Μη τυπική κατηγορία ή οδηγία: απαιτείται έλεγχος"));
                }
            }

            if (codeCell is null)
            {
                continue;
            }

            string classification = codeCell.Text.Trim();
            if (!codeCell.IsFormula && IsE3(classification) &&
                category is not null && categoryAddress is not null)
            {
                candidates.Add(new FiscalXlsxCandidatePair(sheet.Name, direction,
                    category, classification.ToUpperInvariant(), categoryAddress, codeCell.Address));
            }
            else
            {
                observations.Add(new FiscalXlsxReviewObservation(sheet.Name, direction,
                    codeCell.Address, codeCell.Text,
                    codeCell.IsFormula ? "Μη έμπιστη τιμή τύπου Excel" :
                    category is null ? "Απουσία έγκυρης ενεργής κατηγορίας" :
                    "Επεξηγηματική οδηγία ή μη κωδικοποιημένη τιμή E3"));

                // Οι επεξηγηματικές οδηγίες δεν κληρονομούν έγκριση από προηγούμενες γραμμές.
                if (!IsE3(classification))
                {
                    category = null;
                    categoryAddress = null;
                }
            }
        }
    }

    private static bool IsCategory(string value, FiscalXlsxClassificationDirection direction)
    {
        string category = value.ToUpperInvariant();
        return direction switch
        {
            FiscalXlsxClassificationDirection.Income =>
                category == "CATEGORY3" || HasNumericSuffix(category, "CATEGORY1_"),
            FiscalXlsxClassificationDirection.Expense => HasNumericSuffix(category, "CATEGORY2_"),
            _ => false
        };
    }

    private static bool IsE3(string value) =>
        HasNumericSuffix(value.ToUpperInvariant(), "E3_");

    private static bool HasNumericSuffix(string text, string prefix)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal) || text.Length == prefix.Length)
        {
            return false;
        }

        string remainder = text[prefix.Length..];
        return remainder[0] != '_' && remainder[^1] != '_' &&
            remainder.All(ch => char.IsAsciiDigit(ch) || ch == '_');
    }

    private static string CellColumn(string address)
    {
        int index = 0;
        while (index < address.Length && address[index] is >= 'A' and <= 'Z')
        {
            index++;
        }

        return address[..index];
    }

    private static int CellRow(string address)
    {
        int index = 0;
        while (index < address.Length && address[index] is >= 'A' and <= 'Z')
        {
            index++;
        }

        return int.TryParse(address[index..], out int row) ? row : 0;
    }
}
