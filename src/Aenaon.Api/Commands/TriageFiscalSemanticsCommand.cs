using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Δημιουργεί read-only κατάλογο ανθρώπινης επιθεώρησης από επαληθευμένο XLSX/JSON.</summary>
public static class TriageFiscalSemanticsCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        FiscalXlsxEvidenceReconciliationService reconciliation,
        FiscalXlsxEvidenceInspector inspector,
        FiscalXlsxSemanticReviewService semanticService,
        FiscalSemanticReviewDossierWriter writer,
        ILogger logger, TextWriter output, TextWriter error,
        CancellationToken cancellationToken)
    {
        string? evidenceFile = null;
        string? evidenceJson = null;
        string? reportJson = null;
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
                case "--evidence-file" when evidenceFile is null:
                    evidenceFile = value;
                    break;
                case "--evidence-json" when evidenceJson is null:
                    evidenceJson = value;
                    break;
                case "--report-json" when reportJson is null:
                    reportJson = value;
                    break;
                default:
                    invalid = true;
                    break;
            }
        }

        if (invalid || string.IsNullOrWhiteSpace(evidenceFile) ||
            string.IsNullOrWhiteSpace(evidenceJson) ||
            reportJson is not null && string.IsNullOrWhiteSpace(reportJson))
        {
            await error.WriteLineAsync(
                "Χρήση: --triage-fiscal-semantics --evidence-file SOURCE.xlsx " +
                "--evidence-json COMPLETE.json [--report-json NEW_REVIEW.json]");
            return 2;
        }

        try
        {
            logger.LogInformation("Fiscal semantic triage started with original file reconciliation");
            FiscalXlsxEvidenceReconciliationResult proof =
                await reconciliation.VerifyAsync(evidenceFile, evidenceJson, cancellationToken);
            FiscalXlsxEvidenceInspection original =
                await inspector.InspectAsync(evidenceFile, cancellationToken);
            if (!string.Equals(proof.ArtifactSha256, original.ArtifactSha256,
                    StringComparison.Ordinal) ||
                proof.SheetCount != original.Sheets.Count ||
                proof.CellCount != original.TotalExportedCellCount)
            {
                throw new DomainRuleException("fiscal.semantics.evidence.changed",
                    "Το πρωτότυπο XLSX άλλαξε ανάμεσα στον έλεγχο και στη δημιουργία της αναφοράς.");
            }

            FiscalSemanticReviewDossier dossier = semanticService.Build(original);
            if (proof.CandidatePairCount != dossier.CandidatePairCount ||
                proof.ManualReviewCount != dossier.PendingHumanReviewCount)
            {
                throw new DomainRuleException("fiscal.semantics.evidence.changed",
                    "Τα αναλυτικά αποτελέσματα διαφέρουν από τον ανεξάρτητο έλεγχο.");
            }

            if (reportJson is not null)
            {
                // Το report είναι αρχείο εργασίας, χωρίς καταχώρηση απόφασης ή SQL.
                await writer.WriteNewAsync(dossier, reportJson, cancellationToken);
            }

            await output.WriteLineAsync("ΤΕΧΝΙΚΗ ΣΥΜΦΩΝΙΑ XLSX / JSON: ΕΠΙΤΥΧΗΣ");
            await output.WriteLineAsync($"SHA-256: {dossier.OriginalXlsxSha256}");
            await output.WriteLineAsync($"Φύλλα: {dossier.SheetCount}");
            await output.WriteLineAsync($"Κελιά: {dossier.OriginalCellCount}");
            await output.WriteLineAsync($"Μη εγκεκριμένα υποψήφια ζεύγη: {dossier.CandidatePairCount}");
            await output.WriteLineAsync($"Ανοιχτές παρατηρήσεις: {dossier.PendingHumanReviewCount}");
            foreach (FiscalSemanticReviewGroup group in dossier.Groups)
            {
                await output.WriteLineAsync(
                    $"  {group.Kind}: {group.Count} — προτεραιότητα {group.Priority}");
            }

            if (reportJson is not null)
            {
                await output.WriteLineAsync($"Νέος φάκελος ανθρώπινου ελέγχου: {reportJson}");
            }

            await output.WriteLineAsync(
                "Όλα τα review items είναι Pending. Δεν εγκρίθηκε κανένας φορολογικός κανόνας.");
            await output.WriteLineAsync(
                "Δεν πραγματοποιήθηκε IMPACT/ΑΑΔΕ request, SQL write ή έκδοση παραστατικού.");
            logger.LogInformation(
                "Fiscal semantic triage completed: {Candidates} candidates, {Pending} pending, {Unknown} unclassified",
                dossier.CandidatePairCount, dossier.PendingHumanReviewCount,
                dossier.UnclassifiedCount);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο έλεγχος φορολογικών παρατηρήσεων ακυρώθηκε.");
            return 130;
        }
        catch (DomainRuleException exception)
        {
            await error.WriteLineAsync($"Απόρριψη ({exception.Code}): {exception.Message}");
            logger.LogWarning("Fiscal semantic triage rejected: {RuleCode}", exception.Code);
            return 2;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Αποτυχία πρόσβασης στα τοπικά αρχεία τεκμηρίων/αναφοράς.");
            logger.LogWarning("Fiscal semantic triage file access failed");
            return 1;
        }
    }
}
