using Aenaon.Api.Commands;
using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Δοκιμάζει ρητό apply, dry-run και προστασία μυστικών της κονσόλας.</summary>
[TestClass]
public sealed class CatalogManageCommandTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task DryRun_DoesNotCallWriteStore()
    {
        var store = new FakeWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await CatalogManageCommand.ExecuteAsync(
            BaseArgs("--dry-run"), new TenantCredentialFixture(true),
            CreateService(store, TenantMembershipRole.Owner),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(0, code);
        Assert.Contains("DRY RUN", output.ToString());
        Assert.AreEqual(0, store.ApplyCalls);
        Assert.AreEqual(1, store.DetailCalls);
        Assert.DoesNotContain("SampleSecret!91", output.ToString());
    }

    [TestMethod]
    public async Task Apply_UsesSameOwnerProtectedService()
    {
        var store = new FakeWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await CatalogManageCommand.ExecuteAsync(
            BaseArgs("--apply"), new TenantCredentialFixture(true),
            CreateService(store, TenantMembershipRole.Owner),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(0, code);
        Assert.AreEqual(1, store.ApplyCalls);
        Assert.AreEqual(Tenant, store.LastTenant);
        Assert.DoesNotContain("SampleSecret!91", output.ToString());
        Assert.DoesNotContain("SampleSecret!91", error.ToString());
    }

    [TestMethod]
    public async Task Apply_CategoryParentFlag_ReachesSharedService()
    {
        Guid parent = Guid.Parse("933fe6a9-bd60-40ce-ac35-34fcf264e7d3");
        string[] args = [.. BaseArgs("--apply")[..^1],
            "--parent-category-id", parent.ToString("D"), "--apply"];
        var store = new FakeWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await CatalogManageCommand.ExecuteAsync(
            args, new TenantCredentialFixture(true),
            CreateService(store, TenantMembershipRole.Owner),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(0, code, error.ToString());
        Assert.AreEqual(1, store.ApplyCalls);
        Assert.AreEqual(parent, store.LastCommand?.ParentCategoryId);
    }

    [TestMethod]
    public async Task Apply_AutoCode_DoesNotRequireManualCode()
    {
        string[] args = [.. BaseArgs("--apply")[..^1]
            .Where(value => value is not "--code" and not "FOOD"),
            "--auto-code", "--apply"];
        var store = new FakeWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await CatalogManageCommand.ExecuteAsync(
            args, new TenantCredentialFixture(true),
            CreateService(store, TenantMembershipRole.Owner),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(0, code, error.ToString());
        Assert.IsTrue(store.LastCommand?.AutoCode == true);
        Assert.IsNull(store.LastCommand?.Code);
    }

    [TestMethod]
    public async Task Member_CannotUseApplyEvenWithVerifiedPassword()
    {
        var store = new FakeWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await CatalogManageCommand.ExecuteAsync(
            BaseArgs("--apply"), new TenantCredentialFixture(true),
            CreateService(store, TenantMembershipRole.Member),
            new TenantAccessSchemaFixture(), NullLogger.Instance,
            output, error, () => "SampleSecret!91", true);

        Assert.AreEqual(3, code);
        Assert.AreEqual(0, store.DetailCalls);
        Assert.AreEqual(0, store.ApplyCalls);
    }

    [TestMethod]
    [DataRow("no-apply")]
    [DataRow("both")]
    [DataRow("bad-view")]
    [DataRow("noninteractive")]
    [DataRow("duplicate-username")]
    public async Task InvalidCliFlags_DoNotVerifyCredentialsOrTouchDatabase(string scenario)
    {
        string[] args = scenario switch
        {
            "both" => [.. BaseArgs("--apply"), "--dry-run"],
            "bad-view" => [.. BaseArgs("--apply")[..8], "orders",
                .. BaseArgs("--apply")[9..]],
            "duplicate-username" => [.. BaseArgs("--apply"), "--username", "other"],
            "no-apply" => BaseArgs("--apply")[..^1],
            _ => BaseArgs("--apply")
        };
        var credentials = new TenantCredentialFixture(true);
        var store = new FakeWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await CatalogManageCommand.ExecuteAsync(
            args, credentials, CreateService(store, TenantMembershipRole.Owner),
            new TenantAccessSchemaFixture(), NullLogger.Instance, output, error,
            () => throw new InvalidOperationException(), scenario != "noninteractive");

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, credentials.Calls);
        Assert.AreEqual(0, store.ApplyCalls);
    }

    private static string[] BaseArgs(string flag) =>
    [
        "--username", "admin",
        "--tenant-id", Tenant.ToString("D"),
        "--confirm-database", "AenaonERP_Unit",
        "--view", "categories",
        "--action", "create",
        "--code", "FOOD",
        "--name", "Φαγητά",
        flag
    ];

    private static CatalogWriteService CreateService(
        FakeWriter store, TenantMembershipRole? role) =>
        new(new TenantAuthorizationService(new TenantCommandStore { Role = role }), store);

    private sealed class FakeWriter : ICatalogWriteStore
    {
        public int ApplyCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public Guid LastTenant { get; private set; }
        public CatalogChangeCommand? LastCommand { get; private set; }

        public Task<CatalogChangeResult> ApplyAsync(Guid tenantId, CatalogChangeCommand command,
            CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            LastTenant = tenantId;
            LastCommand = command;
            return Task.FromResult(new CatalogChangeResult(CatalogChangeStatus.Saved, Guid.NewGuid()));
        }

        public Task<IReadOnlyList<CatalogCategoryOption>> SearchCategoriesAsync(
            Guid tenantId, string? term, Guid? selectedId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogCategoryOption>>([]);

        public Task<CatalogManageDetails> GetDetailsAsync(Guid tenantId,
            CatalogManagedEntity entity, Guid? id, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Task.FromResult(new CatalogManageDetails(null, []));
        }
    }
}
