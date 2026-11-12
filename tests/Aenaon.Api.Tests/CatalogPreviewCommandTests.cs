using Aenaon.Api.Commands;
using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Επαληθεύει εξουσιοδοτημένη CLI προεπισκόπηση χωρίς SQL write ή μυστικά στην έξοδο.</summary>
[TestClass]
public sealed class CatalogPreviewCommandTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task AuthorizedMember_CanPreviewItemsWithoutPasswordDisclosure()
    {
        var store = new CatalogPreviewStore();
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await CatalogPreviewCommand.ExecuteAsync(
            ["--username", "admin", "--tenant-id", Tenant.ToString(),
                "--confirm-database", "AenaonERP_Unit", "--entity", "items",
                "--page-size", "1", "--search", "SERV"],
            new TenantCredentialFixture(true),
            CreateService(store, TenantMembershipRole.Member),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(0, exit);
        Assert.Contains("SERV01", output.ToString());
        Assert.Contains("Επόμενη σελίδα: Όχι", output.ToString());
        Assert.DoesNotContain("SampleSecret!91", output.ToString());
        Assert.AreEqual(1, store.ItemCalls);
        Assert.AreEqual(Tenant, store.LastTenant);
    }

    [TestMethod]
    public async Task DeniedTenant_HasNoCatalogResults()
    {
        var store = new CatalogPreviewStore();
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await CatalogPreviewCommand.ExecuteAsync(
            ["--username", "admin", "--tenant-id", Tenant.ToString(),
                "--confirm-database", "AenaonERP_Unit", "--entity", "categories"],
            new TenantCredentialFixture(true),
            CreateService(store, null),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(3, exit);
        Assert.AreEqual(0, store.CategoryCalls);
        Assert.DoesNotContain("CATALOG PREVIEW", output.ToString());
    }

    [TestMethod]
    [DataRow("unknown-entity")]
    [DataRow("huge-page")]
    [DataRow("empty-tenant")]
    [DataRow("noninteractive")]
    [DataRow("wrong-db")]
    public async Task InvalidUsage_FailsBeforePasswordOrCatalogRead(string scenario)
    {
        string[] args = ["--username", "admin", "--tenant-id", Tenant.ToString(),
            "--confirm-database", "AenaonERP_Unit", "--entity", "items"];
        args = scenario switch
        {
            "unknown-entity" => [.. args[..^1], "orders"],
            "huge-page" => [.. args, "--page-size", "101"],
            "empty-tenant" => [.. args[..3], Guid.Empty.ToString(), .. args[4..]],
            "wrong-db" => [.. args[..5], "ForeignDatabase", .. args[6..]],
            _ => args
        };
        var store = new CatalogPreviewStore();
        var verifier = new TenantCredentialFixture(true);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await CatalogPreviewCommand.ExecuteAsync(
            args, verifier, CreateService(store, TenantMembershipRole.Owner),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => throw new InvalidOperationException("No credential prompt expected"),
            scenario != "noninteractive");

        Assert.AreEqual(2, exit);
        Assert.AreEqual(0, verifier.Calls);
        Assert.AreEqual(0, store.ItemCalls);
    }

    private static CatalogReadService CreateService(CatalogPreviewStore store,
        TenantMembershipRole? role) =>
        new(new TenantAuthorizationService(new TenantCommandStore { Role = role }), store);

    private sealed class CatalogPreviewStore : ICatalogReadStore
    {
        public int ItemCalls { get; private set; }
        public int CategoryCalls { get; private set; }
        public Guid LastTenant { get; private set; }

        public Task<CatalogReadPage<CatalogCategorySummary>> ReadCategoriesAsync(
            Guid tenantId, CatalogReadQuery query, CancellationToken cancellationToken = default)
        {
            CategoryCalls++;
            LastTenant = tenantId;
            return Task.FromResult(new CatalogReadPage<CatalogCategorySummary>(
                [], query.Page, query.PageSize, false));
        }

        public Task<CatalogReadPage<CatalogItemSummary>> ReadItemsAsync(
            Guid tenantId, CatalogReadQuery query, CancellationToken cancellationToken = default)
        {
            ItemCalls++;
            LastTenant = tenantId;
            return Task.FromResult(new CatalogReadPage<CatalogItemSummary>(
                [new(Guid.NewGuid(), Guid.NewGuid(), "SERV01", "Catering",
                    CatalogItemKind.Service, false, true)],
                query.Page, query.PageSize, false));
        }
    }
}
