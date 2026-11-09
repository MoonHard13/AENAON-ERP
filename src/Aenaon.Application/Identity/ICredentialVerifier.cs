namespace Aenaon.Application.Identity;

/// <summary>Αποτέλεσμα ελέγχου κωδικού χωρίς δημιουργία session ή δικαιωμάτων επιχείρησης.</summary>
public enum CredentialVerificationStatus
{
    Rejected,
    Authenticated
}

public sealed record CredentialVerificationResult(CredentialVerificationStatus Status, Guid? UserId = null);

public interface ICredentialVerifier
{
    Task<CredentialVerificationResult> VerifyAsync(string? userName, string? password,
        CancellationToken cancellationToken = default);
}
