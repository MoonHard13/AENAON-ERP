namespace Aenaon.Application.Identity;

/// <summary>Καταστάσεις ελεγχόμενης διόρθωσης του αρχικού δοκιμαστικού email.</summary>
public enum AdministratorEmailChangeStatus
{
    Ready,
    Updated,
    InvalidInput,
    NotConfigured,
    InvalidConfiguration,
    SchemaNotReady,
    TargetNotEligible,
    InvalidCredentials,
    Unavailable,
    TimedOut
}

public sealed record AdministratorEmailChangeResult(
    AdministratorEmailChangeStatus Status,
    string? DatabaseName = null,
    Guid? UserId = null,
    int? SqlErrorNumber = null);

public interface IAdministratorEmailChangeStore
{
    Task<AdministratorEmailChangeResult> ExecuteAsync(Guid tenantId, Guid userId, string email,
        string? password, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default);
}
