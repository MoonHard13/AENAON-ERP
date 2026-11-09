using Aenaon.Api.Commands;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class InitialBusinessCommandTests
{
    [TestMethod]
    [DataRow(InitialBusinessStatus.Ready, 0)]
    [DataRow(InitialBusinessStatus.Created, 0)]
    [DataRow(InitialBusinessStatus.ExistingData, 2)]
    [DataRow(InitialBusinessStatus.NotConfigured, 2)]
    [DataRow(InitialBusinessStatus.InvalidConfiguration, 2)]
    [DataRow(InitialBusinessStatus.SchemaNotReady, 2)]
    [DataRow(InitialBusinessStatus.Busy, 1)]
    [DataRow(InitialBusinessStatus.Unavailable, 1)]
    [DataRow(InitialBusinessStatus.TimedOut, 1)]
    public async Task Preview_UsesReadOnlyModeAndMeaningfulExitCodes(InitialBusinessStatus status, int expected)
    {
        var store = new CommandBusinessStore(status);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialBusinessCommand.ExecuteAsync(
            ["--dry-run", "--tenant-name", "Demo", "--store-code", "main", "--store-name", "Κεντρικό"],
            new InitialBusinessService(store), output, error, CancellationToken.None);

        Assert.AreEqual(expected, code);
        Assert.IsFalse(store.Apply);
        Assert.IsNotEmpty(code == 0 ? output.ToString() : error.ToString());
    }

    [TestMethod]
    public async Task Apply_RequiresAndPassesDatabaseConfirmation()
    {
        var store = new CommandBusinessStore(InitialBusinessStatus.Created);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialBusinessCommand.ExecuteAsync(
            ["--tenant-name", "Demo", "--apply", "--confirm-database", "AenaonERP_Unit", "--store-code", "MAIN", "--store-name", "Κεντρικό"],
            new InitialBusinessService(store), output, error, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.IsTrue(store.Apply);
        Assert.AreEqual("AenaonERP_Unit", store.Confirmation);
        Assert.Contains("TenantId:", output.ToString());
    }

    [TestMethod]
    [DataRow("missing-confirmation")]
    [DataRow("duplicate-mode")]
    [DataRow("duplicate-option")]
    [DataRow("unknown-option")]
    [DataRow("missing-field")]
    public async Task InvalidArguments_DoNotReachPersistence(string scenario)
    {
        string[] args = ["--tenant-name", "Demo", "--store-code", "MAIN", "--store-name", "Κεντρικό"];
        args = scenario switch
        {
            "missing-confirmation" => [.. args, "--apply"],
            "duplicate-mode" => [.. args, "--apply", "--dry-run"],
            "duplicate-option" => [.. args, "--dry-run", "--tenant-name", "Other"],
            "unknown-option" => [.. args, "--dry-run", "--force"],
            _ => ["--dry-run", "--tenant-name", "Demo"]
        };
        var store = new CommandBusinessStore(InitialBusinessStatus.Created);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await InitialBusinessCommand.ExecuteAsync(args, new InitialBusinessService(store), output, error, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task Cancellation_Returns130WithoutCallingPersistence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var store = new CommandBusinessStore(InitialBusinessStatus.Created);

        int code = await InitialBusinessCommand.ExecuteAsync(
            ["--dry-run", "--tenant-name", "Demo", "--store-code", "MAIN", "--store-name", "Κεντρικό"],
            new InitialBusinessService(store), output, error, cancellation.Token);

        Assert.AreEqual(130, code);
        Assert.AreEqual(0, store.Calls);
    }
}

internal sealed class CommandBusinessStore(InitialBusinessStatus status) : IInitialBusinessStore
{
    public int Calls { get; private set; }
    public bool Apply { get; private set; }
    public string? Confirmation { get; private set; }

    public Task<InitialBusinessResult> ExecuteAsync(Tenant tenant, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Apply = apply;
        Confirmation = confirmedDatabase;
        return Task.FromResult(new InitialBusinessResult(status, tenant.Id, tenant.Stores[0].Id, "AenaonERP_Unit"));
    }
}
