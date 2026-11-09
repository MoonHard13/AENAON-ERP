using Aenaon.Application.Identity;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Identity;
using Aenaon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Επαληθεύει προεπισκόπηση, hashing, ατομικότητα και μη επαναλαμβανόμενη αρχικοποίηση σε απομονωμένο SQL.</summary>
internal static class SqlServerFirstAdministratorAssertions
{
    private const string FixturePassword = "Aenaon-Admin-CI!72";
    private const string DatabaseName = "AenaonERP_Admin_CI";

    public static async Task RunAsync(string connectionString, CancellationToken cancellationToken)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        Assert.AreEqual("127.0.0.1,14333", settings.DataSource);
        Assert.AreEqual(DatabaseName, settings.InitialCatalog);

        var schema = new DatabaseSchemaManager(connectionString, true, NullLogger<DatabaseSchemaManager>.Instance);
        Assert.AreEqual(DatabaseSchemaStatus.Updated,
            (await schema.ExecuteAsync(true, DatabaseName, cancellationToken)).Status);

        var tenant = Tenant.Create("Αρχική επιχείρηση διαχειριστή");
        tenant.AddStore("MAIN", "Κεντρικό");
        var options = AenaonDbContextOptions.Create(connectionString, true);
        await using (var context = new AenaonDbContext(options))
        {
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync(cancellationToken);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new AenaonDbContext(options));
        services.AddIdentityCore<AenaonUser>(identity =>
        {
            identity.User.RequireUniqueEmail = true;
            identity.Password.RequiredLength = 14;
            identity.Password.RequiredUniqueChars = 6;
            identity.Password.RequireDigit = true;
            identity.Password.RequireUppercase = true;
            identity.Password.RequireLowercase = true;
            identity.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<AenaonDbContext>();
        await using var provider = services.BuildServiceProvider();
        var store = new SqlInitialAdministratorStore(connectionString, true, schema,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SqlInitialAdministratorStore>.Instance);
        var service = new InitialAdministratorService(store);

        var missingTenant = await service.ExecuteAsync(Guid.NewGuid(), "admin", "admin@example.invalid",
            null, false, null, cancellationToken);
        Assert.AreEqual(InitialAdministratorStatus.TenantNotFound, missingTenant.Status);

        var preview = await service.ExecuteAsync(tenant.Id, "admin", "admin@example.invalid",
            null, false, null, cancellationToken);
        Assert.AreEqual(InitialAdministratorStatus.Ready, preview.Status);
        Assert.AreEqual(tenant.Name, preview.TenantName);
        await using (var context = new AenaonDbContext(options))
        {
            Assert.AreEqual(0, await context.Users.CountAsync(cancellationToken));
            Assert.AreEqual(0, await context.TenantMemberships.CountAsync(cancellationToken));
        }

        var unconfirmed = await service.ExecuteAsync(tenant.Id, "admin", "admin@example.invalid",
            FixturePassword, true, "OtherDatabase", cancellationToken);
        Assert.AreEqual(InitialAdministratorStatus.InvalidConfiguration, unconfirmed.Status);

        var weakPassword = await service.ExecuteAsync(tenant.Id, "admin", "admin@example.invalid",
            "weak", true, DatabaseName, cancellationToken);
        Assert.AreEqual(InitialAdministratorStatus.PasswordRejected, weakPassword.Status);

        // Εξαναγκασμένη αποτυχία membership για να αποδειχτεί rollback και της εγγραφής Identity.
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            ALTER TABLE dbo.TenantMemberships
            ADD CONSTRAINT CK_TenantMemberships_AdminFixture CHECK ([Role] <> 1);
            """, cancellationToken);
        var rollback = await service.ExecuteAsync(tenant.Id, "admin", "admin@example.invalid",
            FixturePassword, true, DatabaseName, cancellationToken);
        Assert.AreEqual(InitialAdministratorStatus.Unavailable, rollback.Status);
        Assert.AreEqual(547, rollback.SqlErrorNumber);
        await using (var context = new AenaonDbContext(options))
        {
            Assert.AreEqual(0, await context.Users.CountAsync(cancellationToken));
            Assert.AreEqual(0, await context.TenantMemberships.CountAsync(cancellationToken));
        }
        await ExecuteAsync(connection,
            "ALTER TABLE dbo.TenantMemberships DROP CONSTRAINT CK_TenantMemberships_AdminFixture;",
            cancellationToken);

        // Ακόμη και με ταυτόχρονες εκτελέσεις επιτρέπεται μία και μοναδική αρχικοποίηση.
        var results = await Task.WhenAll(
            service.ExecuteAsync(tenant.Id, "admin", "admin@example.invalid", FixturePassword,
                true, DatabaseName, cancellationToken),
            service.ExecuteAsync(tenant.Id, "second", "second@example.invalid", FixturePassword,
                true, DatabaseName, cancellationToken));
        Assert.AreEqual(1, results.Count(result => result.Status == InitialAdministratorStatus.Created));
        Assert.AreEqual(1, results.Count(result => result.Status == InitialAdministratorStatus.ExistingUsers));
        var created = results.Single(result => result.Status == InitialAdministratorStatus.Created);
        Assert.AreEqual(tenant.Id, tenant.Id);
        Assert.IsNotNull(created.UserId);

        await using var scope = provider.CreateAsyncScope();
        var checkContext = scope.ServiceProvider.GetRequiredService<AenaonDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AenaonUser>>();
        Assert.AreEqual(1, await checkContext.Users.CountAsync(cancellationToken));
        Assert.AreEqual(1, await checkContext.TenantMemberships.CountAsync(cancellationToken));
        var user = await checkContext.Users.SingleAsync(cancellationToken);
        Assert.AreEqual(created.UserId, user.Id);
        Assert.IsNotNull(user.PasswordHash);
        Assert.AreNotEqual(FixturePassword, user.PasswordHash);
        Assert.IsTrue(await manager.CheckPasswordAsync(user, FixturePassword));
        var membership = await checkContext.TenantMemberships.SingleAsync(cancellationToken);
        Assert.AreEqual(tenant.Id, membership.TenantId);
        Assert.AreEqual(user.Id, membership.UserId);
        Assert.AreEqual(TenantMembershipRole.Owner, membership.Role);

        var repeated = await service.ExecuteAsync(tenant.Id, "third", "third@example.invalid",
            FixturePassword, true, DatabaseName, cancellationToken);
        Assert.AreEqual(InitialAdministratorStatus.ExistingUsers, repeated.Status);
        Assert.AreEqual(1, await checkContext.Users.CountAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
