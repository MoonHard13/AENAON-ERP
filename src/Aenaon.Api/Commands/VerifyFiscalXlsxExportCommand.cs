using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Ελέγχει την τεχνική συμφωνία πλήρους JSON με το πραγματικό πρωτότυπο XLSX.</summary>
public static class VerifyFiscalXlsxExportCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        FiscalXlsxEvidenceReconciliationService verifier,
        ILogger logger, TextWriter output, TextWriter error,
        CancellationToken cancellationToken)
    {
        string? original = null;
        string? report = null;
        bool invalid = false;

        for (int index = 0; index < args.Length && !invalid;)
        {
            string option = args[index++];
            if (index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }

            string value = args[index++];
            switch (option)
            {
                case "--evidence-file" when original is null:
                    original = value;
                    break;
                case "--evidence-json" when report is null:
                    report = value;
                    break;
                default:
                    invalid = true;
                    break;
            }
        }

        if (invalid || string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(report))
        {
            await error.WriteLineAsync(
                "Χρήση: --verify-fiscal-xlsx-export --evidence-file SOURCE.xlsx " +
                "--evidence-json COMPLETE.json");
            return 2;
        }

        try
        {
            logger.LogInformation("Fiscal XLSX-to-JSON independent reconciliation started");
            FiscalXlsxEvidenceReconciliationResult result =
                await verifier.VerifyAsync(original, report, cancellationToken);
            await output.WriteLineAsync("ΤΕΧΝΙΚΗ ΣΥΜΦΩΝΙΑ ΠΡΩΤΟΤΥΠΟΥ XLSX / JSON: ΕΠΙΤΥΧΗΣ");
            await output.WriteLineAsync($"SHA-256: {result.ArtifactSha256}");
            await output.WriteLineAsync($"Φύλλα: {result.SheetCount}");
            await output.WriteLineAsync($"Κελιά: {result.CellCount}");
            await output.WriteLineAsync($"Υποψήφια ζεύγη: {result.CandidatePairCount}");
            await output.WriteLineAsync($"Παρατηρήσεις προς έλεγχο: {result.ManualReviewCount}");
            await output.WriteLineAsync(
                "Δεν επιβεβαιώθηκε προέλευση από ΑΑΔΕ, φορολογική εγκυρότητα ή IMPACT συμβατότητα.");
            await output.WriteLineAsync("Δεν έγινε SQL write, έγκριση κανόνων ή έκδοση.");
            logger.LogInformation(
                "Fiscal XLSX-to-JSON reconciliation matched: {Sheets} sheets, {Cells} cells",
                result.SheetCount, result.CellCount);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο τεχνικός έλεγχος συμφωνίας ακυρώθηκε.");
            return 130;
        }
        catch (DomainRuleException exception)
        {
            await error.WriteLineAsync($"Απόρριψη ({exception.Code}): {exception.Message}");
            logger.LogWarning("Fiscal reconciliation rejected: {RuleCode}", exception.Code);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Αποτυχία ανάγνωσης των δύο τοπικών τεκμηρίων.");
            logger.LogWarning("Fiscal reconciliation file access failed");
            return 1;
        }
    }
}
