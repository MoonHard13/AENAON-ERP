using System.Globalization;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Τοπική προεπισκόπηση υποψήφιων συνδυασμών με ακριβές αποτύπωμα του XLS.</summary>
public static class InspectFiscalClassificationMatrixCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        FiscalClassificationMatrixInspectionService service, ILogger logger,
        TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        bool query = args.Length == 12;
        if (args.Length != 4 && !query ||
            args[0] != "--manifest" || args[2] != "--evidence-file" ||
            string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[3]) ||
            query && (args[4] != "--invoice-type" || args[6] != "--income-category" ||
                args[8] != "--income-type" || args[10] != "--date"))
        {
            await error.WriteLineAsync(
                "Χρήση: --inspect-fiscal-matrix --manifest FILE.json --evidence-file SOURCE.xlsx " +
                "[--invoice-type CODE --income-category CODE --income-type CODE --date YYYY-MM-DD]");
            return 2;
        }

        if (query && !DateOnly.TryParseExact(args[11], "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            await error.WriteLineAsync("Η ημερομηνία πρέπει να είναι σε μορφή YYYY-MM-DD.");
            return 2;
        }

        try
        {
            logger.LogInformation("Fiscal classification matrix candidate inspection started");
            FiscalClassificationMatrixInspection inspection = await service.InspectAsync(
                args[1], args[3], cancellationToken);
            await output.WriteLineAsync($"Πηγή: {inspection.SourceUrl}");
            await output.WriteLineAsync($"Έκδοση πηγής: {inspection.SourceVersion}");
            await output.WriteLineAsync($"Υποψήφιοι συνδυασμοί: {inspection.CandidateCount}");
            await output.WriteLineAsync($"SHA-256 XLS: {inspection.ArtifactSha256}");
            if (query)
            {
                DateOnly date = DateOnly.ParseExact(args[11], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
                FiscalClassificationCandidateMatch result = inspection.FindCandidate(
                    args[5], args[7], args[9], date);
                await output.WriteLineAsync(result.PresentInCandidateManifest
                    ? $"Η τριάδα υπάρχει στο μη εγκεκριμένο manifest: {result.SourceSheet}!{result.SourceCell}"
                    : "Η τριάδα δεν υπάρχει στο μη εγκεκριμένο manifest.");
            }

            await output.WriteLineAsync(
                "Δεν πραγματοποιήθηκε έλεγχος πραγματικών XLS κελιών, φορολογική έγκριση, SQL write ή έκδοση.");
            logger.LogInformation("Fiscal candidate matrix inspected: {Count} rows", inspection.CandidateCount);
            return 0;
        }
        catch (DomainRuleException exception)
        {
            await error.WriteLineAsync($"Απόρριψη ({exception.Code}): {exception.Message}");
            logger.LogWarning("Fiscal classification matrix inspection rejected: {RuleCode}", exception.Code);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Αποτυχία πρόσβασης σε τοπικό JSON ή αρχείο XLS.");
            logger.LogWarning("Fiscal classification matrix evidence access failed");
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η προεπισκόπηση υποψήφιων συνδυασμών ακυρώθηκε.");
            return 130;
        }
    }
}
