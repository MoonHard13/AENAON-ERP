using System.Data;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

internal static class SqlServerSchemaAssertions
{
    public static async Task RunAsync(string connectionString, CancellationToken cancellationToken)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        Assert.AreEqual("127.0.0.1,14333", settings.DataSource);
        Assert.AreEqual("AenaonERP_CI", settings.InitialCatalog);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var logger = new CaptureLogger<DatabaseSchemaManager>();
        var manager = new DatabaseSchemaManager(connectionString, true, logger);

        var preview = await manager.ExecuteAsync(false, null, cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.Ready, preview.Status);
        Assert.HasCount(8, preview.PendingMigrations!);
        Assert.HasCount(0, preview.AppliedMigrations!);
        await AssertUserTableCountAsync(connection, 0, cancellationToken);

        var unconfirmed = await manager.ExecuteAsync(true, "WrongDatabase", cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.InvalidConfiguration, unconfirmed.Status);
        await AssertUserTableCountAsync(connection, 0, cancellationToken);

        // Η αρχική migration πρέπει να απορρίπτει μια βάση με άσχετα, μη καταγεγραμμένα αντικείμενα.
        await ExecuteAsync(connection, "CREATE TABLE dbo.ExternalFixture (Id int NOT NULL);", cancellationToken);
        var foreignSchema = await manager.ExecuteAsync(true, "AenaonERP_CI", cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.UnsafeDatabase, foreignSchema.Status);
        await AssertUserTableCountAsync(connection, 1, cancellationToken);
        await ExecuteAsync(connection, "DROP TABLE dbo.ExternalFixture;", cancellationToken);

        var applied = await manager.ExecuteAsync(true, "AenaonERP_CI", cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.Updated, applied.Status);
        Assert.HasCount(8, applied.AppliedMigrations!);
        Assert.HasCount(0, applied.PendingMigrations!);
        await AssertUserTableCountAsync(connection, 19, cancellationToken);

        var options = AenaonDbContextOptions.Create(connectionString, true);
        await using var context = new AenaonDbContext(options);
        var first = Tenant.Create("Δοκιμή Αθήνας ' OR 1=1--");
        first.AddStore("main", "Κεντρικό κατάστημα");
        first.AddStore("ath-02", "Δεύτερο κατάστημα");
        var second = Tenant.Create("Δοκιμή Θεσσαλονίκης");
        second.AddStore("MAIN", "Ανεξάρτητο κεντρικό");
        context.Tenants.AddRange(first, second);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var loaded = await context.Tenants.Include(tenant => tenant.Stores)
            .SingleAsync(tenant => tenant.Id == first.Id, cancellationToken);
        Assert.AreEqual(first.Id, loaded.Id);
        Assert.AreEqual(first.Name, loaded.Name);
        Assert.HasCount(2, loaded.Stores);
        Assert.IsTrue(loaded.Stores.All(store => store.TenantId == first.Id));
        Assert.AreEqual(1, await context.Stores.CountAsync(store => store.TenantId == second.Id, cancellationToken));
        loaded.AddStore("ATH-03", "Προσθήκη μετά την ανάγνωση");
        await context.SaveChangesAsync(cancellationToken);

        // Η μοναδικότητα ελέγχεται στη SQL ακόμη και αν παρακαμφθεί η κανονικοποίηση του Domain.
        var duplicate = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertStoreAsync(connection, first.Id, "main", cancellationToken));
        Assert.IsTrue(duplicate.Number is 2601 or 2627);
        var orphan = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertStoreAsync(connection, Guid.NewGuid(), "ORPHAN", cancellationToken));
        Assert.AreEqual(547, orphan.Number);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM dbo.Tenants WHERE Id = @id;";
            command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = first.Id;
            var restricted = await Assert.ThrowsExactlyAsync<SqlException>(async () =>
                await command.ExecuteNonQueryAsync(cancellationToken));
            Assert.AreEqual(547, restricted.Number);
        }

        var repeated = await manager.ExecuteAsync(true, "AenaonERP_CI", cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.Ready, repeated.Status);
        Assert.HasCount(0, repeated.PendingMigrations!);
        Assert.AreEqual(2, await context.Tenants.CountAsync(cancellationToken));
        Assert.AreEqual(4, await context.Stores.CountAsync(cancellationToken));

        await SqlServerCatalogAssertions.RunAsync(connection, context, first.Id, second.Id, cancellationToken);
        await SqlServerPriceListAssertions.RunAsync(context, first.Id, second.Id, cancellationToken);
        await SqlServerFiscalAssertions.RunAsync(connection, context, first.Id, second.Id, cancellationToken);
        await SqlServerFiscalStagingAssertions.RunAsync(connection, context, cancellationToken);

        // Μια μελλοντική ή ξένη έκδοση στο ιστορικό αποκλείει την εφαρμογή παλαιότερου κώδικα.
        await ExecuteAsync(connection, """
            INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N'99999999999999_FutureFixture', N'10.0.14');
            """, cancellationToken);
        var futureSchema = await manager.ExecuteAsync(false, null, cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.UnsafeDatabase, futureSchema.Status);
        Assert.DoesNotContain(settings.Password, string.Join('\n', logger.Messages));
    }

    private static async Task AssertUserTableCountAsync(SqlConnection connection, int expected, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;";
        Assert.AreEqual(expected, await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertStoreAsync(SqlConnection connection, Guid tenantId, string code,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dbo.Stores (Id, TenantId, Code, Name) VALUES (@id, @tenantId, @code, @name);";
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@tenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
        command.Parameters.Add("@code", SqlDbType.NVarChar, 30).Value = code;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 160).Value = "Δοκιμαστικό κατάστημα";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
