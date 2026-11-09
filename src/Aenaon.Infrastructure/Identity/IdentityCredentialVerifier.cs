using Aenaon.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Identity;

/// <summary>Ελέγχει κωδικούς μέσω Identity και τηρεί αποτυχίες/κλείδωμα χωρίς έκδοση cookie.</summary>
public sealed class IdentityCredentialVerifier(
    UserManager<AenaonUser> userManager,
    ILogger<IdentityCredentialVerifier> logger) : ICredentialVerifier
{
    private static readonly AenaonUser DummyUser = new() { UserName = "nonexistent" };
    private static readonly PasswordHasher<AenaonUser> DummyHasher = new();
    private static readonly string DummyHash =
        DummyHasher.HashPassword(DummyUser, "not-a-real-account-password");

    public async Task<CredentialVerificationResult> VerifyAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 128 ||
            string.IsNullOrEmpty(password) || password.Length > 1024)
        {
            return new(CredentialVerificationStatus.Rejected);
        }

        AenaonUser? user = await userManager.FindByNameAsync(userName.Trim());
        cancellationToken.ThrowIfCancellationRequested();

        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            // Ισορροπία κόστους hashing σε ανύπαρκτο και κλειδωμένο λογαριασμό.
            DummyHasher.VerifyHashedPassword(DummyUser, DummyHash, password);
            logger.LogInformation("Credential verification rejected");
            return new(CredentialVerificationStatus.Rejected);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            IdentityResult failed = await userManager.AccessFailedAsync(user);
            if (!failed.Succeeded)
            {
                logger.LogWarning("Credential failure counter could not be updated");
                return new(CredentialVerificationStatus.Rejected);
            }

            logger.LogInformation("Credential verification rejected");
            return new(CredentialVerificationStatus.Rejected);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IdentityResult reset = await userManager.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded)
        {
            logger.LogWarning("Credential failure counter could not be reset");
            return new(CredentialVerificationStatus.Rejected);
        }

        logger.LogInformation("Credential verification succeeded for {UserId}", user.Id);
        return new(CredentialVerificationStatus.Authenticated, user.Id);
    }
}
