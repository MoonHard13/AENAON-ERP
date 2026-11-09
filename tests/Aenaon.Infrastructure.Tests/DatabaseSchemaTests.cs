using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

[TestClass]
public sealed class DatabaseSchemaTests
{
    [TestMethod]
    public void MigrationSnapshot_MatchesCurrentModel()
    {
        using var context = new AenaonDbContext(AenaonDbContextOptions.Create(
            "Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=unit-only;Encrypt=True;", false));

        Assert.IsFalse(context.Database.HasPendingModelChanges());
        Assert.HasCount(1, context.Database.GetMigrations().ToArray());
    }

    [TestMethod]
    public void ReviewedSqlScript_MatchesTheExecutableMigration()
    {
        using var context = new AenaonDbContext(AenaonDbContextOptions.Create(
            "Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=unit-only;Encrypt=True;", false));
        string generated = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        string reviewed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "001-initial-tenancy.sql"));

        Assert.AreEqual(generated.ReplaceLineEndings("\n").Trim(), reviewed.ReplaceLineEndings("\n").Trim());
    }

    [TestMethod]
    [DataRow(null, true, false, null, DatabaseSchemaStatus.NotConfigured)]
    [DataRow("invalid-key=unit-secret", true, false, null, DatabaseSchemaStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=master;User ID=unit;Password=unit-secret;Encrypt=True;", true, false, null, DatabaseSchemaStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=unit-secret;Encrypt=True;", false, false, null, DatabaseSchemaStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=unit-secret;Encrypt=True;", true, true, null, DatabaseSchemaStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=unit-secret;Encrypt=True;", true, true, "DifferentDatabase", DatabaseSchemaStatus.InvalidConfiguration)]
    public async Task SchemaManager_RejectsUnsafeConfigurationBeforeConnecting(string? value, bool development,
        bool apply, string? confirmation, DatabaseSchemaStatus expected)
    {
        var logger = new CaptureLogger<DatabaseSchemaManager>();
        var manager = new DatabaseSchemaManager(value, development, logger);

        var result = await manager.ExecuteAsync(apply, confirmation);

        Assert.AreEqual(expected, result.Status);
        Assert.DoesNotContain("unit-secret", string.Join('\n', logger.Messages));
        Assert.DoesNotContain("example.invalid", string.Join('\n', logger.Messages));
    }

    [TestMethod]
    public async Task SchemaManager_PropagatesCallerCancellationBeforeConnecting()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var manager = new DatabaseSchemaManager(null, true, new CaptureLogger<DatabaseSchemaManager>());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => manager.ExecuteAsync(false, null, cancellation.Token));
    }
}
