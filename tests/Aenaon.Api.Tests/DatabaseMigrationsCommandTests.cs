using Aenaon.Api.Commands;
using Aenaon.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class DatabaseMigrationsCommandTests
{
    [TestMethod]
    [DataRow(DatabaseSchemaStatus.Ready, 0)]
    [DataRow(DatabaseSchemaStatus.Updated, 0)]
    [DataRow(DatabaseSchemaStatus.NotConfigured, 2)]
    [DataRow(DatabaseSchemaStatus.InvalidConfiguration, 2)]
    [DataRow(DatabaseSchemaStatus.UnsafeDatabase, 2)]
    [DataRow(DatabaseSchemaStatus.Unavailable, 1)]
    [DataRow(DatabaseSchemaStatus.TimedOut, 1)]
    public async Task Command_ReturnsMeaningfulExitCode(DatabaseSchemaStatus status, int expected)
    {
        var manager = new FakeSchemaManager(status);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await DatabaseMigrationsCommand.ExecuteAsync(["--dry-run"], manager, output, error, CancellationToken.None);

        Assert.AreEqual(expected, code);
        Assert.IsFalse(manager.Apply);
        Assert.AreEqual(1, manager.Calls);
        Assert.IsNotEmpty(code == 0 ? output.ToString() : error.ToString());
    }

    [TestMethod]
    public async Task Apply_PassesExplicitDatabaseConfirmation()
    {
        var manager = new FakeSchemaManager(DatabaseSchemaStatus.Updated);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await DatabaseMigrationsCommand.ExecuteAsync(
            ["--apply", "--confirm-database", "AenaonERP_Unit"], manager, output, error, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.IsTrue(manager.Apply);
        Assert.AreEqual("AenaonERP_Unit", manager.Confirmation);
    }

    [TestMethod]
    [DataRow("missing-mode")]
    [DataRow("missing-confirmation")]
    [DataRow("ambiguous-mode")]
    [DataRow("downgrade")]
    public async Task InvalidArguments_DoNotCallSchemaManager(string scenario)
    {
        string[] args = scenario switch
        {
            "missing-mode" => [],
            "missing-confirmation" => ["--apply"],
            "ambiguous-mode" => ["--dry-run", "--apply"],
            _ => ["--rollback", "0"]
        };
        var manager = new FakeSchemaManager(DatabaseSchemaStatus.Ready);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await DatabaseMigrationsCommand.ExecuteAsync(args, manager, output, error, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, manager.Calls);
    }

    [TestMethod]
    public async Task Cancellation_ReturnsExitCode130()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await DatabaseMigrationsCommand.ExecuteAsync(["--dry-run"],
            new FakeSchemaManager(DatabaseSchemaStatus.Ready), output, error, cancellation.Token);

        Assert.AreEqual(130, code);
    }
}

internal sealed class FakeSchemaManager(DatabaseSchemaStatus status) : IDatabaseSchemaManager
{
    public int Calls { get; private set; }
    public bool Apply { get; private set; }
    public string? Confirmation { get; private set; }

    public Task<DatabaseSchemaResult> ExecuteAsync(bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        Apply = apply;
        Confirmation = confirmedDatabase;
        return Task.FromResult(new DatabaseSchemaResult(status, "AenaonERP_Unit", [], ["InitialTenancy"]));
    }
}
