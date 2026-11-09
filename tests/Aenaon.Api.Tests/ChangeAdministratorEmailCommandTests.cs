using Aenaon.Api.Commands;
using Aenaon.Application.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class ChangeAdministratorEmailCommandTests
{
    private const string TenantId = "9698df79-3c31-41c9-b292-b7240c7265fb";
    private const string UserId = "76d2c37f-2e26-4311-a129-e6073057c8dd";

    [TestMethod]
    public async Task Preview_DoesNotReadPassword()
    {
        var store = new EmailChangeCommandStore();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ChangeAdministratorEmailCommand.ExecuteAsync(
            ["--dry-run", "--tenant-id", TenantId, "--user-id", UserId, "--new-email", "valid@example.invalid"],
            new AdministratorEmailChangeService(store),
            output, error, () => throw new InvalidOperationException(), false, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.AreEqual(1, store.Calls);
        Assert.IsFalse(store.Apply);
    }

    [TestMethod]
    public async Task Apply_RequiresInteractivePassword()
    {
        var store = new EmailChangeCommandStore { Status = AdministratorEmailChangeStatus.Updated };
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ChangeAdministratorEmailCommand.ExecuteAsync(
            ["--apply", "--confirm-database", "AenaonERP_Unit", "--tenant-id", TenantId,
                "--user-id", UserId, "--new-email", "valid@example.invalid"],
            new AdministratorEmailChangeService(store), output, error,
            () => "MyExamplePassword!9", true, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.AreEqual(1, store.Calls);
        Assert.AreEqual("AenaonERP_Unit", store.Database);
        Assert.DoesNotContain("MyExamplePassword!9", output.ToString());
        Assert.DoesNotContain("MyExamplePassword!9", error.ToString());
    }

    [TestMethod]
    [DataRow("bad-guid")]
    [DataRow("duplicate-mode")]
    [DataRow("password-argument")]
    [DataRow("missing-confirmation")]
    [DataRow("extra-option")]
    public async Task InvalidParameters_DoNotReachStore(string scenario)
    {
        string[] original = ["--tenant-id", TenantId, "--user-id", UserId, "--new-email", "valid@example.invalid"];
        string[] args = scenario switch
        {
            "bad-guid" => ["--dry-run", "--tenant-id", "oops", "--user-id", UserId, "--new-email", "valid@example.invalid"],
            "duplicate-mode" => [.. original, "--dry-run", "--apply"],
            "password-argument" => [.. original, "--apply", "--confirm-database", "AenaonERP_Unit", "--password", "secret"],
            "missing-confirmation" => [.. original, "--apply"],
            _ => [.. original, "--dry-run", "--force"]
        };
        var store = new EmailChangeCommandStore();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ChangeAdministratorEmailCommand.ExecuteAsync(args,
            new AdministratorEmailChangeService(store), output, error,
            () => throw new InvalidOperationException(), true, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task RedirectedInput_RefusedBeforeStore()
    {
        var store = new EmailChangeCommandStore();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ChangeAdministratorEmailCommand.ExecuteAsync(
            ["--apply", "--confirm-database", "AenaonERP_Unit", "--tenant-id", TenantId,
                "--user-id", UserId, "--new-email", "valid@example.invalid"],
            new AdministratorEmailChangeService(store), output, error,
            () => throw new InvalidOperationException(), false, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, store.Calls);
    }
}

internal sealed class EmailChangeCommandStore : IAdministratorEmailChangeStore
{
    public AdministratorEmailChangeStatus Status { get; init; } = AdministratorEmailChangeStatus.Ready;
    public int Calls { get; private set; }
    public bool Apply { get; private set; }
    public string? Database { get; private set; }

    public Task<AdministratorEmailChangeResult> ExecuteAsync(Guid tenantId, Guid userId,
        string email, string? password, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Apply = apply;
        Database = confirmedDatabase;
        return Task.FromResult(new AdministratorEmailChangeResult(Status, "AenaonERP_Unit", userId));
    }
}
