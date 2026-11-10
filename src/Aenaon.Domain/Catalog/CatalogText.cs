using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

internal static class CatalogText
{
    public static string RequiredCode(string? value, int maxLength, string label, string errorCode)
    {
        string raw = value ?? string.Empty;
        string code = raw.Trim().ToUpperInvariant();

        if (code.Length == 0 || code.Length > maxLength || raw.Any(char.IsControl) ||
            code.Any(character => !char.IsLetterOrDigit(character) &&
                character is not '-' and not '_'))
        {
            throw new DomainRuleException(errorCode,
                $"{label}: απαιτούνται 1–{maxLength} γράμματα, αριθμοί, παύλα ή κάτω παύλα.");
        }

        return code;
    }

    public static string RequiredName(string? value, int maxLength, string label, string errorCode)
    {
        string raw = value ?? string.Empty;
        string name = raw.Trim();

        if (name.Length == 0 || name.Length > maxLength || raw.Any(char.IsControl))
        {
            throw new DomainRuleException(errorCode,
                $"{label}: απαιτούνται 1–{maxLength} χαρακτήρες χωρίς χαρακτήρες ελέγχου.");
        }

        return name;
    }
}
