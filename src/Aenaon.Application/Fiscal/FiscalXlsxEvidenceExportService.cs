using System.Text.Json;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Fiscal;

/// <summary>Πλήρης εξαγωγή τεκμηρίου με όλες τις τιμές κελιών και ξεχωριστές υποψήφιες ενδείξεις.</summary>
public sealed record FiscalXlsxEvidenceExport(
    string ArtifactSha256,
    long FileSizeBytes,
    int SheetCount,
    int NonEmptyCellCount,
    int ExportedCellCount,
    bool IsComplete,
    IReadOnlyList<FiscalXlsxSheetExport> Sheets,
    FiscalXlsxCandidateAnalysis ClassificationEvidence)
{
    public bool IsClassificationMatrixVerified => false;
    public bool IsFiscalRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>Πλήρης καταγραφή πραγματικού worksheet, χωρίς σιωπηρή περικοπή τιμών.</summary>
public sealed record FiscalXlsxSheetExport(
    string Name,
    int NonEmptyCellCount,
    int ExportedCellCount,
    bool IsComplete,
    IReadOnlyList<string> MergedRanges,
    bool HasStyledCells,
    IReadOnlyList<FiscalXlsxCellSample> Cells);

/// <summary>
/// Δημιουργεί νέο JSON μόνο μετά από πλήρη επιθεώρηση.
/// Δεν τροποποιεί ποτέ το επίσημο XLSX ούτε αντικαθιστά υπάρχον export.
/// </summary>
public sealed class FiscalXlsxEvidenceExportService(
    FiscalXlsxClassificationEvidenceAnalyzer analyzer)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FiscalXlsxEvidenceExport Build(FiscalXlsxEvidenceInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!inspection.ExportIsComplete)
        {
            throw new DomainRuleException("fiscal.xlsx.export.incomplete",
                "Διαφορά πλήθους κελιών μεταξύ ανάγνωσης και εξαγωγής.");
        }

        FiscalXlsxSheetExport[] sheets = inspection.Sheets.Select(sheet =>
            new FiscalXlsxSheetExport(sheet.Name, sheet.NonEmptyCellCount,
                sheet.AllCells.Count, sheet.ExportIsComplete, sheet.MergedRanges,
                sheet.HasStyledCells, sheet.AllCells)).ToArray();

        return new FiscalXlsxEvidenceExport(inspection.ArtifactSha256,
            inspection.FileSizeBytes, sheets.Length, inspection.TotalNonEmptyCellCount,
            inspection.TotalExportedCellCount, inspection.ExportIsComplete,
            Array.AsReadOnly(sheets), analyzer.Analyze(inspection));
    }

    public async Task<FiscalXlsxEvidenceExport> ExportNewAsync(
        FiscalXlsxEvidenceInspection inspection, string jsonPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) ||
            !string.Equals(Path.GetExtension(jsonPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("fiscal.xlsx.export.path.invalid",
                "Απαιτείται νέο αρχείο εξαγωγής με κατάληξη .json.");
        }

        FiscalXlsxEvidenceExport report = Build(inspection);
        string fullPath = Path.GetFullPath(jsonPath);
        bool created = false;
        try
        {
            // CreateNew: δεν αντικαθιστούμε προηγούμενη εξαγωγή ή πρωτότυπο τεκμήριο.
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                created = true;
                await JsonSerializer.SerializeAsync(stream, report, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            return report;
        }
        catch
        {
            if (created)
            {
                // Απομακρύνουμε μόνο το δικό μας μερικώς δημιουργημένο JSON.
                try
                {
                    File.Delete(fullPath);
                }
                catch (IOException)
                {
                    // Η αρχική εξαίρεση διατηρείται, το σφάλμα cleanup δεν την αποκρύπτει.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ο διαχειριστής μπορεί να απομακρύνει το υπολειπόμενο προσωρινό αρχείο.
                }
            }

            throw;
        }
    }
}
