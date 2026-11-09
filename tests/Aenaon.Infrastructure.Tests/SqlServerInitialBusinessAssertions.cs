using Aenaon.Application.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

internal static class SqlServerInitialBusinessAssertions
{
    public static async Task RunAsync(string connectionString, CancellationToken cancellationToken)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        Assert.AreEqual("127.0.0.1,14333", settings.DataSource);
        Assert.AreEqual("AenaonERP_Startup_CI", settings.InitialCatalog);
        var schema = new DatabaseSchemaManager(connectionString, true, NullLogger<DatabaseSchemaManager>.Instance);
        var service = new InitialBusinessService(new SqlInitialBusinessStore(connectionString, true, schema,
            NullLogger<SqlInitialBusinessStore>.Instance));

        var notMigrated = await service.ExecuteAsync("Demo", "MAIN", "Κεντρικό", false, null, cancellationToken);
        Assert.AreEqual(InitialBusinessStatus.SchemaNotReady, notMigrated.Status);
        Assert.AreEqual(DatabaseSchemaStatus.Updated,
            (await schema.ExecuteAsync(true, "AenaonERP_Startup_CI", cancellationToken)).Status);
        await using var context = new AenaonDbContext(AenaonDbContextOptions.Create(connectionString, true));

        var preview = await service.ExecuteAsync("  Startup Δοκιμή  ", " main ", "  Κεντρικό  ", false, null, cancellationToken);
        Assert.AreEqual(InitialBusinessStatus.Ready, preview.Status);
        Assert.AreEqual("Startup Δοκιμή", preview.Summary!.TenantName);
        Assert.AreEqual("MAIN", preview.Summary.StoreCode);
        Assert.AreEqual(0, await context.Tenants.CountAsync(cancellationToken));
        Assert.AreEqual(0, await context.Stores.CountAsync(cancellationToken));

        var unconfirmed = await service.ExecuteAsync("Demo", "MAIN", "Κεντρικό", true, "WrongDatabase", cancellationToken);
        Assert.AreEqual(InitialBusinessStatus.InvalidConfiguration, unconfirmed.Status);

        // Προσωρινός περιορισμός αποκλειστικά στο CI: ελέγχει rollback όταν αποτύχει η εγγραφή καταστήματος.
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "ALTER TABLE dbo.Stores ADD CONSTRAINT CK_Stores_StartupFixture CHECK ([Code] <> N'BLOCKED');", cancellationToken);
        var failed = await service.ExecuteAsync("Startup Rollback", "BLOCKED", "Κεντρικό", true, "AenaonERP_Startup_CI", cancellationToken);
        Assert.AreEqual(InitialBusinessStatus.Unavailable, failed.Status);
        Assert.AreEqual(547, failed.SqlErrorNumber);
        Assert.AreEqual(0, await context.Tenants.CountAsync(cancellationToken));
        Assert.AreEqual(0, await context.Stores.CountAsync(cancellationToken));
        await ExecuteAsync(connection, "ALTER TABLE dbo.Stores DROP CONSTRAINT CK_Stores_StartupFixture;", cancellationToken);

        // Δύο ταυτόχρονες εντολές πρέπει να παράγουν μία μόνο αρχική επιχείρηση/κατάστημα.
        var results = await Task.WhenAll(
            service.ExecuteAsync("Startup Δοκιμή Α '", " main ", "Κεντρικό Α", true, "AenaonERP_Startup_CI", cancellationToken),
            service.ExecuteAsync("Startup Δοκιμή Β", " main ", "Κεντρικό Β", true, "AenaonERP_Startup_CI", cancellationToken));
        Assert.AreEqual(1, results.Count(result => result.Status == InitialBusinessStatus.Created));
        Assert.AreEqual(1, results.Count(result => result.Status == InitialBusinessStatus.ExistingData));
        var created = results.Single(result => result.Status == InitialBusinessStatus.Created);
        var tenant = await context.Tenants.AsNoTracking().Include(item => item.Stores).SingleAsync(cancellationToken);
        Assert.AreEqual(created.TenantId, tenant.Id);
        Assert.AreEqual(created.Summary!.TenantName, tenant.Name);
        var store = Assert.ContainsSingle(tenant.Stores);
        Assert.AreEqual(created.StoreId, store.Id);
        Assert.AreEqual(tenant.Id, store.TenantId);
        Assert.AreEqual("MAIN", store.Code);
        Assert.AreEqual(1, await context.Stores.CountAsync(cancellationToken));

        var repeated = await service.ExecuteAsync("Other name", "OTHER", "Άλλο κατάστημα", true, "AenaonERP_Startup_CI", cancellationToken);
        Assert.AreEqual(InitialBusinessStatus.ExistingData, repeated.Status);
        Assert.AreEqual(tenant.Name, (await context.Tenants.AsNoTracking().SingleAsync(cancellationToken)).Name);
        Assert.AreEqual(1, await context.Tenants.CountAsync(cancellationToken));
        Assert.AreEqual(1, await context.Stores.CountAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
