using Aenaon.Application.Identity;

namespace Aenaon.Api.Commands;

/// <summary>Διορθώνει μόνο το αρχικό email διαχειριστή μέσω ελεγχόμενης Development εντολής.</summary>
public static class ChangeAdministratorEmailCommand
{
    public static async Task<int> ExecuteAsync(string[] args, AdministratorEmailChangeService service,
        TextWriter output, TextWriter error, Func<string?> readPassword, bool interactive,
        CancellationToken cancellationToken)
    {
        string? mode = null;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            "--tenant-id", "--user-id", "--new-email", "--confirm-database"
        };

        for (int index = 0; index < args.Length; index++)
        {
            if ((args[index] is "--dry-run" or "--apply") && mode is null)
            {
                mode = args[index];
            }
            else if (accepted.Contains(args[index]) && index + 1 < args.Length &&
                options.TryAdd(args[index], args[index + 1]))
            {
                index++;
            }
            else
            {
                return await InvalidAsync(error);
            }
        }

        bool apply = mode == "--apply";
        options.TryGetValue("--confirm-database", out string? confirmedDatabase);
        if (mode is null ||
            !options.TryGetValue("--tenant-id", out string? tenantValue) ||
            !Guid.TryParse(tenantValue, out Guid tenantId) || tenantId == Guid.Empty ||
            !options.TryGetValue("--user-id", out string? userValue) ||
            !Guid.TryParse(userValue, out Guid userId) || userId == Guid.Empty ||
            !options.TryGetValue("--new-email", out string? newEmail) ||
            (apply && string.IsNullOrWhiteSpace(confirmedDatabase)) ||
            (!apply && confirmedDatabase is not null))
        {
            return await InvalidAsync(error);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? password = null;
            if (apply)
            {
                if (!interactive)
                {
                    await error.WriteLineAsync("Η αλλαγή email απαιτεί διαδραστικό terminal.");
                    return 2;
                }

                await output.WriteAsync("Τρέχων κωδικός Administrator: ");
                password = readPassword();
                await output.WriteLineAsync();
                if (password is null)
                {
                    await error.WriteLineAsync("Η αλλαγή email ακυρώθηκε.");
                    return 130;
                }
            }

            var result = await service.ExecuteAsync(tenantId, userId, newEmail, password, apply,
                confirmedDatabase, cancellationToken);
            if (result.Status is AdministratorEmailChangeStatus.Ready or AdministratorEmailChangeStatus.Updated)
            {
                await output.WriteLineAsync($"Βάση: {result.DatabaseName}");
                await output.WriteLineAsync($"UserId: {result.UserId}");
                await output.WriteLineAsync(result.Status == AdministratorEmailChangeStatus.Ready
                    ? "Η προεπισκόπηση ολοκληρώθηκε χωρίς αλλαγές."
                    : "Το δοκιμαστικό email ενημερώθηκε μέσω Identity. Η νέα διεύθυνση δεν έχει ακόμη επιβεβαιωθεί.");
                return 0;
            }

            string message = result.Status switch
            {
                AdministratorEmailChangeStatus.NotConfigured => "Δεν έχει ρυθμιστεί το ConnectionStrings:Aenaon.",
                AdministratorEmailChangeStatus.InvalidConfiguration => "Απαιτείται Development και ακριβής επιβεβαίωση βάσης.",
                AdministratorEmailChangeStatus.SchemaNotReady => "Έλεγξε τις migrations πριν συνεχίσεις.",
                AdministratorEmailChangeStatus.TargetNotEligible => "Δεν βρέθηκε ο μοναδικός Owner με το αρχικό δοκιμαστικό email. Δεν έγιναν αλλαγές.",
                AdministratorEmailChangeStatus.InvalidCredentials => "Η επαλήθευση κωδικού απέτυχε ή ο λογαριασμός είναι προσωρινά κλειδωμένος.",
                AdministratorEmailChangeStatus.InvalidInput => "Η νέα διεύθυνση δεν είναι έγκυρη ή απορρίφθηκε από το Identity.",
                AdministratorEmailChangeStatus.TimedOut => "Η λειτουργία έληξε. Έλεγξε την υπάρχουσα τιμή email πριν επανάληψη.",
                _ => "Αποτυχία πρόσβασης SQL. Έλεγξε το περιβάλλον."
            };
            await error.WriteLineAsync(message);
            if (result.SqlErrorNumber is int number)
            {
                await error.WriteLineAsync($"SQL error number: {number}.");
            }

            return result.Status is AdministratorEmailChangeStatus.Unavailable or
                AdministratorEmailChangeStatus.TimedOut ? 1 : 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η ενέργεια ακυρώθηκε. Έλεγξε την κατάσταση πριν επανάληψη.");
            return 130;
        }
    }

    private static async Task<int> InvalidAsync(TextWriter error)
    {
        await error.WriteLineAsync("Χρήση: --fix-admin-email (--dry-run | --apply --confirm-database NAME) --tenant-id GUID --user-id GUID --new-email ADDRESS.");
        return 2;
    }
}
