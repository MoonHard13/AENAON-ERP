using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Επιθεώρηση XLSX με πλήρη JSON εξαγωγή και σαφή διαφορά δειγμάτων/κελιών.</summary>
public static class InspectFiscalXlsxCommand
{
    public static async Task<int> ExecuteAsync(string[] args, FiscalXlsxEvidenceInspector inspector,
        ILogger logger, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        string? evidencePath = null;
        string? sheetName = null;
        string? jsonPath = null;
        bool all = false;
        bool invalid = false;

        // Τα CLI flags έχουν μία μοναδική σημασία ανεξάρτητα από τη σειρά τους.
        for (int index = 0; index < args.Length && !invalid;)
        {
            string option = args[index++];
            if (option == "--all")
            {
                invalid = all;
                all = true;
                continue;
            }

            if (index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }

            string value = args[index++];
            switch (option)
            {
                case "--evidence-file" when evidencePath is null:
                    evidencePath = value;
                    break;
                case "--sheet" when sheetName is null:
                    sheetName = value;
                    break;
                case "--export-json" when jsonPath is null:
                    jsonPath = value;
                    break;
                default:
                    invalid = true;
                    break;
            }
        }

        if (invalid || string.IsNullOrWhiteSpace(evidencePath) ||
            sheetName is not null && string.IsNullOrWhiteSpace(sheetName) ||
            jsonPath is not null && string.IsNullOrWhiteSpace(jsonPath))
        {
            await error.WriteLineAsync(
                "Χρήση: --inspect-fiscal-xlsx --evidence-file SOURCE.xlsx " +
                "[--sheet NAME] [--all] [--export-json NEW_FILE.json]");
            return 2;
        }

        try
        {
            logger.LogInformation("Fiscal XLSX structure inspection started");
            FiscalXlsxEvidenceInspection result = await inspector.InspectAsync(evidencePath,
                cancellationToken);

            if (sheetName is not null &&
                !result.Sheets.Any(sheet => string.Equals(sheet.Name, sheetName,
                    StringComparison.Ordinal)))
            {
                await error.WriteLineAsync("Δεν βρέθηκε φύλλο με αυτό το ακριβές όνομα.");
                return 2;
            }

            var exporter = new FiscalXlsxEvidenceExportService(
                new FiscalXlsxClassificationEvidenceAnalyzer());
            FiscalXlsxEvidenceExport report = exporter.Build(result);

            if (jsonPath is not null)
            {
                await exporter.ExportNewAsync(result, jsonPath, cancellationToken);
                logger.LogInformation("Fiscal XLSX full JSON export completed: {Sheets} sheets, {Cells} cells",
                    report.SheetCount, report.ExportedCellCount);
            }

            await output.WriteLineAsync($"SHA-256 πρωτοτύπου: {result.ArtifactSha256}");
            await output.WriteLineAsync($"Μέγεθος: {result.FileSizeBytes} bytes");
            await output.WriteLineAsync($"Φύλλα: {result.Sheets.Count}");
            await output.WriteLineAsync(
                $"Πληρότητα ανάγνωσης: {report.ExportedCellCount}/{report.NonEmptyCellCount} " +
                $"κελιά — {(report.IsComplete ? "ΠΛΗΡΗΣ" : "ΑΤΕΛΗΣ")}");

            foreach (FiscalXlsxSheetOverview sheet in result.Sheets)
            {
                await output.WriteLineAsync($"  {Clean(sheet.Name)} — μη κενά κελιά: " +
                    $"{sheet.NonEmptyCellCount}, συλλέχθηκαν: {sheet.AllCells.Count}, " +
                    $"συγχωνεύσεις: {sheet.MergedRanges.Count}");

                if (sheetName is not null &&
                    !string.Equals(sheet.Name, sheetName, StringComparison.Ordinal))
                {
                    continue;
                }

                IReadOnlyList<FiscalXlsxCellSample> displayed =
                    all ? sheet.AllCells : sheet.SampleCells;
                foreach (FiscalXlsxCellSample cell in displayed)
                {
                    await output.WriteLineAsync($"    {cell.Address}: {Clean(cell.Text)}" +
                        $"{(cell.IsFormula ? " [τύπος Excel]" : string.Empty)}");
                }

                if (!all && sheet.NonEmptyCellCount > displayed.Count)
                {
                    await output.WriteLineAsync(
                        $"    [Προεπισκόπηση: {displayed.Count}/{sheet.NonEmptyCellCount} κελιά. " +
                        "Χρησιμοποίησε --all ή --export-json για όλες τις τιμές.]");
                }
            }

            await output.WriteLineAsync(
                $"Υποψήφια ζεύγη εσόδων/εξόδων: {report.ClassificationEvidence.CandidatePairs.Count}. " +
                $"Εγγραφές προς ανθρώπινο έλεγχο: {report.ClassificationEvidence.RequiresReview.Count}.");
            if (jsonPath is not null)
            {
                await output.WriteLineAsync($"Δημιουργήθηκε πλήρες JSON: {jsonPath}");
            }

            await output.WriteLineAsync(
                "Δεν έγινε φορολογική έγκριση, αντιπαραβολή κανόνων IMPACT, SQL write ή έκδοση.");
            logger.LogInformation("Fiscal XLSX inspected: {Sheets} sheets, {Cells} cells",
                result.Sheets.Count, result.TotalNonEmptyCellCount);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο έλεγχος XLSX ακυρώθηκε.");
            logger.LogInformation("Fiscal XLSX inspection cancelled");
            return 130;
        }
        catch (DomainRuleException exception)
        {
            await error.WriteLineAsync($"Απόρριψη ({exception.Code}): {exception.Message}");
            logger.LogWarning("Fiscal XLSX inspection rejected: {RuleCode}", exception.Code);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Δεν είναι δυνατή η ανάγνωση του XLSX ή η ασφαλής εξαγωγή JSON.");
            logger.LogWarning("Fiscal XLSX input/output operation failed");
            return 1;
        }
    }

    private static string Clean(string value) =>
        new(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
}
