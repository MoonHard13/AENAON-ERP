using System.Diagnostics;
using Aenaon.Api.HealthChecks;
using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Aenaon.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Persistence;

namespace Aenaon.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Καταχωρίζει τις υπηρεσίες εκκίνησης του API.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddControllers();
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
