using Aenaon.Api.Commands;
using Aenaon.Application.Identity;
using Aenaon.Infrastructure.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class VerifyCredentialsCommandTests
{
    [TestMethod]
    public async Task ValidCredentials_ReportsOnlyUserIdWithoutPassword()
    {
        var verifier = new CredentialVerifierFixture(CredentialVerificationStatus.Authenticated);
        var schema = new CredentialSchemaFixture();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await VerifyCredentialsCommand.ExecuteAsync(
            ["--username", "admin", "--confirm-database", "AenaonERP_Unit"],
            verifier, schema, output, error, () => "StrongPassword!15", true, CancellationToken.None);

        Assert.AreEqual(0, code);
        Assert.AreEqual(1, verifier.Calls);
        Assert.Contains("UserId:", output.ToString());
        Assert.DoesNotContain("StrongPassword!15", output.ToString());
        Assert.DoesNotContain("StrongPassword!15", error.ToString());
    }

    [TestMethod]
    public async Task WrongPassword_UsesGenericMessage()
    {
        var verifier = new CredentialVerifierFixture(CredentialVerificationStatus.Rejected);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await VerifyCredentialsCommand.ExecuteAsync(
            ["--username", "admin", "--confirm-database", "AenaonERP_Unit"],
            verifier, new CredentialSchemaFixture(), output, error,
            () => "wrong-password", true, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(1, verifier.Calls);
        Assert.DoesNotContain("wrong-password", error.ToString());
    }

    [TestMethod]
    [DataRow("bad-database")]
    [DataRow("bad-mode")]
    [DataRow("non-interactive")]
    public async Task UnsafeInput_DoesNotVerifyCredentials(string mode)
    {
        var verifier = new CredentialVerifierFixture(CredentialVerificationStatus.Authenticated);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = mode == "bad-mode"
            ? new[] { "--username", "admin", "--password", "bad" }
            : new[] { "--username", "admin", "--confirm-database", "AenaonERP_Unit" };

        int code = await VerifyCredentialsCommand.ExecuteAsync(args, verifier,
            new CredentialSchemaFixture(),
            output, error, () => throw new InvalidOperationException(),
            mode != "non-interactive", CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, verifier.Calls);
    }

    [TestMethod]
    public async Task WrongDatabase_IsRejectedBeforePasswordRead()
    {
        var verifier = new CredentialVerifierFixture(CredentialVerificationStatus.Authenticated);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await VerifyCredentialsCommand.ExecuteAsync(
            ["--username", "admin", "--confirm-database", "AenaonERP_Other"],
            verifier, new CredentialSchemaFixture(), output, error,
            () => throw new InvalidOperationException(), true, CancellationToken.None);

        Assert.AreEqual(2, code);
        Assert.AreEqual(0, verifier.Calls);
    }
}

internal sealed class CredentialVerifierFixture(CredentialVerificationStatus status) : ICredentialVerifier
{
    public int Calls { get; private set; }

    public Task<CredentialVerificationResult> VerifyAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new CredentialVerificationResult(status,
            status == CredentialVerificationStatus.Authenticated ? Guid.NewGuid() : null));
    }
}

internal sealed class CredentialSchemaFixture : IDatabaseSchemaManager
{
    public Task<DatabaseSchemaResult> ExecuteAsync(bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DatabaseSchemaResult(DatabaseSchemaStatus.Ready,
            "AenaonERP_Unit", ["Initial", "Identity"], []));
    }
}
