using System.Text.Json;
using Aenaon.Application.Fiscal;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Απομονωμένος έλεγχος τοπικού JSON χωρίς κλήση παρόχου ή SQL.</summary>
public static class InspectImpactPayloadCommand
{
    private const long MaximumPayloadBytes = 4L * 1024 * 1024;
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> ExecuteAsync(
        string[] args,
        ImpactOfflinePayloadInspector inspector,
        ILogger logger,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        string? profileName = null;
        string? scenarioName = null;
        string? payloadPath = null;
        string? reportPath = null;
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
                case "--profile" when profileName is null:
                    profileName = value;
                    break;
                case "--scenario" when scenarioName is null:
                    scenarioName = value;
                    break;
                case "--payload-file" when payloadPath is null:
                    payloadPath = value;
                    break;
                case "--report-json" when reportPath is null:
                    reportPath = value;
                    break;
                default:
                    invalid = true;
                    break;
            }
        }

        if (invalid || !TryProfile(profileName, out ImpactContractProfile profile) ||
            !TryScenario(scenarioName, out ImpactCounterpartyScenario scenario) ||
            string.IsNullOrWhiteSpace(payloadPath) ||
            !string.Equals(Path.GetExtension(payloadPath), ".json",
                StringComparison.OrdinalIgnoreCase) ||
            reportPath is not null &&
            (string.IsNullOrWhiteSpace(reportPath) ||
                !string.Equals(Path.GetExtension(reportPath), ".json",
                    StringComparison.OrdinalIgnoreCase)))
        {
            await error.WriteLineAsync(
                "Χρήση: --inspect-impact-payload --profile base|invoice|b2g " +
                "--scenario unknown|business|retail --payload-file SYNTHETIC.json " +
                "[--report-json NEW_REPORT.json]");
            return 2;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string inputPath = Path.GetFullPath(payloadPath);
            await using var stream = new FileStream(inputPath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumPayloadBytes)
            {
                await error.WriteLineAsync("Το JSON είναι κενό ή υπερβαίνει τα 4 MiB.");
                return 2;
            }

            byte[] bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            if (stream.Length != bytes.Length)
            {
                await error.WriteLineAsync("Το αρχείο άλλαξε κατά την ανάγνωση.");
                return 2;
            }

            // Η εξαίρεση JSON δεν επιτρέπει μερική/σιωπηρή αποδοχή malformed payload.
            ImpactOfflinePayloadReport report = inspector.Inspect(
                bytes, profile, scenario, cancellationToken);

            if (reportPath is not null)
            {
                await SaveReportNewAsync(report, reportPath, cancellationToken);
            }

            await output.WriteLineAsync("IMPACT OFFLINE STRUCTURAL PREFLIGHT");
            await output.WriteLineAsync($"Profile: {report.Profile}; Scenario: {report.CounterpartyScenario}");
            await output.WriteLineAsync($"Γραμμές Details: {report.LineCount}");
            await output.WriteLineAsync($"Δομικά σφάλματα: {report.StructuralErrorCount}");
            await output.WriteLineAsync($"Ανεξάρτητοι έλεγχοι που εκκρεμούν: {report.PendingExpertReviewCount}");
            if (report.ConditionalEvidence.Count > 0)
            {
                await output.WriteLineAsync(
                    $"Υπό όρους πεδία σε τεχνική καταγραφή: {report.ConditionalEvidence.Count}");
                await output.WriteLineAsync(
                    $"Με μη-null παρουσία: {report.ConditionalEvidence.Count(item => item.ObservedNonNull > 0)}");
                await output.WriteLineAsync(
                    $"Με null παρουσία: {report.ConditionalEvidence.Count(item => item.ObservedNull > 0)}");
                await output.WriteLineAsync(
                    "Η παρουσία/απουσία δεν επιβεβαιώνει αν το πεδίο απαιτείται σε πραγματική συναλλαγή.");
            }

            foreach (ImpactOfflineFinding finding in report.Findings)
            {
                await output.WriteLineAsync(
                    $"  [{finding.Severity}] {finding.Code} — {finding.JsonPath} (§{finding.SourceSection})");
            }

            if (reportPath is not null)
            {
                await output.WriteLineAsync($"Νέο JSON αναφοράς: {reportPath}");
            }

            await output.WriteLineAsync(
                "Δεν έχει επαληθευτεί πραγματική συμμόρφωση IMPACT/ΑΑΔΕ ή έγκριση αποστολής.");
            await output.WriteLineAsync(
                "Δεν πραγματοποιήθηκε HTTP request, SQL write ή έκδοση.");
            logger.LogInformation(
                "IMPACT offline payload preflight completed: {Profile}, {Errors} errors, {Pending} review gates",
                report.Profile, report.StructuralErrorCount, report.PendingExpertReviewCount);

            return report.HasDetectedStructuralErrors ? 3 : 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο offline έλεγχος IMPACT ακυρώθηκε.");
            return 130;
        }
        catch (JsonException)
        {
            await error.WriteLineAsync("Μη έγκυρη ή διφορούμενη δομή JSON.");
            logger.LogWarning("IMPACT payload JSON structure rejected");
            return 2;
        }
        catch (ArgumentOutOfRangeException)
        {
            await error.WriteLineAsync("Μη επιτρεπτός συνδυασμός profile/scenario.");
            return 2;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Αποτυχία ανάγνωσης JSON ή δημιουργίας νέας αναφοράς.");
            logger.LogWarning("IMPACT offline payload file access failed");
            return 1;
        }
    }

    private static async Task SaveReportNewAsync(
        ImpactOfflinePayloadReport report, string destinationPath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        bool created = false;
        try
        {
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                created = true;
                await JsonSerializer.SerializeAsync(
                    stream, report, ExportOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        catch
        {
            if (created)
            {
                // Καθαρίζουμε αποκλειστικά το καινούργιο, ελλιπώς αποθηκευμένο report.
                try { File.Delete(fullPath); }
                catch (IOException) { /* Διατηρείται η αρχική εξαίρεση. */ }
                catch (UnauthorizedAccessException) { /* Απαιτείται έλεγχος δικαιωμάτων. */ }
            }

            throw;
        }
    }

    private static bool TryProfile(string? text, out ImpactContractProfile profile)
    {
        profile = text switch
        {
            "base" => ImpactContractProfile.Base,
            "invoice" => ImpactContractProfile.InvoiceIapr,
            "b2g" => ImpactContractProfile.InvoiceB2G,
            _ => default
        };
        return profile != default;
    }

    private static bool TryScenario(string? text, out ImpactCounterpartyScenario scenario)
    {
        scenario = text switch
        {
            "unknown" => ImpactCounterpartyScenario.Unknown,
            "business" => ImpactCounterpartyScenario.Business,
            "retail" => ImpactCounterpartyScenario.Retail,
            _ => default
        };
        return text is "unknown" or "business" or "retail";
    }
}
