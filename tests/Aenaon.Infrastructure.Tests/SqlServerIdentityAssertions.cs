using System.Data;
using System.Security.Claims;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Identity;
using Aenaon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

internal static class SqlServerIdentityAssertions
{
    private const string FixturePassword = "Aenaon-CI-only!7";

    public static async Task RunAsync(string connectionString, CancellationToken cancellationToken)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        Assert.AreEqual("127.0.0.1,14333", settings.DataSource);
        Assert.AreEqual("AenaonERP_Identity_CI", settings.InitialCatalog);
        var options = AenaonDbContextOptions.Create(connectionString, true);

        // Αναβάθμιση από την πραγματική προηγούμενη έκδοση, με ήδη αποθηκευμένα επιχειρησιακά δεδομένα.
        var original = Tenant.Create("Upgrade Διατήρηση ' δεδομένων");
        var originalStore = original.AddStore("MAIN", "Αρχικό κατάστημα");
        await using (var previous = new AenaonDbContext(options))
        {
            await previous.GetService<IMigrator>().MigrateAsync("20261109100218_InitialTenancy", cancellationToken);
            previous.Tenants.Add(original);
            await previous.SaveChangesAsync(cancellationToken);
        }
        var schema = new DatabaseSchemaManager(connectionString, true, NullLogger<DatabaseSchemaManager>.Instance);
        var preview = await schema.ExecuteAsync(false, null, cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.Ready, preview.Status);
        Assert.HasCount(1, preview.AppliedMigrations!);
        Assert.HasCount(9, preview.PendingMigrations!);

        // Παλαιότερα δεδομένα προετοιμάζονται στο schema της 6ης migration:
        // ο έλεγχος διασφαλίζει ότι η 7η δεν τα διαγράφει ή αλλάζει κωδικούς.
        Guid oldCategory = Guid.NewGuid();
        Guid oldProduct = Guid.NewGuid();
        Guid oldService = Guid.NewGuid();
        await using (var sixth = new AenaonDbContext(options))
        {
            await sixth.GetService<IMigrator>().MigrateAsync(
                "20261112130000_CatalogHierarchyAndNumbering", cancellationToken);
        }

        await using (var seededConnection = new SqlConnection(connectionString))
        {
            await seededConnection.OpenAsync(cancellationToken);
            await using var command = seededConnection.CreateCommand();
            command.CommandText = """
                INSERT INTO dbo.CatalogCategories (Id, TenantId, Code, Name, IsActive)
                VALUES (@category, @tenant, N'MIG-TEST', N'Κατηγορία πριν την αναβάθμιση', 1);

                INSERT INTO dbo.CatalogItems
                    (Id, TenantId, CategoryId, Code, Name, Kind, TracksStock, IsActive)
                VALUES
                    (@product, @tenant, @category, N'MIG-P', N'Παλαιό προϊόν', 1, 1, 1),
                    (@service, @tenant, @category, N'MIG-S', N'Παλαιά υπηρεσία', 2, 0, 1);
                """;
            command.Parameters.Add("@category", SqlDbType.UniqueIdentifier).Value = oldCategory;
            command.Parameters.Add("@product", SqlDbType.UniqueIdentifier).Value = oldProduct;
            command.Parameters.Add("@service", SqlDbType.UniqueIdentifier).Value = oldService;
            command.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = original.Id;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        Assert.AreEqual(DatabaseSchemaStatus.Updated,
            (await schema.ExecuteAsync(true, "AenaonERP_Identity_CI", cancellationToken)).Status);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new AenaonDbContext(options));
        services.AddIdentityCore<AenaonUser>(identity => identity.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AenaonDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AenaonDbContext>();

        // Η πραγματική αναβάθμιση διατηρεί είδη και αποδίδει ασφαλή αρχικά προφίλ.
        CatalogItem migratedProduct = await context.CatalogItems
            .SingleAsync(x => x.Id == oldProduct, cancellationToken);
        CatalogItem migratedService = await context.CatalogItems
            .SingleAsync(x => x.Id == oldService, cancellationToken);
        Assert.AreEqual("MIG-P", migratedProduct.Code);
        Assert.AreEqual("MIG-S", migratedService.Code);
        Assert.AreEqual(CatalogUnitOfMeasure.Piece, migratedProduct.UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.Merchandise, migratedProduct.AccountingType);
        Assert.AreEqual(CatalogUnitOfMeasure.Service, migratedService.UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.Service, migratedService.AccountingType);

        // Καθαρίζουμε αποκλειστικά το απομονωμένο fixture, όχι επιχειρησιακά δεδομένα.
        context.CatalogItems.RemoveRange(migratedProduct, migratedService);
        CatalogCategory seededCategory = await context.CatalogCategories
            .SingleAsync(x => x.Id == oldCategory, cancellationToken);
        context.CatalogCategories.Remove(seededCategory);
        await context.SaveChangesAsync(cancellationToken);

        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AenaonUser>>();
        var preserved = await context.Tenants.Include(tenant => tenant.Stores).SingleAsync(cancellationToken);
        Assert.AreEqual(original.Id, preserved.Id);
        Assert.AreEqual(original.Name, preserved.Name);
        var preservedStore = Assert.ContainsSingle(preserved.Stores);
        Assert.AreEqual(originalStore.Id, preservedStore.Id);
        Assert.AreEqual(originalStore.Name, preservedStore.Name);
        Assert.AreEqual(originalStore.Code, preservedStore.Code);
        Assert.AreEqual(0, await context.Users.CountAsync(cancellationToken));

        var second = Tenant.Create("Δεύτερη επιχείρηση πρόσβασης");
        context.Tenants.Add(second);
        await context.SaveChangesAsync(cancellationToken);
        var user = new AenaonUser { UserName = "owner@example.invalid", Email = "owner@example.invalid" };
        var created = await manager.CreateAsync(user, FixturePassword);
        Assert.IsTrue(created.Succeeded, string.Join(',', created.Errors.Select(error => error.Code)));
        Assert.IsTrue(await manager.CheckPasswordAsync(user, FixturePassword));
        string? persistedHash = await context.Users.Where(item => item.Id == user.Id)
            .Select(item => item.PasswordHash).SingleAsync(cancellationToken);
        Assert.IsNotNull(persistedHash);
        Assert.AreNotEqual(FixturePassword, persistedHash);
        Assert.DoesNotContain(FixturePassword, persistedHash);

        var secondUser = new AenaonUser { UserName = "second@example.invalid", Email = "second@example.invalid" };
        Assert.IsTrue((await manager.CreateAsync(secondUser, FixturePassword)).Succeeded);
        context.TenantMemberships.AddRange(
            TenantMembership.Create(original.Id, user.Id, TenantMembershipRole.Owner),
            TenantMembership.Create(second.Id, user.Id));
        await context.SaveChangesAsync(cancellationToken);
        var access = await context.TenantMemberships.Where(item => item.UserId == user.Id)
            .ToDictionaryAsync(item => item.TenantId, item => item.Role, cancellationToken);
        Assert.AreEqual(TenantMembershipRole.Owner, access[original.Id]);
        Assert.AreEqual(TenantMembershipRole.Member, access[second.Id]);

        var duplicateEmail = await manager.CreateAsync(new AenaonUser
        {
            UserName = "other@example.invalid",
            Email = "OWNER@example.invalid"
        }, FixturePassword);
        Assert.IsFalse(duplicateEmail.Succeeded);
        Assert.IsTrue(duplicateEmail.Errors.Any(error => error.Code == "DuplicateEmail"));

        // Η μοναδικότητα προστατεύεται και στη SQL όταν παρακαμφθεί το UserManager.
        await AssertDuplicateUserAsync(context, new AenaonUser
        {
            UserName = "bypass@example.invalid",
            NormalizedUserName = "BYPASS@EXAMPLE.INVALID",
            Email = user.Email,
            NormalizedEmail = user.NormalizedEmail
        }, cancellationToken);
        await AssertDuplicateUserAsync(context, new AenaonUser
        {
            UserName = user.UserName,
            NormalizedUserName = user.NormalizedUserName,
            Email = "unique@example.invalid",
            NormalizedEmail = "UNIQUE@EXAMPLE.INVALID"
        }, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var duplicateMembership = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertMembershipAsync(connection, original.Id, user.Id, 2, cancellationToken));
        Assert.IsTrue(duplicateMembership.Number is 2601 or 2627);
        var unknownUser = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertMembershipAsync(connection, original.Id, Guid.NewGuid(), 2, cancellationToken));
        Assert.AreEqual(547, unknownUser.Number);
        var unknownTenant = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertMembershipAsync(connection, Guid.NewGuid(), secondUser.Id, 2, cancellationToken));
        Assert.AreEqual(547, unknownTenant.Number);
        var invalidRole = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            InsertMembershipAsync(connection, original.Id, secondUser.Id, 99, cancellationToken));
        Assert.AreEqual(547, invalidRole.Number);

        Assert.IsTrue((await manager.AddClaimAsync(user, new Claim("fixture", "value"))).Succeeded);
        await AssertRestrictedDeleteAsync(connection, "DELETE FROM dbo.AspNetUsers WHERE Id = @id;", user.Id, cancellationToken);
        await AssertRestrictedDeleteAsync(connection, "DELETE FROM dbo.Tenants WHERE Id = @id;", second.Id, cancellationToken);
        Assert.AreEqual(2, await context.Users.CountAsync(cancellationToken));
        Assert.AreEqual(2, await context.TenantMemberships.CountAsync(cancellationToken));
        Assert.AreEqual(1, await context.UserClaims.CountAsync(cancellationToken));
        Assert.AreEqual(1, await context.Stores.CountAsync(cancellationToken));
        var repeated = await schema.ExecuteAsync(true, "AenaonERP_Identity_CI", cancellationToken);
        Assert.AreEqual(DatabaseSchemaStatus.Ready, repeated.Status);
        Assert.HasCount(10, repeated.AppliedMigrations!);
        Assert.HasCount(0, repeated.PendingMigrations!);
    }

    private static async Task AssertDuplicateUserAsync(AenaonDbContext context, AenaonUser user, CancellationToken cancellationToken)
    {
        context.Users.Add(user);
        var error = await Assert.ThrowsExactlyAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
        Assert.IsTrue(error.InnerException is SqlException { Number: 2601 or 2627 });
        context.Entry(user).State = EntityState.Detached;
    }

    private static async Task InsertMembershipAsync(SqlConnection connection, Guid tenantId, Guid userId, int role,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dbo.TenantMemberships (TenantId, UserId, Role) VALUES (@tenantId, @userId, @role);";
        command.Parameters.Add("@tenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@role", SqlDbType.Int).Value = role;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AssertRestrictedDeleteAsync(SqlConnection connection, string sql, Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        var error = await Assert.ThrowsExactlyAsync<SqlException>(async () => await command.ExecuteNonQueryAsync(cancellationToken));
        Assert.AreEqual(547, error.Number);
    }
}
