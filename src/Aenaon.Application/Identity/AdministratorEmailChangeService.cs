using System.Net.Mail;

namespace Aenaon.Application.Identity;

/// <summary>Ελέγχει την είσοδο για αλλαγή αποκλειστικά του αρχικού δοκιμαστικού email.</summary>
public sealed class AdministratorEmailChangeService(IAdministratorEmailChangeStore store)
{
    public async Task<AdministratorEmailChangeResult> ExecuteAsync(
        Guid tenantId, Guid userId, string? newEmail, string? password, bool apply,
        string? confirmedDatabase, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string email = newEmail?.Trim() ?? string.Empty;
        if (tenantId == Guid.Empty || userId == Guid.Empty || email.Length is < 3 or > 256 ||
            !MailAddress.TryCreate(email, out MailAddress? parsed) ||
            !string.Equals(email, parsed.Address, StringComparison.OrdinalIgnoreCase) ||
            email.Equals("your-email@example.com", StringComparison.OrdinalIgnoreCase) ||
            (apply && (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(confirmedDatabase))) ||
            (!apply && (password is not null || confirmedDatabase is not null)))
        {
            return new(AdministratorEmailChangeStatus.InvalidInput);
        }

        return await store.ExecuteAsync(tenantId, userId, email, password, apply, confirmedDatabase,
            cancellationToken);
    }
}
