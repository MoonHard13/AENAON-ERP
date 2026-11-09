namespace Aenaon.Application.Identity;

public interface IInitialAdministratorStore
{
    Task<InitialAdministratorResult> ExecuteAsync(Guid tenantId, string userName, string email,
        string? password, bool apply, string? confirmedDatabase, CancellationToken cancellationToken = default);
}

public enum InitialAdministratorStatus
{
    Ready,
    Created,
    ExistingUsers,
    TenantNotFound,
    ValidationFailed,
    PasswordRejected,
    NotConfigured,
    InvalidConfiguration,
    SchemaNotReady,
    Busy,
    Unavailable,
    TimedOut
}

public sealed record InitialAdministratorResult(
    InitialAdministratorStatus Status,
    string? DatabaseName = null,
    string? TenantName = null,
    Guid? UserId = null,
    int? SqlErrorNumber = null,
    string? ValidationMessage = null);
