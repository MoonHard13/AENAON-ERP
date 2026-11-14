namespace Aenaon.Domain.Catalog;

/// <summary>Μία εμπορική υποψήφια τιμή, χωρίς υπολογισμό ΦΠΑ ή έκδοση παραστατικού.</summary>
public sealed record PriceResolutionCandidate(
    Guid PriceListId, string PriceListCode, Guid? StoreId,
    PriceListChannel Channel, bool PricesIncludeVat, string Currency,
    DateOnly ValidFrom, DateOnly? ValidUntil, bool IsActive, decimal UnitPrice);

public enum PriceSelectionStatus { Missing, Unique, Ambiguous }

public sealed record PriceSelectionResult(
    PriceSelectionStatus Status, PriceResolutionCandidate? Selected = null,
    IReadOnlyList<Guid>? ConflictingPriceListIds = null);

/// <summary>Επιλέγει ντετερμινιστικά τιμή και απορρίπτει την αμφίσημη προτεραιότητα.</summary>
public static class PriceResolutionPolicy
{
    /// <summary>
    /// Προτεραιότητα: κατάστημα+κανάλι, κατάστημα+όλα, επιχείρηση+κανάλι,
    /// επιχείρηση+όλα. Σε ισοβαθμία δεν επιλέγεται αυθαίρετα τιμοκατάλογος.
    /// </summary>
    public static PriceSelectionResult Select(
        IEnumerable<PriceResolutionCandidate> candidates,
        Guid? storeId, PriceListChannel channel, DateOnly businessDate)
    {
        var top = new List<PriceResolutionCandidate>();
        int highest = 0;

        foreach (PriceResolutionCandidate candidate in candidates)
        {
            int score = Score(candidate, storeId, channel, businessDate);
            if (score == 0 || score < highest)
            {
                continue;
            }

            if (score > highest)
            {
                highest = score;
                top.Clear();
            }

            top.Add(candidate);
        }

        if (top.Count == 0)
        {
            return new(PriceSelectionStatus.Missing);
        }

        if (top.Count > 1)
        {
            return new(PriceSelectionStatus.Ambiguous,
                ConflictingPriceListIds: top.Select(x => x.PriceListId)
                    .OrderBy(x => x).ToArray());
        }

        return new(PriceSelectionStatus.Unique, Selected: top[0]);
    }

    private static int Score(PriceResolutionCandidate candidate, Guid? storeId,
        PriceListChannel channel, DateOnly businessDate)
    {
        if (!candidate.IsActive || candidate.ValidFrom > businessDate ||
            candidate.ValidUntil is { } until && until < businessDate ||
            !string.Equals(candidate.Currency, PriceList.SupportedCurrency,
                StringComparison.Ordinal) ||
            candidate.UnitPrice < 0 || candidate.UnitPrice > PriceListItem.MaxUnitPrice ||
            decimal.Round(candidate.UnitPrice, 4) != candidate.UnitPrice)
        {
            return 0;
        }

        bool exactStore = storeId.HasValue && candidate.StoreId == storeId;
        if (candidate.StoreId.HasValue && !exactStore)
        {
            return 0;
        }

        bool exactChannel = candidate.Channel == channel;
        if (!exactChannel && candidate.Channel != PriceListChannel.All)
        {
            return 0;
        }

        if (exactStore)
        {
            return exactChannel ? 4 : 3;
        }

        return exactChannel ? 2 : 1;
    }
}
