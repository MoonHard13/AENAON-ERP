using Aenaon.Infrastructure.Database;

namespace Aenaon.Api.Commands;

public static class DatabaseCheckCommand
{
    public static async Task<int> ExecuteAsync(IDatabaseConnectionProbe probe, TextWriter output,
        TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var result = await probe.CheckAsync(cancellationToken);
            if (result.Status == DatabaseConnectionStatus.Connected)
            {
                await output.WriteLineAsync($"Η σύνδεση SQL Server λειτουργεί. Compatibility level: {result.CompatibilityLevel}.");
                return 0;
            }

            string message = result.Status switch
            {
                DatabaseConnectionStatus.NotConfigured => "Δεν έχει οριστεί το ConnectionStrings:Aenaon στα User Secrets ή στο περιβάλλον.",
                DatabaseConnectionStatus.InvalidConfiguration => "Μη έγκυρες ρυθμίσεις σύνδεσης. Έλεγξε βάση, διαπιστευτήρια και ρυθμίσεις TLS.",
                DatabaseConnectionStatus.TimedOut => "Έληξε ο χρόνος σύνδεσης. Έλεγξε το SQL instance και το δίκτυο.",
                _ => "Αποτυχία σύνδεσης SQL Server. Έλεγξε την υπηρεσία, την πρόσβαση στη βάση και τα διαπιστευτήρια."
            };
            await error.WriteLineAsync(message);
            if (result.SqlErrorNumber is int number)
            {
                await error.WriteLineAsync($"SQL error number: {number}.");
            }
            return result.Status is DatabaseConnectionStatus.NotConfigured or DatabaseConnectionStatus.InvalidConfiguration ? 2 : 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο έλεγχος σύνδεσης ακυρώθηκε.");
            return 130;
        }
    }
}
