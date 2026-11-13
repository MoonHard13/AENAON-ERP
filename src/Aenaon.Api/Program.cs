using System.Reflection;
using Aenaon.Api.Authentication;
using Aenaon.Api.Commands;
using Aenaon.Api.Configuration;
using Aenaon.Api.HealthChecks;
using Aenaon.Api.Middleware;
using Aenaon.Api.Ui;
using Aenaon.Application.Catalog;
using Aenaon.Application.Fiscal;
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
            Console.WriteLine("Aenaon ERP API\n  --check-database  Έλεγχος σύνδεσης SQL χωρίς εκκίνηση HTTP.\n  --configure-database --server INSTANCE [--database NAME] [--trust-server-certificate]\n                    Ρύθμιση SQL login στα User Secrets του Development.\n  --migrate-database --dry-run\n                    Προεπισκόπηση migrations χωρίς αλλαγές.\n  --migrate-database --apply --confirm-database NAME\n                    Εφαρμογή migrations σε υπάρχουσα βάση Development.\n  --initialize-business (--dry-run | --apply --confirm-database NAME)\n                    --tenant-name NAME --store-code CODE --store-name NAME\n                    Αρχική καταχώρηση επιχείρησης και καταστήματος στο Development.\n  --initialize-admin (--dry-run | --apply --confirm-database NAME)\n                    --tenant-id GUID --username NAME --email ADDRESS\n                    Αρχική δημιουργία πρώτου Owner αποκλειστικά στο Development.\n  --fix-admin-email (--dry-run | --apply --confirm-database NAME)\n                    --tenant-id GUID --user-id GUID --new-email ADDRESS\n                    Διόρθωση αρχικού δοκιμαστικού email μετά από επαλήθευση κωδικού.\n  --verify-credentials --username NAME --confirm-database NAME\n                    Διαδραστικός έλεγχος κωδικού χωρίς session (Development).\n  --check-tenant-access --username NAME --tenant-id GUID [--store-id GUID]\n                    --permission Read|Manage --confirm-database NAME\n                    Έλεγχος Tenant/Store δικαιωμάτων (Development, χωρίς session).\n  --manage-catalog --username NAME --tenant-id GUID --confirm-database NAME\n                    --view categories|products|services --action create|update|activate|deactivate\n                    [--id GUID] [--code CODE | --auto-code] [--name NAME] [--category-id GUID]\n                    [--parent-category-id GUID | --root] [--tracks-stock]\n                    --dry-run | --apply\n                    Ασφαλής διαχείριση καταλόγου (Owner/Manage, Development).\n  --manage-price-lists --username NAME --tenant-id GUID --confirm-database NAME\n                    --action list|items|create|update|activate|deactivate|prices [--id GUID]\n                    [--code CODE] [--name NAME] [--valid-from yyyy-MM-dd] [--price ITEM_GUID=VALUE]\n                    (--dry-run | --apply). Εμπορικοί τιμοκατάλογοι χωρίς φορολογική διαβίβαση.\n  --preview-catalog --username NAME --tenant-id GUID --confirm-database NAME\n                    --entity categories|items [--search PREFIX] [--page N]\n                    [--page-size 1..100] [--include-inactive]\n                    Εξουσιοδοτημένη, μόνο-ανάγνωσης προεπισκόπηση καταλόγου.\n  --inspect-fiscal-import --manifest FILE.json --evidence-file SOURCE_FILE\n                    Τοπικός έλεγχος υποψήφιων κωδικών και SHA-256, χωρίς SQL.\n  --inspect-fiscal-matrix --manifest FILE.json --evidence-file SOURCE.xlsx\n                    Επιθεώρηση υποψήφιων συνδυασμών myDATA χωρίς SQL ή φορολογική έγκριση.\n  --inspect-fiscal-xlsx --evidence-file SOURCE.xlsx [--sheet NAME] [--all] [--export-json NEW_FILE.json]\n                    Πλήρης δομική εξαγωγή XLSX/JSON και SHA-256 χωρίς SQL ή έγκριση.\n  --verify-fiscal-xlsx-export --evidence-file SOURCE.xlsx --evidence-json COMPLETE.json\n                    Ανεξάρτητος έλεγχος συμφωνίας όλων των κελιών και παρατηρήσεων.\n  --triage-fiscal-semantics --evidence-file SOURCE.xlsx --evidence-json COMPLETE.json\n                    [--report-json NEW_REVIEW.json | --worklist-csv NEW_WORKLIST.csv]\n                    Μη εγκεκριμένες παρατηρήσεις ή ασφαλής λίστα Excel για φοροτεχνικό.\n  --inspect-impact-contract --profile base|invoice|b2g [--report-json NEW_FILE.json]\n                    Read-only checklist δημοσιευμένων πεδίων IMPACT, χωρίς API calls.\n  --inspect-impact-payload --profile base|invoice|b2g --scenario unknown|business|retail\n                    --payload-file SYNTHETIC.json [--report-json NEW_REPORT.json]\n                    Offline δομικός έλεγχος, χωρίς φορολογική έγκριση ή διαβίβαση.\n  --help            Εμφάνιση βοήθειας.\nΧωρίς επιλογές εκκινεί το API.");
            return 0;
        }

        bool inspectImpactPayload = args.Length > 0 && args[0] == "--inspect-impact-payload";
        bool inspectImpactContract = args.Length > 0 && args[0] == "--inspect-impact-contract";
        bool triageFiscalSemantics = args.Length > 0 && args[0] == "--triage-fiscal-semantics";
        bool verifyFiscalXlsxExport = args.Length > 0 && args[0] == "--verify-fiscal-xlsx-export";
        bool inspectFiscalXlsx = args.Length > 0 && args[0] == "--inspect-fiscal-xlsx";
        bool inspectFiscalMatrix = args.Length > 0 && args[0] == "--inspect-fiscal-matrix";
        bool inspectFiscalImport = args.Length > 0 && args[0] == "--inspect-fiscal-import";
        bool checkDatabase = args is ["--check-database"];
        bool configureDatabase = args.Length > 0 && args[0] == "--configure-database";
        bool migrateDatabase = args.Length > 0 && args[0] == "--migrate-database";
        bool initializeBusiness = args.Length > 0 && args[0] == "--initialize-business";
        bool initializeAdmin = args.Length > 0 && args[0] == "--initialize-admin";
        bool fixAdminEmail = args.Length > 0 && args[0] == "--fix-admin-email";
        bool verifyCredentials = args.Length > 0 && args[0] == "--verify-credentials";
        bool checkTenantAccess = args.Length > 0 && args[0] == "--check-tenant-access";
        bool previewCatalog = args.Length > 0 && args[0] == "--preview-catalog";
        bool manageCatalog = args.Length > 0 && args[0] == "--manage-catalog";
        bool managePriceLists = args.Length > 0 && args[0] == "--manage-price-lists";
        if (args.Contains("--check-database") && !checkDatabase)
        {
            Console.Error.WriteLine("Χρησιμοποίησε την επιλογή --check-database χωρίς επιπλέον ορίσματα εφαρμογής.");
            return 2;
        }

        var builder = WebApplication.CreateBuilder(managePriceLists || manageCatalog || previewCatalog || inspectImpactPayload || inspectImpactContract || triageFiscalSemantics || verifyFiscalXlsxExport || inspectFiscalXlsx || inspectFiscalMatrix || inspectFiscalImport || checkDatabase || configureDatabase || migrateDatabase || initializeBusiness || initializeAdmin || fixAdminEmail || verifyCredentials || checkTenantAccess ? [] : args);
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
        if (managePriceLists || manageCatalog || previewCatalog || inspectImpactPayload || inspectImpactContract || triageFiscalSemantics || verifyFiscalXlsxExport || inspectFiscalXlsx || inspectFiscalMatrix || inspectFiscalImport || checkDatabase || migrateDatabase || initializeBusiness || initializeAdmin || fixAdminEmail || verifyCredentials || checkTenantAccess)
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
                if (manageCatalog)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η διαχείριση καταλόγου επιτρέπεται μόνο στο Development.");
                        return 2;
                    }

                    await using var catalogScope = app.Services.CreateAsyncScope();
                    return await CatalogManageCommand.ExecuteAsync(args[1..],
                        catalogScope.ServiceProvider.GetRequiredService<ICredentialVerifier>(),
                        catalogScope.ServiceProvider.GetRequiredService<CatalogWriteService>(),
                        app.Services.GetRequiredService<IDatabaseSchemaManager>(),
                        app.Logger, Console.Out, Console.Error, ConsolePasswordReader.Read,
                        !Console.IsInputRedirected, cancellation.Token);
                }

                if (previewCatalog)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η προεπισκόπηση καταλόγου επιτρέπεται μόνο στο Development.");
                        return 2;
                    }

                    await using var catalogScope = app.Services.CreateAsyncScope();
                    return await CatalogPreviewCommand.ExecuteAsync(args[1..],
                        catalogScope.ServiceProvider.GetRequiredService<ICredentialVerifier>(),
                        catalogScope.ServiceProvider.GetRequiredService<CatalogReadService>(),
                        app.Services.GetRequiredService<IDatabaseSchemaManager>(),
                        app.Logger, Console.Out, Console.Error, ConsolePasswordReader.Read,
                        !Console.IsInputRedirected, cancellation.Token);
                }

                if (inspectImpactPayload)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο offline έλεγχος IMPACT επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    return await InspectImpactPayloadCommand.ExecuteAsync(args[1..],
                        new ImpactOfflinePayloadInspector(),
                        app.Logger, Console.Out, Console.Error, cancellation.Token);
                }

                if (inspectImpactContract)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η προεπισκόπηση συμβολαίου IMPACT επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    return await InspectImpactContractCommand.ExecuteAsync(args[1..],
                        new ImpactContractPreflightCatalog(),
                        app.Logger, Console.Out, Console.Error, cancellation.Token);
                }

                if (triageFiscalSemantics)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο φάκελος φορολογικών παρατηρήσεων επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    var inspector = new FiscalXlsxEvidenceInspector();
                    var analyzer = new FiscalXlsxClassificationEvidenceAnalyzer();
                    return await TriageFiscalSemanticsCommand.ExecuteAsync(args[1..],
                        new FiscalXlsxEvidenceReconciliationService(
                            inspector, new FiscalXlsxEvidenceExportService(analyzer)),
                        inspector, new FiscalXlsxSemanticReviewService(analyzer),
                        new FiscalSemanticReviewDossierWriter(),
                        app.Logger, Console.Out, Console.Error, cancellation.Token);
                }

                if (verifyFiscalXlsxExport)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο ανεξάρτητος έλεγχος XLSX επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    return await VerifyFiscalXlsxExportCommand.ExecuteAsync(args[1..],
                        new FiscalXlsxEvidenceReconciliationService(
                            new FiscalXlsxEvidenceInspector(),
                            new FiscalXlsxEvidenceExportService(
                                new FiscalXlsxClassificationEvidenceAnalyzer())),
                        app.Logger, Console.Out, Console.Error, cancellation.Token);
                }

                if (inspectFiscalXlsx)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο έλεγχος αρχείων XLSX επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    return await InspectFiscalXlsxCommand.ExecuteAsync(args[1..],
                        new FiscalXlsxEvidenceInspector(), app.Logger,
                        Console.Out, Console.Error, cancellation.Token);
                }

                if (inspectFiscalMatrix)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Η προεπισκόπηση συνδυασμών επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    return await InspectFiscalClassificationMatrixCommand.ExecuteAsync(args[1..],
                        new FiscalClassificationMatrixInspectionService(TimeProvider.System),
                        app.Logger, Console.Out, Console.Error, cancellation.Token);
                }

                if (inspectFiscalImport)
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        Console.Error.WriteLine("Ο έλεγχος φορολογικών τεκμηρίων επιτρέπεται μόνο σε Development.");
                        return 2;
                    }

                    return await InspectFiscalImportCommand.ExecuteAsync(args[1..],
                        new FiscalImportInspectionService(TimeProvider.System), app.Logger,
                        Console.Out, Console.Error, cancellation.Token);
                }

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
        if (app.Environment.IsDevelopment())
        {
            app.UseStaticFiles();
        }
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.IsDevelopment())
        {
            app.UseMiddleware<AuthenticatedWelcomeRedirectMiddleware>();
        }
        app.UseAntiforgery();
        app.MapControllers();
        if (app.Environment.IsDevelopment())
        {
            app.MapSessionEndpoints();
            app.MapUiSessionEndpoints();
            app.MapWorkspaceSelectionEndpoints();
            app.MapCatalogManagementEndpoints();
            app.MapRazorComponents<App>();
        }
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
