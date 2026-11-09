using System.Reflection;
using Aenaon.Api.Commands;
using Aenaon.Api.Configuration;
using Aenaon.Api.HealthChecks;
using Aenaon.Api.Middleware;
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
            Console.WriteLine("Aenaon ERP API\n  --check-database  Έλεγχος σύνδεσης SQL χωρίς εκκίνηση HTTP.\n  --configure-database --server INSTANCE [--database NAME] [--trust-server-certificate]\n                    Ρύθμιση SQL login στα User Secrets του Development.\n  --help            Εμφάνιση βοήθειας.\nΧωρίς επιλογές εκκινεί το API.");
            return 0;
        }

        bool checkDatabase = args is ["--check-database"];
        bool configureDatabase = args.Length > 0 && args[0] == "--configure-database";
        if (args.Contains("--check-database") && !checkDatabase)
        {
            Console.Error.WriteLine("Χρησιμοποίησε την επιλογή --check-database χωρίς επιπλέον ορίσματα εφαρμογής.");
            return 2;
        }

        var builder = WebApplication.CreateBuilder(checkDatabase || configureDatabase ? [] : args);
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
        if (checkDatabase)
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
