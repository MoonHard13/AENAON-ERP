using System.Diagnostics;
using Aenaon.Api.HealthChecks;
using Aenaon.Infrastructure.Database;

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
