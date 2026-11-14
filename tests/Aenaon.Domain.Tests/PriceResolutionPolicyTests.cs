using Aenaon.Domain.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

/// <summary>Ανεξάρτητοι έλεγχοι προτεραιότητας εμπορικών τιμοκαταλόγων.</summary>
[TestClass]
public sealed class PriceResolutionPolicyTests
{
    private static readonly Guid Store = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly DateOnly Date = new(2026, 11, 14);

    [TestMethod]
    public void StoreAndChannel_OverrideEveryLowerScope()
    {
        PriceResolutionCandidate globalAll = Candidate(null, PriceListChannel.All, 10m);
        PriceResolutionCandidate globalExact = Candidate(null, PriceListChannel.Delivery, 11m);
        PriceResolutionCandidate storeAll = Candidate(Store, PriceListChannel.All, 12m);
        PriceResolutionCandidate storeExact = Candidate(Store, PriceListChannel.Delivery, 13m);

        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [globalAll, storeAll, globalExact, storeExact], Store,
            PriceListChannel.Delivery, Date);

        Assert.AreEqual(PriceSelectionStatus.Unique, result.Status);
        Assert.AreEqual(storeExact.PriceListId, result.Selected?.PriceListId);
        Assert.AreEqual(13m, result.Selected?.UnitPrice);
    }

    [TestMethod]
    public void StoreFallback_PrecedesGlobalExactChannel()
    {
        PriceResolutionCandidate globalExact = Candidate(null, PriceListChannel.Takeaway, 2m);
        PriceResolutionCandidate storeAll = Candidate(Store, PriceListChannel.All, 3m);

        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [globalExact, storeAll], Store, PriceListChannel.Takeaway, Date);

        Assert.AreEqual(storeAll.PriceListId, result.Selected?.PriceListId);
    }

    [TestMethod]
    public void WithoutStore_NeverUsesScopedPrices()
    {
        PriceResolutionCandidate scoped = Candidate(Store, PriceListChannel.All, 8m);
        PriceResolutionCandidate global = Candidate(null, PriceListChannel.All, 4m);
        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [scoped, global], null, PriceListChannel.Retail, Date);
        Assert.AreEqual(global.PriceListId, result.Selected?.PriceListId);
    }

    [TestMethod]
    public void WithoutMatchingChannel_ReturnsMissing()
    {
        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [Candidate(null, PriceListChannel.Online, 1m)],
            null, PriceListChannel.DineIn, Date);
        Assert.AreEqual(PriceSelectionStatus.Missing, result.Status);
    }

    [TestMethod]
    public void EqualTopRank_ReturnsAmbiguousInsteadOfArbitraryPrice()
    {
        PriceResolutionCandidate first = Candidate(Store, PriceListChannel.DineIn, 7m);
        PriceResolutionCandidate second = Candidate(Store, PriceListChannel.DineIn, 9m);
        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [second, first], Store, PriceListChannel.DineIn, Date);

        Assert.AreEqual(PriceSelectionStatus.Ambiguous, result.Status);
        Assert.IsNull(result.Selected);
        CollectionAssert.AreEquivalent(
            new[] { first.PriceListId, second.PriceListId },
            result.ConflictingPriceListIds!.ToArray());
    }

    [TestMethod]
    public void Period_IsInclusiveAndInactiveOrInvalidOptionsAreExcluded()
    {
        PriceResolutionCandidate valid = Candidate(null, PriceListChannel.All, 0m)
            with
            {
                ValidFrom = Date,
                ValidUntil = Date
            };
        PriceResolutionCandidate expired = Candidate(Store, PriceListChannel.DineIn, 6m)
            with
            {
                ValidUntil = Date.AddDays(-1)
            };
        PriceResolutionCandidate inactive = Candidate(Store, PriceListChannel.DineIn, 4m)
            with
            {
                IsActive = false
            };
        PriceResolutionCandidate invalidPrice = Candidate(Store, PriceListChannel.DineIn, -2m);
        PriceResolutionCandidate foreignCurrency = Candidate(Store, PriceListChannel.DineIn, 3m)
            with
            {
                Currency = "USD"
            };

        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [expired, invalidPrice, inactive, foreignCurrency, valid],
            Store, PriceListChannel.DineIn, Date);
        Assert.AreEqual(PriceSelectionStatus.Unique, result.Status);
        Assert.AreEqual(0m, result.Selected?.UnitPrice);

        PriceSelectionResult otherDate = PriceResolutionPolicy.Select(
            [valid], Store, PriceListChannel.DineIn, Date.AddDays(1));
        Assert.AreEqual(PriceSelectionStatus.Missing, otherDate.Status);
    }

    [TestMethod]
    public void GeneralChannel_OnlyMatchesGeneralCandidates()
    {
        PriceResolutionCandidate exact = Candidate(null, PriceListChannel.All, 10m);
        PriceResolutionCandidate other = Candidate(null, PriceListChannel.Delivery, 20m);
        PriceSelectionResult result = PriceResolutionPolicy.Select(
            [exact, other], null, PriceListChannel.All, Date);
        Assert.AreEqual(exact.PriceListId, result.Selected?.PriceListId);
    }

    private static PriceResolutionCandidate Candidate(
        Guid? storeId, PriceListChannel channel, decimal amount) =>
        new(Guid.NewGuid(), "TEST", storeId, channel, true, "EUR",
            Date.AddDays(-1), null, true, amount);
}
