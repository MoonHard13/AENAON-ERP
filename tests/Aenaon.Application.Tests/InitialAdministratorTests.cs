using Aenaon.Application.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

[TestClass]
public sealed class InitialAdministratorTests
{
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task Preview_NormalizesInputWithoutPassingPassword()
    {
        var store = new RecordingAdministratorStore();
        var result = await new InitialAdministratorService(store)
            .ExecuteAsync(TenantId, "  admin  ", "  admin@example.invalid  ", null, false, null);

        Assert.AreEqual(InitialAdministratorStatus.Ready, result.Status);
        Assert.AreEqual("admin", store.UserName);
        Assert.AreEqual("admin@example.invalid", store.Email);
        Assert.IsNull(store.Password);
        Assert.IsFalse(store.Apply);
    }

    [TestMethod]
    public async Task Apply_PreservesConfirmationAndPasswordForIdentityStore()
    {
        var store = new RecordingAdministratorStore { Status = InitialAdministratorStatus.Created };
        var result = await new InitialAdministratorService(store)
            .ExecuteAsync(TenantId, "owner", "owner@example.invalid", "StrongPassword!1234",
                true, "AenaonERP_Unit");

        Assert.AreEqual(InitialAdministratorStatus.Created, result.Status);
        Assert.AreEqual("AenaonERP_Unit", store.ConfirmedDatabase);
        Assert.IsTrue(store.Apply);
        Assert.AreEqual("StrongPassword!1234", store.Password);
    }

    [TestMethod]
    [DataRow("empty-tenant")]
    [DataRow("bad-username")]
    [DataRow("bad-email")]
    [DataRow("missing-password")]
    [DataRow("missing-confirmation")]
    [DataRow("preview-password")]
    public async Task InvalidInput_IsRejectedBeforeStore(string scenario)
    {
        var store = new RecordingAdministratorStore();
        Guid tenantId = scenario == "empty-tenant" ? Guid.Empty : TenantId;
        string userName = scenario == "bad-username" ? "bad name" : "admin";
        string email = scenario == "bad-email" ? "not-an-email" : "admin@example.invalid";
        bool apply = scenario is not "preview-password";
        string? password = scenario == "missing-password" ? null : "StrongPassword!1234";
        string? confirmation = scenario == "missing-confirmation" ? null : "AenaonERP_Unit";

        var result = await new InitialAdministratorService(store)
            .ExecuteAsync(tenantId, userName, email, password, apply, confirmation);

        Assert.AreEqual(InitialAdministratorStatus.ValidationFailed, result.Status);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task Cancellation_DoesNotCallStore()
    {
        var store = new RecordingAdministratorStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new InitialAdministratorService(store).ExecuteAsync(TenantId, "admin", "admin@example.invalid",
                null, false, null, cancellation.Token));
        Assert.AreEqual(0, store.Calls);
    }
}

internal sealed class RecordingAdministratorStore : IInitialAdministratorStore
{
    public InitialAdministratorStatus Status { get; init; } = InitialAdministratorStatus.Ready;
    public int Calls { get; private set; }
    public string? UserName { get; private set; }
    public string? Email { get; private set; }
    public string? Password { get; private set; }
    public bool Apply { get; private set; }
    public string? ConfirmedDatabase { get; private set; }

    public Task<InitialAdministratorResult> ExecuteAsync(Guid tenantId, string userName, string email,
        string? password, bool apply, string? confirmedDatabase, CancellationToken cancellationToken = default)
    {
        Calls++;
        UserName = userName;
        Email = email;
        Password = password;
        Apply = apply;
        ConfirmedDatabase = confirmedDatabase;
        return Task.FromResult(new InitialAdministratorResult(Status, DatabaseName: "AenaonERP_Unit", TenantName: "Demo"));
    }
}
