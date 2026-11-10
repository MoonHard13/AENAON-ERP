using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Αναλύει τοπικά ένα υποψήφιο fiscal manifest χωρίς αλλαγή στη βάση.</summary>
public static class InspectFiscalImportCommand
{
    public static async Task<int> ExecuteAsync(string[] args, FiscalImportInspectionService service,
        ILogger logger, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args is not ["--manifest", var manifest, "--evidence-file", var evidence] ||
            string.IsNullOrWhiteSpace(manifest) || string.IsNullOrWhiteSpace(evidence))
        {
            await error.WriteLineAsync(
                "Χρήση: --inspect-fiscal-import --manifest ΑΡΧΕΙΟ.json --evidence-file ΕΠΙΣΗΜΟ_ΤΕΚΜΗΡΙΟ");
            return 2;
        }

        try
        {
            logger.LogInformation("Fiscal import evidence inspection started");
            FiscalImportInspection result = await service.InspectAsync(manifest, evidence, cancellationToken);
            await output.WriteLineAsync($"Πηγή: {result.Source}");
            await output.WriteLineAsync($"Σύστημα: {result.CodeSystem}");
            await output.WriteLineAsync($"Έκδοση: {result.SourceVersion}");
            await output.WriteLineAsync($"Υποψήφιες γραμμές: {result.CandidateCount}");
            await output.WriteLineAsync($"SHA-256 τεκμηρίου: {result.ArtifactSha256}");
            await output.WriteLineAsync(
                "Ο έλεγχος αρχείων ολοκληρώθηκε. Δεν έγινε εισαγωγή σε SQL ή φορολογική έγκριση.");
            logger.LogInformation("Fiscal import evidence inspected: {CandidateCount} rows, {CodeSystem}",
                result.CandidateCount, result.CodeSystem);
            return 0;
        }
        catch (DomainRuleException exception)
        {
            await error.WriteLineAsync($"Απόρριψη ({exception.Code}): {exception.Message}");
            logger.LogWarning("Fiscal import inspection rejected: {RuleCode}", exception.Code);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Αδυναμία πρόσβασης σε αρχείο τεκμηρίου ή manifest.");
            logger.LogWarning("Fiscal import inspection could not access input");
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η προεπισκόπηση εισαγωγής ακυρώθηκε.");
            logger.LogInformation("Fiscal import inspection cancelled");
            return 130;
        }
    }
}
