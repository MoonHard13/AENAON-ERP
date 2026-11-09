using Aenaon.Infrastructure.Database;

namespace Aenaon.Api.Commands;

public static class ConfigureDatabaseCommand
{
    public static async Task<int> ExecuteAsync(string[] args, string secretsId, IUserSecretsWriter writer,
        TextReader input, TextWriter output, TextWriter error, Func<string?> readPassword,
        CancellationToken cancellationToken = default)
    {
        string? server = null;
        string database = "AenaonERP_Dev";
        bool trustServerCertificate = false;
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--server" when index + 1 < args.Length:
                    server = args[++index];
                    break;
                case "--database" when index + 1 < args.Length:
                    database = args[++index];
                    break;
                case "--trust-server-certificate":
                    trustServerCertificate = true;
                    break;
                default:
                    await error.WriteLineAsync("Μη έγκυρες επιλογές. Χρησιμοποίησε --help για βοήθεια.");
                    return 2;
            }
        }

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        {
            await error.WriteLineAsync("Απαιτείται --server και έγκυρο όνομα βάσης.");
            return 2;
        }

        await output.WriteAsync("SQL login που χρησιμοποιείς στο SSMS: ");
        string? login = await input.ReadLineAsync(cancellationToken);
        await output.WriteAsync("Κωδικός SQL login: ");
        string? password = readPassword();
        await output.WriteLineAsync();
        if (password is null)
        {
            await error.WriteLineAsync("Η ρύθμιση ακυρώθηκε.");
            return 130;
        }

        string connectionString;
        try
        {
            connectionString = SqlConnectionConfiguration.ForDevelopment(server, database, login ?? "", password,
                trustServerCertificate);
        }
        catch (ArgumentException)
        {
            await error.WriteLineAsync("Έλεγξε server, ξεχωριστή βάση εφαρμογής, SQL login και κωδικό.");
            return 2;
        }

        if (!await writer.SaveAsync(secretsId, connectionString, cancellationToken))
        {
            await error.WriteLineAsync("Δεν αποθηκεύτηκαν τα User Secrets. Έλεγξε το .NET SDK και τα δικαιώματα του τοπικού λογαριασμού.");
            return 2;
        }

        await output.WriteLineAsync("Αποθηκεύτηκαν οι ρυθμίσεις ανάπτυξης. Εκτέλεσε τον έλεγχο --check-database.");
        return 0;
    }
}
