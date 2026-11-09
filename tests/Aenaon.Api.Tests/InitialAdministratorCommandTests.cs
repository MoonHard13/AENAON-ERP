using Aenaon.Api.Commands;
using Aenaon.Application.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class InitialAdministratorCommandTests
{
    private const string TenantId = "9698df79-3c31-41c9-b292-b7240c7265fb";

    [TestMethod]
    public async Task Preview_NeverReadsPassword()
    {
        var store = new AdministratorCommandStore(InitialAdministratorStatus.Ready);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialAdministratorCommand.ExecuteAsync(
            ["--dry-run", "--tenant-id", TenantId, "--username", "admin", "--email", "admin@example.invalid"],
            new InitialAdministratorService(store), output, error, () => throw new InvalidOperationException(),
            false, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.AreEqual(1, store.Calls);
        Assert.IsFalse(store.Apply);
        Assert.Contains("προεπισκόπηση", output.ToString());
    }

    [TestMethod]
    public async Task Apply_RequiresTwoMatchingInteractivePasswords()
    {
        var store = new AdministratorCommandStore(InitialAdministratorStatus.Created);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int reads = 0;

        int code = await InitialAdministratorCommand.ExecuteAsync(
            ["--apply", "--confirm-database", "AenaonERP_Unit", "--tenant-id", TenantId,
                "--username", "admin", "--email", "admin@example.invalid"],
            new InitialAdministratorService(store), output, error,
            () => { reads++; return "StrongPassword!1234"; }, true, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.AreEqual(2, reads);
        Assert.AreEqual(1, store.Calls);
        Assert.AreEqual("AenaonERP_Unit", store.Confirmation);
        Assert.DoesNotContain("StrongPassword!1234", output.ToString());
        Assert.DoesNotContain("StrongPassword!1234", error.ToString());
    }

    [TestMethod]
    [DataRow("missing-confirmation")]
    [DataRow("duplicate-mode")]
    [DataRow("password-argument")]
    [DataRow("unknown-option")]
    [DataRow("missing-tenant")]
    [DataRow("bad-guid")]
    public async Task InvalidArguments_NeverReadPasswordOrCallStore(string scenario)
    {
        string[] args = ["--tenant-id", TenantId, "--username", "admin", "--email", "admin@example.invalid"];
        args = scenario switch
        {
            "missing-confirmation" => [.. args, "--apply"],
            "duplicate-mode" => [.. args, "--dry-run", "--apply"],
            "password-argument" => [.. args, "--apply", "--confirm-database", "AenaonERP_Unit", "--password", "secret"],
            "unknown-option" => [.. args, "--dry-run", "--force"],
            "missing-tenant" => ["--dry-run", "--username", "admin", "--email", "admin@example.invalid"],
            _ => ["--dry-run", "--tenant-id", "invalid", "--username", "admin", "--email", "admin@example.invalid"]
        };
        var store = new AdministratorCommandStore(InitialAdministratorStatus.Created);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialAdministratorCommand.ExecuteAsync(args, new InitialAdministratorService(store),
            output, error, () => throw new InvalidOperationException(), true, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task Apply_RejectsRedirectedInput()
    {
        var store = new AdministratorCommandStore(InitialAdministratorStatus.Created);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialAdministratorCommand.ExecuteAsync(
            ["--apply", "--confirm-database", "AenaonERP_Unit", "--tenant-id", TenantId,
                "--username", "admin", "--email", "admin@example.invalid"],
            new InitialAdministratorService(store), output, error,
            () => throw new InvalidOperationException(), false, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task Apply_MismatchedPasswordsCannotReachDatabase()
    {
        var store = new AdministratorCommandStore(InitialAdministratorStatus.Created);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int reads = 0;

        int code = await InitialAdministratorCommand.ExecuteAsync(
            ["--apply", "--confirm-database", "AenaonERP_Unit", "--tenant-id", TenantId,
                "--username", "admin", "--email", "admin@example.invalid"],
            new InitialAdministratorService(store), output, error,
            () => ++reads == 1 ? "first" : "second", true, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task Cancellation_Returns130()
    {
        var store = new AdministratorCommandStore(InitialAdministratorStatus.Created);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialAdministratorCommand.ExecuteAsync(
            ["--dry-run", "--tenant-id", TenantId, "--username", "admin", "--email", "admin@example.invalid"],
            new InitialAdministratorService(store), output, error,
            () => throw new InvalidOperationException(), false, cancellation.Token);

        Assert.AreEqual(130, code);
        Assert.AreEqual(0, store.Calls);
    }
}

internal sealed class AdministratorCommandStore(InitialAdministratorStatus status) : IInitialAdministratorStore
{
    public int Calls { get; private set; }
    public bool Apply { get; private set; }
    public string? Confirmation { get; private set; }

    public Task<InitialAdministratorResult> ExecuteAsync(Guid tenantId, string userName, string email,
        string? password, bool apply, string? confirmedDatabase, CancellationToken cancellationToken = default)
    {
        Calls++;
        Apply = apply;
        Confirmation = confirmedDatabase;
        return Task.FromResult(new InitialAdministratorResult(status, DatabaseName: "AenaonERP_Unit",
            TenantName: "Demo", UserId: Guid.NewGuid()));
    }
}
