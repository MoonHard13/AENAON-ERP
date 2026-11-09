using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Aenaon.Application.Tests;

[TestClass]
public sealed class InitialBusinessTests
{
    [TestMethod]
    public async Task Preview_NormalizesInputWithoutRequestingAWrite()
    {
        var store = new CaptureBusinessStore();
        var service = new InitialBusinessService(store);

        var result = await service.ExecuteAsync("  Demo Επιχείρηση  ", " main ", "  Κεντρικό  ", false, null);

        Assert.AreEqual(InitialBusinessStatus.Ready, result.Status);
        Assert.AreEqual("Demo Επιχείρηση", result.Summary!.TenantName);
        Assert.AreEqual("MAIN", result.Summary.StoreCode);
        Assert.AreEqual("Κεντρικό", result.Summary.StoreName);
        Assert.IsFalse(store.Apply);
        Assert.AreEqual(store.Tenant!.Id, store.Tenant.Stores[0].TenantId);
    }

    [TestMethod]
    public async Task Apply_PassesDatabaseConfirmationAndPreservesPersistenceOutcome()
    {
        var store = new CaptureBusinessStore { Status = InitialBusinessStatus.ExistingData };

        var result = await new InitialBusinessService(store).ExecuteAsync("Demo", "MAIN", "Κεντρικό", true, "AenaonERP_Unit");

        Assert.AreEqual(InitialBusinessStatus.ExistingData, result.Status);
        Assert.IsNull(result.Summary);
        Assert.IsTrue(store.Apply);
        Assert.AreEqual("AenaonERP_Unit", store.Confirmation);
    }

    [TestMethod]
    [DataRow(null, "MAIN", "Κεντρικό")]
    [DataRow("Demo", "INVALID CODE", "Κεντρικό")]
    [DataRow("Demo", "MAIN", "\nΚεντρικό")]
    public async Task InvalidInput_DoesNotReachPersistence(string? name, string code, string storeName)
    {
        var store = new CaptureBusinessStore();

        var result = await new InitialBusinessService(store).ExecuteAsync(name, code, storeName, true, "AenaonERP_Unit");

        Assert.AreEqual(InitialBusinessStatus.ValidationFailed, result.Status);
        Assert.IsNotEmpty(result.ValidationMessage!);
        Assert.IsNull(store.Tenant);
    }

    [TestMethod]
    public async Task Cancellation_PropagatesWithoutReachingPersistence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new CaptureBusinessStore();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new InitialBusinessService(store).ExecuteAsync("Demo", "MAIN", "Κεντρικό", true, "AenaonERP_Unit", cancellation.Token));

        Assert.IsNull(store.Tenant);
    }
}

internal sealed class CaptureBusinessStore : IInitialBusinessStore
{
    public InitialBusinessStatus Status { get; init; } = InitialBusinessStatus.Ready;
    public Tenant? Tenant { get; private set; }
    public bool Apply { get; private set; }
    public string? Confirmation { get; private set; }

    public Task<InitialBusinessResult> ExecuteAsync(Tenant tenant, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        Tenant = tenant;
        Apply = apply;
        Confirmation = confirmedDatabase;
        return Task.FromResult(new InitialBusinessResult(Status));
    }
}
