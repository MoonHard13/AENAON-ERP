using Aenaon.Api.Commands;
using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class TenantAccessCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid UserId = Guid.Parse("76d2c37f-2e26-4311-a129-e6073057c8dd");

    [TestMethod]
    public async Task AuthorizedOwner_SeesResultButNotPassword()
    {
        var verifier = new TenantCredentialFixture(true);
        var store = new TenantCommandStore { Role = TenantMembershipRole.Owner };
        using var output = new StringWriter();
        using var error = new StringWriter();

        int result = await TenantAccessCommand.ExecuteAsync(
            ["--username", "admin", "--tenant-id", TenantId.ToString(),
                "--permission", "Manage", "--confirm-database", "AenaonERP_Unit"],
            verifier, new TenantAuthorizationService(store), new TenantAccessSchemaFixture(),
            output, error, () => "SampleSecret!91", true, CancellationToken.None);

        Assert.AreEqual(0, result);
        Assert.Contains("επιτρέπεται", output.ToString());
        Assert.DoesNotContain("SampleSecret!91", output.ToString());
        Assert.AreEqual(1, store.Calls);
    }

    [TestMethod]
    public async Task MemberCannotManage()
    {
        var store = new TenantCommandStore { Role = TenantMembershipRole.Member };
        using var output = new StringWriter();
        using var error = new StringWriter();

        int result = await TenantAccessCommand.ExecuteAsync(
            ["--username", "admin", "--tenant-id", TenantId.ToString(),
                "--permission", "Manage", "--confirm-database", "AenaonERP_Unit"],
            new TenantCredentialFixture(true), new TenantAuthorizationService(store),
            new TenantAccessSchemaFixture(), output, error,
            () => "SampleSecret!91", true, CancellationToken.None);

        Assert.AreEqual(3, result);
        Assert.DoesNotContain(TenantId.ToString(), error.ToString());
    }

    [TestMethod]
    public async Task RejectedPassword_DoesNotCallAuthorization()
    {
        var store = new TenantCommandStore { Role = TenantMembershipRole.Owner };
        using var output = new StringWriter();
        using var error = new StringWriter();

        int result = await TenantAccessCommand.ExecuteAsync(
            ["--username", "admin", "--tenant-id", TenantId.ToString(),
                "--permission", "Read", "--confirm-database", "AenaonERP_Unit"],
            new TenantCredentialFixture(false), new TenantAuthorizationService(store),
            new TenantAccessSchemaFixture(), output, error,
            () => "incorrect", true, CancellationToken.None);

        Assert.AreEqual(2, result);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    [DataRow("missing-confirmation")]
    [DataRow("unknown-permission")]
    [DataRow("malformed-store")]
    [DataRow("unknown-option")]
    [DataRow("duplicate-option")]
    [DataRow("noninteractive")]
    public async Task InvalidArguments_DoNotReadPasswordOrAccessDatabase(string scenario)
    {
        string[] args = ["--username", "admin", "--tenant-id", TenantId.ToString(),
            "--permission", "Read", "--confirm-database", "AenaonERP_Unit"];
        args = scenario switch
        {
            "missing-confirmation" => ["--username", "admin", "--tenant-id", TenantId.ToString(), "--permission", "Read"],
            "unknown-permission" => ["--username", "admin", "--tenant-id", TenantId.ToString(),
                "--permission", "Delete", "--confirm-database", "AenaonERP_Unit"],
            "malformed-store" => [.. args, "--store-id", "oops"],
            "unknown-option" => [.. args, "--force", "true"],
            "duplicate-option" => [.. args, "--username", "other"],
            _ => args
        };
        var verifier = new TenantCredentialFixture(true);
        var store = new TenantCommandStore();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int result = await TenantAccessCommand.ExecuteAsync(
            args, verifier, new TenantAuthorizationService(store),
            new TenantAccessSchemaFixture(), output, error,
            () => throw new InvalidOperationException(), scenario != "noninteractive", CancellationToken.None);

        Assert.AreEqual(2, result);
        Assert.AreEqual(0, verifier.Calls);
        Assert.AreEqual(0, store.Calls);
    }

    [TestMethod]
    public async Task WrongDatabase_IsRejectedBeforeCredentials()
    {
        var verifier = new TenantCredentialFixture(true);
        var store = new TenantCommandStore();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int result = await TenantAccessCommand.ExecuteAsync(
            ["--username", "admin", "--tenant-id", TenantId.ToString(),
                "--permission", "Read", "--confirm-database", "OtherDatabase"],
            verifier, new TenantAuthorizationService(store), new TenantAccessSchemaFixture(),
            output, error, () => throw new InvalidOperationException(), true, CancellationToken.None);

        Assert.AreEqual(2, result);
        Assert.AreEqual(0, verifier.Calls);
        Assert.AreEqual(0, store.Calls);
    }
}

internal sealed class TenantCredentialFixture(bool success) : ICredentialVerifier
{
    public int Calls { get; private set; }

    public Task<CredentialVerificationResult> VerifyAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new CredentialVerificationResult(
            success ? CredentialVerificationStatus.Authenticated : CredentialVerificationStatus.Rejected,
            success ? Guid.Parse("76d2c37f-2e26-4311-a129-e6073057c8dd") : null));
    }
}

internal sealed class TenantCommandStore : ITenantAccessStore
{
    public TenantMembershipRole? Role { get; init; }
    public int Calls { get; private set; }

    public Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Role);
    }

    public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AccessibleTenant>>([]);
    }
}

internal sealed class TenantAccessSchemaFixture : IDatabaseSchemaManager
{
    public Task<DatabaseSchemaResult> ExecuteAsync(bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DatabaseSchemaResult(DatabaseSchemaStatus.Ready,
            "AenaonERP_Unit", ["Initial", "Identity"], []));
    }
}
