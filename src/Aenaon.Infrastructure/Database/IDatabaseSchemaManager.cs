namespace Aenaon.Infrastructure.Database;

public interface IDatabaseSchemaManager
{
    Task<DatabaseSchemaResult> ExecuteAsync(bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default);
}

public enum DatabaseSchemaStatus
{
    Ready,
    Updated,
    NotConfigured,
    InvalidConfiguration,
    UnsafeDatabase,
    Unavailable,
    TimedOut
}

public sealed record DatabaseSchemaResult(DatabaseSchemaStatus Status, string? DatabaseName = null,
    IReadOnlyList<string>? AppliedMigrations = null, IReadOnlyList<string>? PendingMigrations = null,
    int? SqlErrorNumber = null);
