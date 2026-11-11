using System.Text.Json;
using Aenaon.Application.Fiscal;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Επιθεώρηση δημοσιευμένων απαιτήσεων IMPACT χωρίς δεδομένα ή κλήσεις παρόχου.</summary>
public static class InspectImpactContractCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> ExecuteAsync(string[] args,
        ImpactContractPreflightCatalog catalog,
        ILogger logger,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        string? profileName = null;
        string? exportPath = null;
        bool invalid = false;
        for (int index = 0; index < args.Length && !invalid;)
        {
            string argument = args[index++];
            if (index == args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }

            string value = args[index++];
            switch (argument)
            {
                case "--profile" when profileName is null:
                    profileName = value;
                    break;
                case "--report-json" when exportPath is null:
                    exportPath = value;
                    break;
                default:
                    invalid = true;
                    break;
            }
        }

        if (invalid || !TryProfile(profileName, out ImpactContractProfile profile) ||
            exportPath is not null &&
            (string.IsNullOrWhiteSpace(exportPath) ||
                !string.Equals(Path.GetExtension(exportPath), ".json",
                    StringComparison.OrdinalIgnoreCase)))
        {
            await error.WriteLineAsync(
                "Χρήση: --inspect-impact-contract --profile base|invoice|b2g " +
                "[--report-json NEW_FILE.json]");
            return 2;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImpactContractPreflightReport report = catalog.Build(profile);
            if (exportPath is not null)
            {
                string path = Path.GetFullPath(exportPath);
                bool created = false;
                try
                {
                    await using (var stream = new FileStream(path, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    {
                        created = true;
                        await JsonSerializer.SerializeAsync(
                            stream, report, JsonOptions, cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                    }
                }
                catch
                {
                    if (created)
                    {
                        // Διαγράφουμε μόνο το νέο και ενδεχομένως ατελές JSON.
                        try { File.Delete(path); }
                        catch (IOException) { /* Διατηρούμε την αρχική εξαίρεση. */ }
                        catch (UnauthorizedAccessException) { /* Απαιτείται έλεγχος δικαιωμάτων. */ }
                    }
                    throw;
                }
            }

            await output.WriteLineAsync($"IMPACT Contract Preflight: {profile}");
            await output.WriteLineAsync(
                $"Τεκμηρίωση: στιγμιότυπο {report.DocumentationSnapshotDate}");
            await output.WriteLineAsync($"Απαιτούμενα πεδία: {report.RequiredCount}");
            await output.WriteLineAsync($"Υπό όρους πεδία: {report.ConditionalCount}");
            await output.WriteLineAsync(
                $"Έλεγχοι που απαιτούν ειδικό: {report.ManualReviewCount}");

            foreach (ImpactContractRequirement field in report.Requirements)
            {
                await output.WriteLineAsync(
                    $"  [{field.Kind}] {field.PublishedField} — §{field.SourceSection}");
            }

            foreach (ImpactContractReviewGate gate in report.ReviewGates)
            {
                await output.WriteLineAsync(
                    $"  [EXPERT REVIEW] {gate.Name}: {gate.SourceUrl}");
            }

            if (exportPath is not null)
            {
                await output.WriteLineAsync($"Νέο JSON απαιτήσεων: {exportPath}");
            }

            await output.WriteLineAsync(
                "ΜΟΝΟ CHECKLIST: κανένα πραγματικό payload δεν επικυρώθηκε.");
            await output.WriteLineAsync(
                "Δεν έγινε API call, SQL write, φορολογική έγκριση ή έκδοση.");
            logger.LogInformation("IMPACT contract preflight catalog generated: {Profile}, {Fields}, {Gates}",
                profile, report.Requirements.Count, report.ReviewGates.Count);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η προεπισκόπηση IMPACT ακυρώθηκε.");
            return 130;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync("Δεν ήταν δυνατή η ανάγνωση ή ασφαλής εξαγωγή αρχείου.");
            logger.LogWarning("IMPACT contract preflight output rejected");
            return 1;
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
}
