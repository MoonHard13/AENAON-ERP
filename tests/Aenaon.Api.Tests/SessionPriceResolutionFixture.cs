using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;

namespace Aenaon.Api.Tests;

/// <summary>Απομονωμένα εμπορικά δεδομένα για HTTP προεπισκόπηση χωρίς SQL writes.</summary>
internal sealed class SessionPriceResolutionFixture : IPriceResolutionStore
{
    public static readonly Guid ProductId =
        Guid.Parse("519f1811-6215-4841-81d2-62e921b22e3d");
    public static readonly Guid TenantId =
        Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    public bool ItemActive { get; set; } = true;
    public int ItemChecks { get; private set; }
    public int CandidateReads { get; private set; }
    public Guid? LastTenantId { get; private set; }

    public List<PriceResolutionCandidate> Candidates { get; } =
    [
        new(Guid.Parse("41111111-1111-4111-8111-111111111111"),
            "RETAIL", null, PriceListChannel.Retail, true, "EUR",
            new DateOnly(2026, 11, 1), null, true, 10.5000m)
    ];

    public Task<bool> IsActiveCatalogItemAsync(Guid tenantId,
        Guid itemId, CancellationToken token)
    {
        ItemChecks++;
        LastTenantId = tenantId;
        return Task.FromResult(ItemActive && tenantId == TenantId && itemId == ProductId);
    }

    public Task<IReadOnlyList<PriceResolutionCandidate>> FindCandidatesAsync(
        Guid tenantId, Guid itemId, Guid? storeId, PriceListChannel channel,
        DateOnly businessDate, CancellationToken token)
    {
        CandidateReads++;
        LastTenantId = tenantId;
        IReadOnlyList<PriceResolutionCandidate> result =
            tenantId == TenantId && itemId == ProductId ? Candidates : [];
        return Task.FromResult(result);
    }
}
