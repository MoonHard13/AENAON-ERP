using Aenaon.Application.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

[TestClass]
public sealed class AdministratorEmailChangeTests
{
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid UserId = Guid.Parse("76d2c37f-2e26-4311-a129-e6073057c8dd");

    [TestMethod]
    public async Task Preview_UsesReadOnlyModeWithoutPassword()
    {
        var store = new EmailChangeRecordingStore();
        var result = await new AdministratorEmailChangeService(store)
            .ExecuteAsync(TenantId, UserId, "  valid@example.invalid  ", null, false, null);

        Assert.AreEqual(AdministratorEmailChangeStatus.Ready, result.Status);
        Assert.AreEqual("valid@example.invalid", store.Email);
        Assert.IsNull(store.Password);
        Assert.IsFalse(store.Apply);
    }

    [TestMethod]
    [DataRow("missing-tenant")]
    [DataRow("missing-user")]
    [DataRow("invalid-email")]
    [DataRow("placeholder-email")]
    [DataRow("preview-password")]
    [DataRow("missing-password")]
    [DataRow("missing-confirmation")]
    public async Task InvalidInput_NeverCallsStore(string scenario)
    {
        var store = new EmailChangeRecordingStore();
        bool apply = scenario is not "preview-password";
        string? password = scenario == "missing-password" ? null : "StrongPassword!1234";
        string? database = scenario == "missing-confirmation" ? null : "AenaonERP_Unit";
        string email = scenario == "invalid-email" ? "bad-email" :
            scenario == "placeholder-email" ? "your-email@example.com" : "valid@example.invalid";
        var result = await new AdministratorEmailChangeService(store).ExecuteAsync(
            scenario == "missing-tenant" ? Guid.Empty : TenantId,
            scenario == "missing-user" ? Guid.Empty : UserId,
            email, password, apply, database);

        Assert.AreEqual(AdministratorEmailChangeStatus.InvalidInput, result.Status);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task Apply_CarriesDatabaseConfirmation()
    {
        var store = new EmailChangeRecordingStore { Status = AdministratorEmailChangeStatus.Updated };
        var result = await new AdministratorEmailChangeService(store)
            .ExecuteAsync(TenantId, UserId, "valid@example.invalid", "StrongPassword!1234",
                true, "AenaonERP_Unit");

        Assert.AreEqual(AdministratorEmailChangeStatus.Updated, result.Status);
        Assert.AreEqual("AenaonERP_Unit", store.Database);
        Assert.IsTrue(store.Apply);
    }

    [TestMethod]
    public async Task Cancellation_StopsBeforePersistence()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var store = new EmailChangeRecordingStore();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new AdministratorEmailChangeService(store)
                .ExecuteAsync(TenantId, UserId, "valid@example.invalid", null, false, null, source.Token));
        Assert.AreEqual(0, store.Calls);
    }
}

internal sealed class EmailChangeRecordingStore : IAdministratorEmailChangeStore
{
    public AdministratorEmailChangeStatus Status { get; init; } = AdministratorEmailChangeStatus.Ready;
    public int Calls { get; private set; }
    public string? Email { get; private set; }
    public string? Password { get; private set; }
    public bool Apply { get; private set; }
    public string? Database { get; private set; }

    public Task<AdministratorEmailChangeResult> ExecuteAsync(Guid tenantId, Guid userId,
        string email, string? password, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Email = email;
        Password = password;
        Apply = apply;
        Database = confirmedDatabase;
        return Task.FromResult(new AdministratorEmailChangeResult(Status));
    }
}
