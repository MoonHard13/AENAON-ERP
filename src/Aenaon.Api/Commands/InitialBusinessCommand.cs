using Aenaon.Application.Tenancy;

namespace Aenaon.Api.Commands;

public static class InitialBusinessCommand
{
    public static async Task<int> ExecuteAsync(string[] args, InitialBusinessService service, TextWriter output,
        TextWriter error, CancellationToken cancellationToken)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        string? mode = null;
        var valueOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "--tenant-name", "--store-code", "--store-name", "--confirm-database"
        };
        for (int index = 0; index < args.Length; index++)
        {
            if ((args[index] is "--dry-run" or "--apply") && mode is null)
            {
                mode = args[index];
            }
            else if (valueOptions.Contains(args[index]) && index + 1 < args.Length &&
                options.TryAdd(args[index], args[index + 1]))
            {
                index++;
            }
            else
            {
                return await InvalidArgumentsAsync(error);
            }
        }
        bool apply = mode == "--apply";
        options.TryGetValue("--confirm-database", out string? confirmedDatabase);
        if (mode is null || !options.TryGetValue("--tenant-name", out string? tenantName) ||
            !options.TryGetValue("--store-code", out string? storeCode) ||
            !options.TryGetValue("--store-name", out string? storeName) ||
            (apply && string.IsNullOrWhiteSpace(confirmedDatabase)) || (!apply && confirmedDatabase is not null))
        {
            return await InvalidArgumentsAsync(error);
        }

        try
        {
            var result = await service.ExecuteAsync(tenantName, storeCode, storeName, apply, confirmedDatabase, cancellationToken);
            if (result.Status is InitialBusinessStatus.Ready or InitialBusinessStatus.Created)
            {
                await output.WriteLineAsync($"Βάση: {result.DatabaseName}");
                await output.WriteLineAsync($"Επιχείρηση: {result.Summary!.TenantName}");
                await output.WriteLineAsync($"Κατάστημα: {result.Summary.StoreCode} — {result.Summary.StoreName}");
                if (result.Status == InitialBusinessStatus.Created)
                {
                    await output.WriteLineAsync($"TenantId: {result.TenantId}");
                    await output.WriteLineAsync($"StoreId: {result.StoreId}");
                    await output.WriteLineAsync("Η επιχείρηση και το κεντρικό κατάστημα δημιουργήθηκαν επιτυχώς.");
                }
                else
                {
                    await output.WriteLineAsync("Η προεπισκόπηση ολοκληρώθηκε χωρίς καταχώρηση δεδομένων.");
                }
                return 0;
            }

            string message = result.Status switch
            {
                InitialBusinessStatus.ValidationFailed => result.ValidationMessage ?? "Μη έγκυρα στοιχεία επιχείρησης/καταστήματος.",
                InitialBusinessStatus.ExistingData => "Υπάρχουν ήδη επιχειρησιακά δεδομένα. Η αρχικοποίηση δεν επαναλαμβάνεται και δεν άλλαξε εγγραφές.",
                InitialBusinessStatus.NotConfigured => "Δεν έχει οριστεί το ConnectionStrings:Aenaon.",
                InitialBusinessStatus.InvalidConfiguration => "Έλεγξε Development, ρυθμίσεις SQL και το ακριβές όνομα στο --confirm-database.",
                InitialBusinessStatus.SchemaNotReady => "Έλεγξε τις migrations με --migrate-database --dry-run και ενημέρωσε πρώτα το σχήμα της βάσης.",
                InitialBusinessStatus.Busy => "Η αρχικοποίηση χρησιμοποιείται από άλλη διεργασία. Έλεγξε την κατάσταση πριν επαναλάβεις.",
                InitialBusinessStatus.TimedOut => "Έληξε ο χρόνος αρχικοποίησης. Έλεγξε τα δεδομένα πριν επαναλάβεις.",
                _ => "Αποτυχία πρόσβασης ή αποθήκευσης. Έλεγξε τη σύνδεση και τα δικαιώματα SQL."
            };
            await error.WriteLineAsync(message);
            if (result.SqlErrorNumber is int number)
            {
                await error.WriteLineAsync($"SQL error number: {number}.");
            }
            return result.Status is InitialBusinessStatus.Unavailable or InitialBusinessStatus.TimedOut or InitialBusinessStatus.Busy ? 1 : 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η αρχικοποίηση ακυρώθηκε. Έλεγξε τα δεδομένα πριν συνεχίσεις.");
            return 130;
        }
    }

    private static async Task<int> InvalidArgumentsAsync(TextWriter error)
    {
        await error.WriteLineAsync("Χρησιμοποίησε --initialize-business (--dry-run ή --apply --confirm-database NAME) --tenant-name NAME --store-code CODE --store-name NAME.");
        return 2;
    }
}
