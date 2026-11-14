using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;

namespace Aenaon.Api.Tests;

/// <summary>Απομονωμένα τιμολογιακά δεδομένα για HTTP/CSRF tests χωρίς SQL.</summary>
internal sealed class SessionPriceListFixture : IPriceListManagementStore
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    public static readonly Guid ListId = Guid.Parse("41111111-1111-4111-8111-111111111111");
    public static readonly Guid ItemId = Guid.Parse("42222222-2222-4222-8222-222222222222");
    public int SaveCalls { get; private set; }
    public PriceListChange? LastChange { get; private set; }
    public int PriceCalls { get; private set; }
    public int RemoveCalls { get; private set; }
    public IReadOnlyList<PriceListPriceChange>? LastPriceChanges { get; private set; }

    private static PriceListSummary Summary => new(
        ListId, "RETAIL", "Βασική λιανική", null,
        PriceListChannel.Retail, true, new DateOnly(2026, 11, 13),
        null, true, 1);

    public Task<PriceListPage> ListAsync(Guid tenantId, string? search,
        bool includeInactive, int page, int pageSize, CancellationToken token) =>
        Task.FromResult(new PriceListPage(tenantId == Tenant ? [Summary] : [], page, false));

    public Task<PriceListSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken token) =>
        Task.FromResult<PriceListSummary?>(tenantId == Tenant && id == ListId ? Summary : null);

    public Task<PriceListItemsPage?> ReadItemsAsync(Guid tenantId, Guid listId,
        string? search, int page, int pageSize, CancellationToken token) =>
        Task.FromResult<PriceListItemsPage?>(tenantId == Tenant && listId == ListId ?
            new([new PriceListItemRow(ItemId, "SKU001", "Δοκιμαστικό προϊόν",
                CatalogItemKind.Product, 10m)], page, false) : null);

    public Task<PriceListHistoryPage?> ReadHistoryAsync(Guid tenantId, Guid priceListId,
        int page, int pageSize, CancellationToken token) =>
        Task.FromResult<PriceListHistoryPage?>(new PriceListHistoryPage([], page, false));

    public Task<PriceListResult> ApplyAsync(Guid tenantId, PriceListChange command,
        CancellationToken token, Guid? actorUserId = null, string source = "System")
    {
        SaveCalls++;
        LastChange = command;
        return Task.FromResult(new PriceListResult(PriceListStatus.Saved,
            command.Id ?? ListId));
    }

    public Task<PriceListResult> SetPricesAsync(Guid tenantId, Guid listId,
        IReadOnlyList<PriceListPriceChange> changes, CancellationToken token, Guid? actorUserId = null, string source = "System")
    {
        PriceCalls++;
        LastPriceChanges = changes;
        return Task.FromResult(new PriceListResult(PriceListStatus.Saved, listId));
    }

    public Task<PriceListResult> RemovePriceAsync(Guid tenantId, Guid listId,
        Guid catalogItemId, decimal expectedUnitPrice, CancellationToken token, Guid? actorUserId = null, string source = "System")
    {
        RemoveCalls++;
        return Task.FromResult(new PriceListResult(PriceListStatus.Saved, listId));
    }
}
