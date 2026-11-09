using System.Reflection;
using Aenaon.Api.Commands;
using Aenaon.Api.Configuration;
using Aenaon.Api.HealthChecks;
using Aenaon.Api.Middleware;
using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Aenaon.Infrastructure.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace Aenaon.Api;

/// <summary>Εκκινεί το API και συνδέει τα κοινά στοιχεία υποδομής.</summary>
public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine("Aenaon ERP API\n  --check-database  Έλεγχος σύνδεσης SQL χωρίς εκκίνηση HTTP.\n  --configure-database --server INSTANCE [--database NAME] [--trust-server-certificate]\n                    Ρύθμιση SQL login στα User Secrets του Development.\n  --migrate-database --dry-run\n                    Προεπισκόπηση migrations χωρίς αλλαγές.\n  --migrate-database --apply --confirm-database NAME\n                    Εφαρμογή migrations σε υπάρχουσα βάση Development.\n  --initialize-business (--dry-run | --apply --confirm-database NAME)\n                    --tenant-name NAME --store-code CODE --store-name NAME\n                    Αρχική καταχώρηση επιχείρησης και καταστήματος στο Development.\n  --initialize-admin (--dry-run | --apply --confirm-database NAME)\n                    --tenant-id GUID --username NAME --email ADDRESS\n                    Αρχική δημιουργία πρώτου Owner αποκλειστικά στο Development.\n  --fix-admin-email (--dry-run | --apply --confirm-database NAME)\n                    --tenant-id GUID --user-id GUID --new-email ADDRESS\n                    Διόρθωση αρχικού δοκιμαστικού email μετά από επαλήθευση κωδικού.\n  --verify-credentials --username NAME --confirm-database NAME\n                    Διαδραστικός έλεγχος κωδικού χωρίς session (Development).\n  --check-tenant-access --username NAME --tenant-id GUID [--store-id GUID]\n                    --permission Read|Manage --confirm-database NAME\n                    Έλεγχος Tenant/Store δικαιωμάτων (Development, χωρίς session).\n  --help            Εμφάνιση βοήθειας.\nΧωρίς επιλογές εκκινεί το API.");
            return 0;
        }

        bool checkDatabase = args is ["--check-database"];
        bool configureDatabase = args.Length > 0 && args[0] == "--configure-database";
        bool migrateDatabase = args.Length > 0 && args[0] == "--migrate-database";
        bool initializeBusiness = args.Length > 0 && args[0] == "--initialize-business";
        bool initializeAdmin = args.Length > 0 && args[0] == "--initialize-admin";
        bool fixAdminEmail = args.Length > 0 && args[0] == "--fix-admin-email";
        bool verifyCredentials = args.Length > 0 && args[0] == "--verify-credentials";
        bool checkTenantAccess = args.Length > 0 && args[0] == "--check-tenant-access";
        if (args.Contains("--check-database") && !checkDatabase)
        {
            Console.Error.WriteLine("Χρησιμοποίησε την επιλογή --check-database χωρίς επιπλέον ορίσματα εφαρμογής.");
            return 2;
        }

        var builder = WebApplication.CreateBuilder(checkDatabase || configureDatabase || migrateDatabase || initializeBusiness || initializeAdmin || fixAdminEmail || verifyCredentials || checkTenantAccess ? [] : args);
        builder.Services.AddApiServices(builder.Configuration, builder.Environment);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        });

        await using var app = builder.Build();
        if (configureDatabase)
        {
            string? secretsId = typeof(Program).Assembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;
            if (!app.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(secretsId))
            {
                Console.Error.WriteLine("Η ρύθμιση User Secrets επιτρέπεται μόνο στο Development. Χρησιμοποίησε --launch-profile https.");
                return 2;
            }
            return await ConfigureDatabaseCommand.ExecuteAsync(args[1..], secretsId, new DotnetUserSecretsWriter(),
                Console.In, Console.Out, Console.Error, ConsolePasswordReader.Read);
        }
        if (checkDatabase || migrateDatabase || initializeBusiness || initializeAdmin || fixAdminEmail || verifyCredentials || checkTenantAccess)
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += handler;
            try
            {
                if (checkTenantAccess)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο έλεγχος Tenant Authorization από CLI επιτρέπεται μόνο στο Development.");
                        return 2;
                    }
                    await using var accessScope = app.Services.CreateAsyncScope();
                    return await TenantAccessCommand.ExecuteAsync(args[1..],
                        accessScope.ServiceProvider.GetRequiredService<ICredentialVerifier>(),
                        accessScope.ServiceProvider.GetRequiredService<TenantAuthorizationService>(),
                        app.Services.GetRequiredService<IDatabaseSchemaManager>(),
                        Console.Out, Console.Error, ConsolePasswordReader.Read,
                        !Console.IsInputRedirected, cancellation.Token);
                }
                if (verifyCredentials)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο έλεγχος διαπιστευτηρίων από CLI επιτρέπεται μόνο σε Development.");
                        return 2;
                    }
                    await using var verifyScope = app.Services.CreateAsyncScope();
                    return await VerifyCredentialsCommand.ExecuteAsync(args[1..],
                        verifyScope.ServiceProvider.GetRequiredService<ICredentialVerifier>(),
                        app.Services.GetRequiredService<IDatabaseSchemaManager>(),
                        Console.Out, Console.Error, ConsolePasswordReader.Read,
                        !Console.IsInputRedirected, cancellation.Token);
                }
                if (fixAdminEmail)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η αλλαγή του αρχικού email μέσω CLI επιτρέπεται μόνο σε Development.");
                        return 2;
                    }
                    await using var emailScope = app.Services.CreateAsyncScope();
                    return await ChangeAdministratorEmailCommand.ExecuteAsync(args[1..],
                        emailScope.ServiceProvider.GetRequiredService<AdministratorEmailChangeService>(),
                        Console.Out, Console.Error, ConsolePasswordReader.Read,
                        !Console.IsInputRedirected, cancellation.Token);
                }
                if (initializeAdmin)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η αρχικοποίηση Administrator από CLI επιτρέπεται μόνο στο Development.");
                        return 2;
                    }
                    await using var initialAdminScope = app.Services.CreateAsyncScope();
                    return await InitialAdministratorCommand.ExecuteAsync(args[1..],
                        initialAdminScope.ServiceProvider.GetRequiredService<InitialAdministratorService>(),
                        Console.Out, Console.Error, ConsolePasswordReader.Read,
                        !Console.IsInputRedirected, cancellation.Token);
                }
                if (initializeBusiness)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η αρχικοποίηση επιχείρησης από αυτό το CLI επιτρέπεται μόνο στο Development.");
                        return 2;
                    }
                    return await InitialBusinessCommand.ExecuteAsync(args[1..],
                        app.Services.GetRequiredService<InitialBusinessService>(), Console.Out, Console.Error, cancellation.Token);
                }
                if (migrateDatabase)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η διαχείριση migrations από αυτό το CLI επιτρέπεται μόνο στο Development.");
                        return 2;
                    }
                    return await DatabaseMigrationsCommand.ExecuteAsync(args[1..],
                        app.Services.GetRequiredService<IDatabaseSchemaManager>(), Console.Out, Console.Error, cancellation.Token);
                }
                return await DatabaseCheckCommand.ExecuteAsync(app.Services.GetRequiredService<IDatabaseConnectionProbe>(),
                    Console.Out, Console.Error, cancellation.Token);
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }
        app.UseExceptionHandler();
        app.UseMiddleware<RequestLoggingMiddleware>();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health/database", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(DatabaseHealthCheck.Tag)
            });
        }
        app.Logger.LogInformation("Starting {ApplicationName}", "Aenaon ERP API");
        await app.RunAsync();
        return 0;
    }
}
