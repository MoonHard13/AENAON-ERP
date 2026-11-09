using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Identity;

/// <summary>Ανακτά τα στοιχεία cookie μόνο αφού το Identity επαληθεύσει τον κωδικό και τη συμμετοχή.</summary>
public sealed class IdentitySessionService(
    ICredentialVerifier credentialVerifier,
    UserManager<AenaonUser> userManager,
    ITenantAccessStore tenantAccessStore,
    ILogger<IdentitySessionService> logger) : ISessionIdentityService
{
    public async Task<SessionIdentity?> AuthenticateAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        var result = await credentialVerifier.VerifyAsync(userName, password, cancellationToken);
        if (result.Status != CredentialVerificationStatus.Authenticated || result.UserId is not Guid userId)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        AenaonUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.UserName) ||
            string.IsNullOrWhiteSpace(user.SecurityStamp) ||
            await userManager.IsLockedOutAsync(user) ||
            (await tenantAccessStore.ListUserTenantsAsync(userId, cancellationToken)).Count == 0)
        {
            logger.LogWarning("Session issue refused for {UserId}", userId);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Session identity verified for {UserId}", userId);
        return new SessionIdentity(userId, user.UserName, user.SecurityStamp);
    }

    public async Task<bool> ValidateSessionAsync(Guid userId, string? securityStamp,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrEmpty(securityStamp))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        AenaonUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null ||
            !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal) ||
            await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return (await tenantAccessStore.ListUserTenantsAsync(userId, cancellationToken)).Count > 0;
    }
}
