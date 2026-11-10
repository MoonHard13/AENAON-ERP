using System.Diagnostics;
using System.Threading.RateLimiting;
using Aenaon.Api.Authentication;
using Aenaon.Api.HealthChecks;
using Aenaon.Api.Ui;
using Aenaon.Application.Fiscal;
using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Fiscal;
using Aenaon.Infrastructure.Identity;
using Aenaon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Aenaon.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Καταχωρίζει τις υπηρεσίες εκκίνησης του API.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddControllers();
        services.AddRazorComponents();
        services.AddSingleton<UiAssetVersions>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<FiscalImportInspectionService>();
        services.AddScoped<IFiscalStagingAuthorizationPolicy, DenyAllFiscalStagingAuthorizationPolicy>();
        services.AddScoped<FiscalStagingService>();
        services.AddSingleton<WorkspacePreferenceService>();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();
        services.AddSingleton<IDatabaseConnectionProbe>(provider => new SqlServerConnectionProbe(
            configuration.GetConnectionString("Aenaon"), environment.IsDevelopment(),
            provider.GetRequiredService<ILogger<SqlServerConnectionProbe>>()));
        services.AddSingleton<IDatabaseSchemaManager>(provider => new DatabaseSchemaManager(
            configuration.GetConnectionString("Aenaon"), environment.IsDevelopment(),
            provider.GetRequiredService<ILogger<DatabaseSchemaManager>>()));
        services.AddSingleton<IInitialBusinessStore>(provider => new SqlInitialBusinessStore(
            configuration.GetConnectionString("Aenaon"), environment.IsDevelopment(),
            provider.GetRequiredService<IDatabaseSchemaManager>(), provider.GetRequiredService<ILogger<SqlInitialBusinessStore>>()));
        services.AddSingleton<InitialBusinessService>();
        services.AddScoped(_ => new AenaonDbContext(AenaonDbContextOptions.Create(
            configuration.GetConnectionString("Aenaon") ?? string.Empty, environment.IsDevelopment())));
        services.AddIdentityCore<AenaonUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Password.RequiredLength = 14;
            options.Password.RequiredUniqueChars = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<AenaonDbContext>();
        services.AddScoped<IInitialAdministratorStore>(provider => new SqlInitialAdministratorStore(
            configuration.GetConnectionString("Aenaon"), environment.IsDevelopment(),
            provider.GetRequiredService<IDatabaseSchemaManager>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<SqlInitialAdministratorStore>>()));
        services.AddScoped<InitialAdministratorService>();
        services.AddScoped<ICredentialVerifier, IdentityCredentialVerifier>();
        services.AddScoped<IAdministratorEmailChangeStore>(provider => new SqlAdministratorEmailChangeStore(
            configuration.GetConnectionString("Aenaon"), environment.IsDevelopment(),
            provider.GetRequiredService<IDatabaseSchemaManager>(),
            provider.GetRequiredService<AenaonDbContext>(),
            provider.GetRequiredService<UserManager<AenaonUser>>(),
            provider.GetRequiredService<ICredentialVerifier>(),
            provider.GetRequiredService<ILogger<SqlAdministratorEmailChangeStore>>()));
        services.AddScoped<AdministratorEmailChangeService>();
        services.AddScoped<ITenantAccessStore, SqlTenantAccessStore>();
        services.AddScoped<TenantAuthorizationService>();
        services.AddScoped<ITenantStoreCatalog, SqlTenantStoreCatalog>();
        services.AddScoped<TenantWorkspaceService>();
        services.AddScoped<TenantWorkspaceResolver>();
        services.AddScoped<ISessionIdentityService, IdentitySessionService>();
        services.AddScoped<SessionCookieEvents>();
        services.AddAuthentication(SessionAuthenticationOptions.Scheme)
            .AddCookie(SessionAuthenticationOptions.Scheme, options =>
            {
                options.Cookie.Name = "__Host-AenaonERP.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = false;
                options.EventsType = typeof(SessionCookieEvents);
            });
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = SessionAuthenticationOptions.CsrfHeader;
            options.Cookie.Name = "__Host-AenaonERP.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
            {
                // Εμφανίζουμε φιλικό σφάλμα μόνο στην HTML φόρμα, διατηρώντας 429 στα API.
                var response = context.HttpContext.Response;
                if (context.HttpContext.Request.Path.StartsWithSegments(
                    "/ui/session/login", StringComparison.OrdinalIgnoreCase))
                {
                    response.Redirect("/login?error=rate-limited");
                }
                else
                {
                    response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    {
                        response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds))
                            .ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                return ValueTask.CompletedTask;
            };
            options.AddPolicy(SessionAuthenticationOptions.RateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true
                    }));
        });
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sqlserver",
            tags: [DatabaseHealthCheck.Tag], timeout: TimeSpan.FromSeconds(10));
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
            };
        });
        return services;
    }
}
