using System.Net.Mail;

namespace Aenaon.Application.Identity;

/// <summary>Επικυρώνει την αρχικοποίηση του πρώτου διαχειριστή πριν από οποιαδήποτε πρόσβαση στη βάση.</summary>
public sealed class InitialAdministratorService(IInitialAdministratorStore store)
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public async Task<InitialAdministratorResult> ExecuteAsync(Guid tenantId, string? userName, string? email,
        string? password, bool apply, string? confirmedDatabase, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedUserName = userName?.Trim() ?? string.Empty;
        string normalizedEmail = email?.Trim() ?? string.Empty;

        if (tenantId == Guid.Empty ||
            normalizedUserName.Length is < 3 or > 128 ||
            normalizedUserName.Any(character => !AllowedUserNameCharacters.Contains(character)) ||
            normalizedEmail.Length is < 3 or > 256 ||
            !MailAddress.TryCreate(normalizedEmail, out MailAddress? address) ||
            !string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
            (apply && (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(confirmedDatabase))) ||
            (!apply && (password is not null || confirmedDatabase is not null)))
        {
            return new(InitialAdministratorStatus.ValidationFailed, ValidationMessage:
                "Έλεγξε TenantId, username (3–128 χαρακτήρες), email και τις επιλογές εφαρμογής.");
        }

        return await store.ExecuteAsync(tenantId, normalizedUserName, normalizedEmail, password,
            apply, confirmedDatabase, cancellationToken);
    }
}
