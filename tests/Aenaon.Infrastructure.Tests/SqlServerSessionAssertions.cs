using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
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

/// <summary>Ελέγχει σε πραγματική απομονωμένη SQL βάση ανάκληση συνεδρίας μετά από αλλαγή stamp ή membership.</summary>
internal static class SqlServerSessionAssertions
{
    private const string DatabaseName = "AenaonERP_Session_CI";
    private const string Password = "Aenaon-Session-CI!79";

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
        }).AddEntityFrameworkStores<AenaonDbContext>();
        services.AddScoped<ITenantAccessStore, SqlTenantAccessStore>();
        services.AddScoped<ICredentialVerifier>(provider => new IdentityCredentialVerifier(
            provider.GetRequiredService<UserManager<AenaonUser>>(),
            NullLogger<IdentityCredentialVerifier>.Instance));
        services.AddScoped<ISessionIdentityService>(provider => new IdentitySessionService(
            provider.GetRequiredService<ICredentialVerifier>(),
            provider.GetRequiredService<UserManager<AenaonUser>>(),
            provider.GetRequiredService<ITenantAccessStore>(),
            NullLogger<IdentitySessionService>.Instance));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AenaonDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AenaonUser>>();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionIdentityService>();

        var tenant = Tenant.Create("Session CI Tenant");
        tenant.AddStore("MAIN", "Κεντρικό");
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(cancellationToken);

        var user = new AenaonUser { UserName = "admin", Email = "admin@example.invalid" };
        var outsider = new AenaonUser { UserName = "outsider", Email = "outsider@example.invalid" };
        Assert.IsTrue((await users.CreateAsync(user, Password)).Succeeded);
        Assert.IsTrue((await users.CreateAsync(outsider, Password)).Succeeded);
        context.TenantMemberships.Add(TenantMembership.Create(tenant.Id, user.Id, TenantMembershipRole.Owner));
        await context.SaveChangesAsync(cancellationToken);

        Assert.IsNull(await sessions.AuthenticateAsync("admin", "wrong-password", cancellationToken));
        Assert.IsNull(await sessions.AuthenticateAsync("outsider", Password, cancellationToken));

        SessionIdentity? authenticated = await sessions.AuthenticateAsync("admin", Password, cancellationToken);
        Assert.IsNotNull(authenticated);
        Assert.AreEqual(user.Id, authenticated.UserId);
        Assert.AreEqual("admin", authenticated.UserName);
        Assert.IsTrue(await sessions.ValidateSessionAsync(user.Id, authenticated.SecurityStamp, cancellationToken));

        Assert.IsTrue((await users.UpdateSecurityStampAsync(user)).Succeeded);
        Assert.IsFalse(await sessions.ValidateSessionAsync(user.Id, authenticated.SecurityStamp, cancellationToken));
        SessionIdentity? refreshed = await sessions.AuthenticateAsync("admin", Password, cancellationToken);
        Assert.IsNotNull(refreshed);
        Assert.AreNotEqual(authenticated.SecurityStamp, refreshed.SecurityStamp);

        context.TenantMemberships.Remove(await context.TenantMemberships.SingleAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        Assert.IsFalse(await sessions.ValidateSessionAsync(user.Id, refreshed.SecurityStamp, cancellationToken));
        Assert.IsNull(await sessions.AuthenticateAsync("admin", Password, cancellationToken));
    }
}
