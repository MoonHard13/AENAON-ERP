using Aenaon.Infrastructure.Database;

namespace Aenaon.Api.Commands;

public static class DatabaseMigrationsCommand
{
    public static async Task<int> ExecuteAsync(string[] args, IDatabaseSchemaManager manager, TextWriter output,
        TextWriter error, CancellationToken cancellationToken)
    {
        bool apply = false;
        string? confirmedDatabase = null;
        if (args is ["--dry-run"])
        {
            // Η προεπισκόπηση διαβάζει metadata χωρίς να εκτελεί migrations.
        }
        else if (args is ["--apply", "--confirm-database", var database] && !string.IsNullOrWhiteSpace(database))
        {
            apply = true;
            confirmedDatabase = database;
        }
        else
        {
            await error.WriteLineAsync("Χρησιμοποίησε --migrate-database --dry-run ή --migrate-database --apply --confirm-database ΟΝΟΜΑ_ΒΑΣΗΣ.");
            return 2;
        }

        try
        {
            var result = await manager.ExecuteAsync(apply, confirmedDatabase, cancellationToken);
            if (result.Status is DatabaseSchemaStatus.Ready or DatabaseSchemaStatus.Updated)
            {
                await output.WriteLineAsync($"Βάση: {result.DatabaseName}");
                await output.WriteLineAsync($"Εφαρμοσμένες migrations: {result.AppliedMigrations?.Count ?? 0}");
                await output.WriteLineAsync($"Εκκρεμείς migrations: {result.PendingMigrations?.Count ?? 0}");
                foreach (string migration in result.PendingMigrations ?? [])
                {
                    await output.WriteLineAsync($"  {migration}");
                }
                await output.WriteLineAsync(!apply ? "Η προεπισκόπηση ολοκληρώθηκε χωρίς αλλαγές στη βάση."
                    : result.Status == DatabaseSchemaStatus.Updated ? "Οι migrations εφαρμόστηκαν επιτυχώς."
                    : "Η βάση είναι ήδη ενημερωμένη. Δεν απαιτούνται αλλαγές.");
                return 0;
            }

            string message = result.Status switch
            {
                DatabaseSchemaStatus.NotConfigured => "Δεν έχει οριστεί το ConnectionStrings:Aenaon.",
                DatabaseSchemaStatus.InvalidConfiguration => "Έλεγξε Development, ρυθμίσεις SQL, ενημέρωση κώδικα και το ακριβές όνομα στο --confirm-database.",
                DatabaseSchemaStatus.UnsafeDatabase => "Δεν επιτρέπεται εφαρμογή: απαιτείται compatibility 160 και συμβατό ιστορικό. Η αρχική migration απαιτεί βάση χωρίς άλλα αντικείμενα χρήστη.",
                DatabaseSchemaStatus.TimedOut => "Έληξε ο χρόνος ελέγχου ή εφαρμογής. Έλεγξε την κατάσταση με --dry-run πριν επαναλάβεις.",
                _ => "Αποτυχία πρόσβασης ή εφαρμογής. Έλεγξε την υπηρεσία SQL και τα δικαιώματα στη βάση."
            };
            await error.WriteLineAsync(message);
            if (result.SqlErrorNumber is int number)
            {
                await error.WriteLineAsync($"SQL error number: {number}.");
            }
            return result.Status is DatabaseSchemaStatus.Unavailable or DatabaseSchemaStatus.TimedOut ? 1 : 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η λειτουργία ακυρώθηκε. Έλεγξε την κατάσταση με --dry-run πριν συνεχίσεις.");
            return 130;
        }
    }
}
