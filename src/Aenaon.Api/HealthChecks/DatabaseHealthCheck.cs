using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aenaon.Api.HealthChecks;

public sealed class DatabaseHealthCheck(IDatabaseConnectionProbe probe) : IHealthCheck
{
    public const string Tag = "database";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await probe.CheckAsync(cancellationToken);
        return result.Status == DatabaseConnectionStatus.Connected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database connectivity unavailable.");
    }
}
