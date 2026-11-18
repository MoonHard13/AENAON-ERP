using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Menus;

/// <summary>Κοινή επικύρωση κειμένων της διαχείρισης πρόχειρων μενού.</summary>
internal static class MenuText
{
    public static string Required(string? value, int maximum, string code, string label,
        bool uppercase = false)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximum ||
            normalized.Any(char.IsControl))
        {
            throw new DomainRuleException(code, $"Το πεδίο {label} δεν είναι έγκυρο.");
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    public static string? Optional(string? value, int maximum, string code, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, maximum, code, label);
    }

    public static void Identity(Guid tenantId, Guid value, string label)
    {
        if (tenantId == Guid.Empty || value == Guid.Empty)
        {
            throw new DomainRuleException("menu.scope.invalid",
                $"Δεν έχει καθοριστεί έγκυρη επιχείρηση ή {label}.");
        }
    }

    public static void Position(int position)
    {
        if (position < 1 || position > 10000)
        {
            throw new DomainRuleException("menu.position.invalid",
                "Η θέση πρέπει να είναι από 1 έως 10000.");
        }
    }
}
