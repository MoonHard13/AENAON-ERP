using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Catalog;

/// <summary>Κοινοί κανόνες για προαιρετικό barcode και αναλυτική περιγραφή προϊόντος ή υπηρεσίας.</summary>
public static class CatalogItemDetails
{
    public const int MaxBarcodeLength = 40;
    public const int MaxLongDescriptionLength = 2000;

    /// <summary>Διατηρεί ανεξάρτητο εσωτερικό barcode ή επαληθεύει το ψηφίο ελέγχου GTIN.</summary>
    public static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        string normalized = barcode.Trim().ToUpperInvariant();
        if (normalized.Length > MaxBarcodeLength ||
            normalized.Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.')))
        {
            throw new DomainRuleException("catalog.item.barcode.invalid",
                "Το barcode δέχεται έως 40 λατινικούς χαρακτήρες, αριθμούς, παύλα, κάτω παύλα και τελεία.");
        }

        // Ακριβώς 8/12/13/14 αριθμητικά ψηφία αντιμετωπίζονται ως GTIN και ελέγχονται.
        bool numeric = normalized.All(c => c is >= '0' and <= '9');
        if (numeric && normalized.Length is 8 or 12 or 13 or 14 &&
            !HasValidGtinCheckDigit(normalized))
        {
            throw new DomainRuleException("catalog.item.barcode.check_digit",
                "Το ψηφίο ελέγχου του αριθμητικού GTIN/EAN δεν είναι έγκυρο.");
        }

        return normalized;
    }

    /// <summary>Επιτρέπει παραγράφους απλού κειμένου, χωρίς ανεξέλεγκτους χαρακτήρες ελέγχου.</summary>
    public static string? NormalizeLongDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        string normalized = description.Trim();
        if (normalized.Length > MaxLongDescriptionLength ||
            normalized.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
        {
            throw new DomainRuleException("catalog.item.description.invalid",
                "Η αναλυτική περιγραφή πρέπει να είναι απλό κείμενο έως 2000 χαρακτήρων.");
        }

        return normalized;
    }

    private static bool HasValidGtinCheckDigit(string value)
    {
        int sum = 0;
        int multiplier = 3;
        for (int i = value.Length - 2; i >= 0; i--)
        {
            sum += (value[i] - '0') * multiplier;
            multiplier = multiplier == 3 ? 1 : 3;
        }

        return (10 - sum % 10) % 10 == value[^1] - '0';
    }
}
