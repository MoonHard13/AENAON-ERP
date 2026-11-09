using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Tenancy;

internal static class OrganizationText
{
    public static string RequiredName(string? value, int maxLength, string label, string errorCode)
    {
        string raw = value ?? "";
        string trimmed = raw.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength || raw.Any(char.IsControl))
        {
            throw new DomainRuleException(errorCode,
                $"{label}: απαιτούνται 1–{maxLength} χαρακτήρες χωρίς αλλαγές γραμμής ή χαρακτήρες ελέγχου.");
        }
        return trimmed;
    }

    public static string StoreCode(string? value)
    {
        string raw = value ?? "";
        string code = raw.Trim().ToUpperInvariant();
        if (code.Length == 0 || code.Length > Store.MaxCodeLength || raw.Any(char.IsControl) ||
            code.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new DomainRuleException("store.code.invalid",
                $"Ο κωδικός καταστήματος δέχεται 1–{Store.MaxCodeLength} γράμματα, αριθμούς, παύλα ή κάτω παύλα.");
        }
        return code;
    }
}
