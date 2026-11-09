namespace Aenaon.Infrastructure.Database;

public interface IDatabaseConnectionProbe
{
    Task<DatabaseConnectionResult> CheckAsync(CancellationToken cancellationToken = default);
}

public enum DatabaseConnectionStatus
{
    Connected,
    NotConfigured,
    InvalidConfiguration,
    Unavailable,
    TimedOut
}

/// <summary>Αποτέλεσμα διάγνωσης χωρίς στοιχεία σύνδεσης ή κείμενο εξαιρέσεων.</summary>
public sealed record DatabaseConnectionResult(DatabaseConnectionStatus Status,
    int? CompatibilityLevel = null, int? SqlErrorNumber = null);
