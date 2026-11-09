using Aenaon.Application.Identity;

namespace Aenaon.Api.Commands;

/// <summary>Εκθέτει ασφαλή αρχικοποίηση πρώτου διαχειριστή αποκλειστικά μέσω Development CLI.</summary>
public static class InitialAdministratorCommand
{
    public static async Task<int> ExecuteAsync(string[] args, InitialAdministratorService service,
        TextWriter output, TextWriter error, Func<string?> readPassword, bool interactive,
        CancellationToken cancellationToken)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var values = new HashSet<string>(StringComparer.Ordinal)
        {
            "--tenant-id", "--username", "--email", "--confirm-database"
        };
        string? mode = null;
        for (int index = 0; index < args.Length; index++)
        {
            if ((args[index] is "--dry-run" or "--apply") && mode is null)
            {
                mode = args[index];
            }
            else if (values.Contains(args[index]) && index + 1 < args.Length &&
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
        options.TryGetValue("--confirm-database", out string? confirmation);
        if (mode is null ||
            !options.TryGetValue("--tenant-id", out string? tenantValue) ||
            !Guid.TryParse(tenantValue, out Guid tenantId) || tenantId == Guid.Empty ||
            !options.TryGetValue("--username", out string? userName) ||
            !options.TryGetValue("--email", out string? email) ||
            (apply && string.IsNullOrWhiteSpace(confirmation)) ||
            (!apply && confirmation is not null))
        {
            return await InvalidArgumentsAsync(error);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? password = null;
            if (apply)
            {
                if (!interactive)
                {
                    await error.WriteLineAsync("Η δημιουργία διαχειριστή απαιτεί διαδραστικό τερματικό, χωρίς ανακατεύθυνση stdin.");
                    return 2;
                }

                await output.WriteAsync("Νέος κωδικός Administrator: ");
                password = readPassword();
                await output.WriteLineAsync();
                if (password is null)
                {
                    await error.WriteLineAsync("Η καταχώρηση ακυρώθηκε.");
                    return 130;
                }

                await output.WriteAsync("Επιβεβαίωση κωδικού: ");
                string? confirmationPassword = readPassword();
                await output.WriteLineAsync();
                if (confirmationPassword is null)
                {
                    await error.WriteLineAsync("Η καταχώρηση ακυρώθηκε.");
                    return 130;
                }
                if (!string.Equals(password, confirmationPassword, StringComparison.Ordinal))
                {
                    await error.WriteLineAsync("Οι κωδικοί δεν ταιριάζουν. Δεν έγιναν αλλαγές.");
                    return 2;
                }
            }

            var result = await service.ExecuteAsync(tenantId, userName, email, password, apply, confirmation,
                cancellationToken);
            if (result.Status is InitialAdministratorStatus.Ready or InitialAdministratorStatus.Created)
            {
                await output.WriteLineAsync($"Βάση: {result.DatabaseName}");
                await output.WriteLineAsync($"Επιχείρηση: {result.TenantName}");
                await output.WriteLineAsync($"TenantId: {tenantId}");
                if (result.Status == InitialAdministratorStatus.Created)
                {
                    await output.WriteLineAsync($"UserId: {result.UserId}");
                    await output.WriteLineAsync("Ο πρώτος Administrator δημιουργήθηκε με ρόλο Owner.");
                }
                else
                {
                    await output.WriteLineAsync("Η προεπισκόπηση ολοκληρώθηκε χωρίς καταχώρηση χρήστη.");
                }
                return 0;
            }

            string message = result.Status switch
            {
                InitialAdministratorStatus.ExistingUsers => "Υπάρχουν ήδη χρήστες ή memberships. Η αρχική δημιουργία δεν επαναλαμβάνεται.",
                InitialAdministratorStatus.TenantNotFound => "Δεν βρέθηκε η επιχείρηση με το συγκεκριμένο TenantId.",
                InitialAdministratorStatus.ValidationFailed => result.ValidationMessage ??
                    "Το username ή email απορρίφθηκε από την επικύρωση Identity.",
                InitialAdministratorStatus.PasswordRejected => "Ο κωδικός δεν καλύπτει την πολιτική ασφαλείας (14+ χαρακτήρες, κεφαλαίο, πεζό, αριθμός, σύμβολο, 6 διαφορετικοί χαρακτήρες).",
                InitialAdministratorStatus.NotConfigured => "Δεν έχει οριστεί το ConnectionStrings:Aenaon.",
                InitialAdministratorStatus.InvalidConfiguration => "Έλεγξε Development, ρυθμίσεις SQL και το ακριβές όνομα στο --confirm-database.",
                InitialAdministratorStatus.SchemaNotReady => "Έλεγξε τις migrations με --migrate-database --dry-run πριν συνεχίσεις.",
                InitialAdministratorStatus.Busy => "Άλλη αρχικοποίηση βρίσκεται σε εξέλιξη. Έλεγξε την κατάσταση πριν επαναλάβεις.",
                InitialAdministratorStatus.TimedOut => "Έληξε ο χρόνος. Έλεγξε χρήστες και memberships πριν επαναλάβεις.",
                _ => "Αποτυχία SQL. Έλεγξε σύνδεση και δικαιώματα χωρίς να επαναφέρεις το σχήμα."
            };
            await error.WriteLineAsync(message);
            if (result.SqlErrorNumber is int number)
            {
                await error.WriteLineAsync($"SQL error number: {number}.");
            }
            return result.Status is InitialAdministratorStatus.Busy or
                InitialAdministratorStatus.TimedOut or InitialAdministratorStatus.Unavailable ? 1 : 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η λειτουργία ακυρώθηκε. Έλεγξε τα δεδομένα πριν επαναλάβεις.");
            return 130;
        }
    }

    private static async Task<int> InvalidArgumentsAsync(TextWriter error)
    {
        await error.WriteLineAsync(
            "Χρησιμοποίησε --initialize-admin (--dry-run | --apply --confirm-database NAME) --tenant-id GUID --username NAME --email ADDRESS.");
        return 2;
    }
}
