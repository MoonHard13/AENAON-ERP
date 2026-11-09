using System.Diagnostics;

namespace Aenaon.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    /// <summary>Καταχωρίζει τις υπηρεσίες εκκίνησης του API.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddHealthChecks();
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
