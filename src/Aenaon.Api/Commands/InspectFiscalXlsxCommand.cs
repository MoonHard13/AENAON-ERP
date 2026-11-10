using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Παρουσιάζει πραγματικές διευθύνσεις και περιεχόμενο κελιών χωρίς φορολογικό χαρακτηρισμό.</summary>
public static class InspectFiscalXlsxCommand
{
    public static async Task<int> ExecuteAsync(string[] args, FiscalXlsxEvidenceInspector inspector,
        ILogger logger, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        bool selected = args is ["--evidence-file", _, "--sheet", _];
        if (args is not ["--evidence-file", _] && !selected ||
            string.IsNullOrWhiteSpace(args[1]) ||
            selected && string.IsNullOrWhiteSpace(args[3]))
        {
            await error.WriteLineAsync(
                "Χρήση: --inspect-fiscal-xlsx --evidence-file SOURCE.xlsx [--sheet SHEET_NAME]");
            return 2;
        }

        try
        {
            logger.LogInformation("Fiscal XLSX structure inspection started");
            FiscalXlsxEvidenceInspection result = await inspector.InspectAsync(args[1],
                cancellationToken);
            await output.WriteLineAsync($"SHA-256 πρωτοτύπου: {result.ArtifactSha256}");
            await output.WriteLineAsync($"Μέγεθος: {result.FileSizeBytes} bytes");
            await output.WriteLineAsync($"Φύλλα: {result.Sheets.Count}");

            bool found = !selected;
            foreach (FiscalXlsxSheetOverview sheet in result.Sheets)
            {
                await output.WriteLineAsync(
                    $"  {Clean(sheet.Name)} — μη κενά κελιά: {sheet.NonEmptyCellCount}");
                if (selected && !string.Equals(sheet.Name, args[3], StringComparison.Ordinal))
                {
                    continue;
                }

                found = true;
                foreach (FiscalXlsxCellSample cell in sheet.SampleCells)
                {
                    string display = Clean(cell.Text);
                    await output.WriteLineAsync(
                        $"    {cell.Address}: {display}{(cell.IsFormula ? " [τύπος Excel]" : string.Empty)}");
                }
            }

            if (!found)
            {
                await error.WriteLineAsync("Δεν βρέθηκε φύλλο με αυτό το ακριβές όνομα.");
                return 2;
            }

            await output.WriteLineAsync(
                "Η δομή διαβάστηκε. Δεν επαληθεύτηκαν συνδυασμοί ΑΑΔΕ, δεν έγιναν SQL writes ή φορολογική έγκριση.");
            logger.LogInformation("Fiscal XLSX structure inspected: {SheetCount} sheets", result.Sheets.Count);
            return 0;
        }
        catch (DomainRuleException exception)
        {
            await error.WriteLineAsync($"Απόρριψη ({exception.Code}): {exception.Message}");
            logger.LogWarning("Fiscal XLSX structure inspection rejected: {RuleCode}", exception.Code);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Δεν είναι δυνατή η ανάγνωση του αρχείου XLSX.");
            logger.LogWarning("Fiscal XLSX input could not be read");
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο έλεγχος XLSX ακυρώθηκε.");
            return 130;
        }
    }

    private static string Clean(string value) =>
        new(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
}
