using Aenaon.Domain.Tenancy;

namespace Aenaon.Application.Tenancy;

public interface IInitialBusinessStore
{
    Task<InitialBusinessResult> ExecuteAsync(Tenant tenant, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default);
}

public enum InitialBusinessStatus
{
    Ready,
    Created,
    ExistingData,
    ValidationFailed,
    NotConfigured,
    InvalidConfiguration,
    SchemaNotReady,
    Busy,
    Unavailable,
    TimedOut
}

public sealed record InitialBusinessSummary(string TenantName, string StoreCode, string StoreName);

public sealed record InitialBusinessResult(InitialBusinessStatus Status, Guid? TenantId = null, Guid? StoreId = null,
    string? DatabaseName = null, InitialBusinessSummary? Summary = null, string? ValidationMessage = null,
    int? SqlErrorNumber = null);
