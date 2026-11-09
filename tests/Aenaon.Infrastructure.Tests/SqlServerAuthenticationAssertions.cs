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

/// <summary>Ελέγχει πραγματικό Identity verification, lockout και αλλαγή δοκιμαστικού email σε προσωρινό SQL.</summary>
internal static class SqlServerAuthenticationAssertions
{
    private const string FixturePassword = "Aenaon-CI-Pass!92";
    private const string DatabaseName = "AenaonERP_Auth_CI";

    public static async Task RunAsync(string connectionString, CancellationToken cancellationToken)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        Assert.AreEqual("127.0.0.1,14333", settings.DataSource);
        Assert.AreEqual(DatabaseName, settings.InitialCatalog);

        var schema = new DatabaseSchemaManager(connectionString, true, NullLogger<DatabaseSchemaManager>.Instance);
        Assert.AreEqual(DatabaseSchemaStatus.Updated,
            (await schema.ExecuteAsync(true, DatabaseName, cancellationToken)).Status);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new AenaonDbContext(AenaonDbContextOptions.Create(connectionString, true)));
        services.AddIdentityCore<AenaonUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Password.RequiredLength = 14;
            options.Password.RequiredUniqueChars = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<AenaonDbContext>();
        services.AddScoped<ICredentialVerifier>(provider => new IdentityCredentialVerifier(
            provider.GetRequiredService<UserManager<AenaonUser>>(),
            NullLogger<IdentityCredentialVerifier>.Instance));
        services.AddScoped<IAdministratorEmailChangeStore>(provider => new SqlAdministratorEmailChangeStore(
            connectionString, true, schema, provider.GetRequiredService<AenaonDbContext>(),
            provider.GetRequiredService<UserManager<AenaonUser>>(),
            provider.GetRequiredService<ICredentialVerifier>(),
            NullLogger<SqlAdministratorEmailChangeStore>.Instance));
        services.AddScoped<AdministratorEmailChangeService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AenaonDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AenaonUser>>();
        var verifier = scope.ServiceProvider.GetRequiredService<ICredentialVerifier>();
        var emailChange = scope.ServiceProvider.GetRequiredService<AdministratorEmailChangeService>();
        var tenant = Tenant.Create("Authentication CI Tenant");
        tenant.AddStore("MAIN", "Κεντρικό");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        var account = new AenaonUser { UserName = "admin", Email = "your-email@example.com" };
        IdentityResult created = await manager.CreateAsync(account, FixturePassword);
        Assert.IsTrue(created.Succeeded, string.Join(',', created.Errors.Select(error => error.Code)));
        db.TenantMemberships.Add(TenantMembership.Create(tenant.Id, account.Id, TenantMembershipRole.Owner));
        await db.SaveChangesAsync(cancellationToken);
        Assert.IsTrue(await manager.GetLockoutEnabledAsync(account));

        // Οι άγνωστοι χρήστες και οι λανθασμένοι κωδικοί δεν αποκαλύπτουν αναγνωριστικά.
        var unknown = await verifier.VerifyAsync("absent", FixturePassword, cancellationToken);
        Assert.AreEqual(CredentialVerificationStatus.Rejected, unknown.Status);
        Assert.IsNull(unknown.UserId);
        var valid = await verifier.VerifyAsync("admin", FixturePassword, cancellationToken);
        Assert.AreEqual(CredentialVerificationStatus.Authenticated, valid.Status);
        Assert.AreEqual(account.Id, valid.UserId);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var rejected = await verifier.VerifyAsync("admin", "incorrect-password", cancellationToken);
            Assert.AreEqual(CredentialVerificationStatus.Rejected, rejected.Status);
            Assert.IsNull(rejected.UserId);
        }
        Assert.IsTrue(await manager.IsLockedOutAsync(account));
        var locked = await verifier.VerifyAsync("admin", FixturePassword, cancellationToken);
        Assert.AreEqual(CredentialVerificationStatus.Rejected, locked.Status);

        // Μόνο για το fixture: λήξη lockout ώστε να εξεταστεί η ανάκτηση και η αλλαγή email.
        Assert.IsTrue((await manager.SetLockoutEndDateAsync(account, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        var recovered = await verifier.VerifyAsync("admin", FixturePassword, cancellationToken);
        Assert.AreEqual(CredentialVerificationStatus.Authenticated, recovered.Status);
        Assert.AreEqual(0, await manager.GetAccessFailedCountAsync(account));

        var preview = await emailChange.ExecuteAsync(tenant.Id, account.Id, "owner@example.invalid",
            null, false, null, cancellationToken);
        Assert.AreEqual(AdministratorEmailChangeStatus.Ready, preview.Status);
        Assert.AreEqual("your-email@example.com", account.Email);

        var wrongDatabase = await emailChange.ExecuteAsync(tenant.Id, account.Id, "owner@example.invalid",
            FixturePassword, true, "WrongDatabase", cancellationToken);
        Assert.AreEqual(AdministratorEmailChangeStatus.InvalidConfiguration, wrongDatabase.Status);

        var wrongPassword = await emailChange.ExecuteAsync(tenant.Id, account.Id, "owner@example.invalid",
            "incorrect-password", true, DatabaseName, cancellationToken);
        Assert.AreEqual(AdministratorEmailChangeStatus.InvalidCredentials, wrongPassword.Status);
        Assert.AreEqual("your-email@example.com", account.Email);

        string? oldSecurityStamp = account.SecurityStamp;
        var changed = await emailChange.ExecuteAsync(tenant.Id, account.Id, "owner@example.invalid",
            FixturePassword, true, DatabaseName, cancellationToken);
        Assert.AreEqual(AdministratorEmailChangeStatus.Updated, changed.Status);
        Assert.AreEqual("owner@example.invalid", account.Email);
        Assert.AreEqual("OWNER@EXAMPLE.INVALID", account.NormalizedEmail);
        Assert.IsFalse(account.EmailConfirmed);
        Assert.AreNotEqual(oldSecurityStamp, account.SecurityStamp);

        var repeat = await emailChange.ExecuteAsync(tenant.Id, account.Id, "another@example.invalid",
            FixturePassword, true, DatabaseName, cancellationToken);
        Assert.AreEqual(AdministratorEmailChangeStatus.TargetNotEligible, repeat.Status);
        Assert.AreEqual("owner@example.invalid", account.Email);
        Assert.AreEqual(1, await db.Users.CountAsync(cancellationToken));
        Assert.AreEqual(1, await db.TenantMemberships.CountAsync(cancellationToken));
        Assert.AreEqual(TenantMembershipRole.Owner,
            (await db.TenantMemberships.SingleAsync(cancellationToken)).Role);
    }
}
