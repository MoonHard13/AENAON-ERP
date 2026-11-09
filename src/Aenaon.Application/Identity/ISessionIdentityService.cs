namespace Aenaon.Application.Identity;

/// <summary>Επιβεβαιωμένη ταυτότητα μόνο για server-issued session, χωρίς προεπιλεγμένα δικαιώματα Tenant.</summary>
public sealed record SessionIdentity(Guid UserId, string UserName, string SecurityStamp);

public interface ISessionIdentityService
{
    Task<SessionIdentity?> AuthenticateAsync(string? userName, string? password,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateSessionAsync(Guid userId, string? securityStamp,
        CancellationToken cancellationToken = default);
}
