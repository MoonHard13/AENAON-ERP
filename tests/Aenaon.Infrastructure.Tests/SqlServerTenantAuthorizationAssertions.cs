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

/// <summary>Επαληθεύει απομόνωση επιχειρήσεων/καταστημάτων με πραγματικό SQL Server 2022 σε CI.</summary>
internal static class SqlServerTenantAuthorizationAssertions
{
    private const string DatabaseName = "AenaonERP_Authorization_CI";
    private const string Password = "Aenaon-Authorization-CI!71";

    public static async Task RunAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        Assert.AreEqual("127.0.0.1,14333", connection.DataSource);
        Assert.AreEqual(DatabaseName, connection.InitialCatalog);

        var schema = new DatabaseSchemaManager(connectionString, true, NullLogger<DatabaseSchemaManager>.Instance);
        Assert.AreEqual(DatabaseSchemaStatus.Updated,
            (await schema.ExecuteAsync(true, DatabaseName, cancellationToken)).Status);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new AenaonDbContext(AenaonDbContextOptions.Create(connectionString, true)));
        services.AddIdentityCore<AenaonUser>(settings => settings.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AenaonDbContext>();
        services.AddScoped<ITenantAccessStore, SqlTenantAccessStore>();
        services.AddScoped<TenantAuthorizationService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AenaonDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AenaonUser>>();
        var authorization = scope.ServiceProvider.GetRequiredService<TenantAuthorizationService>();

        var tenantA = Tenant.Create("Alpha Company");
        Store alphaStore = tenantA.AddStore("MAIN", "Alpha Main");
        Store alphaSecond = tenantA.AddStore("S2", "Alpha Second");
        var tenantB = Tenant.Create("Beta Company");
        Store betaStore = tenantB.AddStore("MAIN", "Beta Main");
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync(cancellationToken);

        var owner = new AenaonUser { UserName = "owner", Email = "owner@example.invalid" };
        var member = new AenaonUser { UserName = "member", Email = "member@example.invalid" };
        var betaOwner = new AenaonUser { UserName = "beta", Email = "beta@example.invalid" };
        var outsider = new AenaonUser { UserName = "outsider", Email = "outsider@example.invalid" };
        foreach (AenaonUser user in new[] { owner, member, betaOwner, outsider })
        {
            IdentityResult created = await userManager.CreateAsync(user, Password);
            Assert.IsTrue(created.Succeeded, string.Join(',', created.Errors.Select(error => error.Code)));
        }

        db.TenantMemberships.AddRange(
            TenantMembership.Create(tenantA.Id, owner.Id, TenantMembershipRole.Owner),
            TenantMembership.Create(tenantA.Id, member.Id, TenantMembershipRole.Member),
            TenantMembership.Create(tenantB.Id, betaOwner.Id, TenantMembershipRole.Owner));
        await db.SaveChangesAsync(cancellationToken);

        await AssertAccessAsync(authorization, owner.Id, tenantA.Id, alphaStore.Id,
            TenantPermission.Read, TenantAccessStatus.Granted, cancellationToken);
        await AssertAccessAsync(authorization, owner.Id, tenantA.Id, alphaSecond.Id,
            TenantPermission.Manage, TenantAccessStatus.Granted, cancellationToken);
        await AssertAccessAsync(authorization, member.Id, tenantA.Id, alphaStore.Id,
            TenantPermission.Read, TenantAccessStatus.Granted, cancellationToken);
        await AssertAccessAsync(authorization, member.Id, tenantA.Id, alphaStore.Id,
            TenantPermission.Manage, TenantAccessStatus.Denied, cancellationToken);
        await AssertAccessAsync(authorization, owner.Id, tenantA.Id, betaStore.Id,
            TenantPermission.Read, TenantAccessStatus.Denied, cancellationToken);
        await AssertAccessAsync(authorization, betaOwner.Id, tenantA.Id, alphaStore.Id,
            TenantPermission.Read, TenantAccessStatus.Denied, cancellationToken);
        await AssertAccessAsync(authorization, outsider.Id, tenantA.Id, alphaStore.Id,
            TenantPermission.Read, TenantAccessStatus.Denied, cancellationToken);
        await AssertAccessAsync(authorization, Guid.NewGuid(), tenantA.Id, null,
            TenantPermission.Read, TenantAccessStatus.Denied, cancellationToken);
        await AssertAccessAsync(authorization, owner.Id, Guid.NewGuid(), null,
            TenantPermission.Read, TenantAccessStatus.Denied, cancellationToken);
        await AssertAccessAsync(authorization, owner.Id, tenantA.Id, Guid.NewGuid(),
            TenantPermission.Read, TenantAccessStatus.Denied, cancellationToken);

        var ownerTenants = await authorization.GetAccessibleTenantsAsync(owner.Id, cancellationToken);
        var onlyAlpha = Assert.ContainsSingle(ownerTenants);
        Assert.AreEqual(tenantA.Id, onlyAlpha.TenantId);
        Assert.AreEqual(tenantA.Name, onlyAlpha.Name);
        Assert.AreEqual(TenantMembershipRole.Owner, onlyAlpha.Role);

        var betaTenants = await authorization.GetAccessibleTenantsAsync(betaOwner.Id, cancellationToken);
        var onlyBeta = Assert.ContainsSingle(betaTenants);
        Assert.AreEqual(tenantB.Id, onlyBeta.TenantId);
        Assert.AreEqual(TenantMembershipRole.Owner, onlyBeta.Role);
        Assert.HasCount(0, await authorization.GetAccessibleTenantsAsync(outsider.Id, cancellationToken));

        // Οι read-only έλεγχοι δεν τροποποιούν συμμετοχές ή άλλα δεδομένα.
        Assert.AreEqual(2, await db.Tenants.CountAsync(cancellationToken));
        Assert.AreEqual(3, await db.Stores.CountAsync(cancellationToken));
        Assert.AreEqual(4, await db.Users.CountAsync(cancellationToken));
        Assert.AreEqual(3, await db.TenantMemberships.CountAsync(cancellationToken));
    }

    private static async Task AssertAccessAsync(TenantAuthorizationService service,
        Guid userId, Guid tenantId, Guid? storeId, TenantPermission permission,
        TenantAccessStatus expected, CancellationToken cancellationToken)
    {
        TenantAccessResult result = await service.CheckAsync(userId, tenantId, storeId, permission, cancellationToken);
        Assert.AreEqual(expected, result.Status);
        if (expected == TenantAccessStatus.Denied)
        {
            Assert.IsNull(result.Role);
        }
    }
}
