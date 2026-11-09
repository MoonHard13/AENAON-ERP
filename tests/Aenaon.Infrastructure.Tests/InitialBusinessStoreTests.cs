using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

[TestClass]
public sealed class InitialBusinessStoreTests
{
    [TestMethod]
    [DataRow(null, true, true, "AenaonERP_Unit", InitialBusinessStatus.NotConfigured)]
    [DataRow("invalid-key=fixture-secret", true, true, "AenaonERP_Unit", InitialBusinessStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=master;User ID=unit;Password=fixture-secret;Encrypt=True;", true, true, "master", InitialBusinessStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=fixture-secret;Encrypt=True;", false, false, null, InitialBusinessStatus.InvalidConfiguration)]
    [DataRow("Server=example.invalid;Database=AenaonERP_Unit;User ID=unit;Password=fixture-secret;Encrypt=True;", true, true, "OtherDatabase", InitialBusinessStatus.InvalidConfiguration)]
    public async Task UnsafeConfiguration_IsRejectedBeforeSqlAccess(string? value, bool development,
        bool apply, string? confirmation, InitialBusinessStatus expected)
    {
        var logger = new CaptureLogger<SqlInitialBusinessStore>();
        var tenant = Tenant.Create("Ιδιωτικό όνομα fixture");
        tenant.AddStore("MAIN", "Κεντρικό");
        var schema = new UnusedSchemaManager();

        var result = await new SqlInitialBusinessStore(value, development, schema, logger)
            .ExecuteAsync(tenant, apply, confirmation);

        Assert.AreEqual(expected, result.Status);
        Assert.AreEqual(0, schema.Calls);
        string logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("fixture-secret", logs);
        Assert.DoesNotContain(tenant.Name, logs);
    }

    [TestMethod]
    public async Task Cancellation_IsPropagatedBeforeSqlAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new SqlInitialBusinessStore(null, true, new UnusedSchemaManager(), new CaptureLogger<SqlInitialBusinessStore>());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            store.ExecuteAsync(Tenant.Create("Demo"), false, null, cancellation.Token));
    }
}

internal sealed class UnusedSchemaManager : IDatabaseSchemaManager
{
    public int Calls { get; private set; }

    public Task<DatabaseSchemaResult> ExecuteAsync(bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException("Δεν πρέπει να εκτελεστεί πρόσβαση SQL σε αυτόν τον έλεγχο.");
    }
}
