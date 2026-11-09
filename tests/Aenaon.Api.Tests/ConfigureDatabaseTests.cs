using System.Diagnostics;
using Aenaon.Api.Commands;
using Aenaon.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class ConfigureDatabaseTests
{
    private const string FixturePassword = "unit-only;\"quoted\";δοκιμή\\end";

    [TestMethod]
    public async Task Configure_PreservesCredentialsWithoutPrintingThem()
    {
        var writer = new FakeSecretsWriter();
        using var input = new StringReader("unit-user\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ConfigureDatabaseCommand.ExecuteAsync(
            ["--server", "example.invalid", "--trust-server-certificate"], "unit-id", writer,
            input, output, error, () => FixturePassword);

        Assert.AreEqual(0, code);
        Assert.AreEqual("unit-id", writer.SecretsId);
        var settings = new SqlConnectionStringBuilder(writer.ConnectionString);
        Assert.AreEqual(FixturePassword, settings.Password);
        Assert.AreEqual("example.invalid", settings.DataSource);
        Assert.AreEqual("AenaonERP_Dev", settings.InitialCatalog);
        Assert.AreEqual(SqlConnectionEncryptOption.Mandatory, settings.Encrypt);
        Assert.IsTrue(settings.TrustServerCertificate);
        Assert.IsFalse(settings.PersistSecurityInfo);
        Assert.DoesNotContain(FixturePassword, output.ToString() + error);
        Assert.DoesNotContain("unit-user", output.ToString() + error);
    }

    [TestMethod]
    [DataRow("missing-server")]
    [DataRow("missing-value")]
    [DataRow("unknown-option")]
    [DataRow("system-database")]
    [DataRow("empty-login")]
    [DataRow("empty-password")]
    public async Task Configure_RejectsInvalidInputWithoutSaving(string scenario)
    {
        string[] args = scenario switch
        {
            "missing-server" => [],
            "missing-value" => ["--server"],
            "unknown-option" => ["--server", "example.invalid", "--password", FixturePassword],
            "system-database" => ["--server", "example.invalid", "--database", "master"],
            _ => ["--server", "example.invalid"]
        };
        var writer = new FakeSecretsWriter();
        using var input = new StringReader(scenario == "empty-login" ? "\n" : "unit-user\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ConfigureDatabaseCommand.ExecuteAsync(args, "unit-id", writer, input, output, error,
            () => scenario == "empty-password" ? "" : FixturePassword);

        Assert.AreEqual(2, code);
        Assert.IsNull(writer.ConnectionString);
        Assert.DoesNotContain(FixturePassword, output.ToString() + error);
    }

    [TestMethod]
    public async Task Configure_CanCancelWithoutSaving()
    {
        var writer = new FakeSecretsWriter();
        using var input = new StringReader("unit-user\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ConfigureDatabaseCommand.ExecuteAsync(["--server", "example.invalid"], "unit-id",
            writer, input, output, error, () => null);

        Assert.AreEqual(130, code);
        Assert.IsNull(writer.ConnectionString);
    }

    [TestMethod]
    public async Task Configure_ReportsSaveFailureWithoutLeakingCredentials()
    {
        using var input = new StringReader("unit-user\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await ConfigureDatabaseCommand.ExecuteAsync(["--server", "example.invalid"], "unit-id",
            new FakeSecretsWriter { Succeeds = false }, input, output, error, () => FixturePassword);

        Assert.AreEqual(2, code);
        Assert.DoesNotContain(FixturePassword, output.ToString() + error);
        Assert.DoesNotContain("unit-user", output.ToString() + error);
    }

    [TestMethod]
    public async Task UserSecretsWriter_RoundTripsThroughOfficialSdkAndConfigurationProvider()
    {
        // Το τυχαίο αναγνωριστικό απομονώνει το test από τα secrets οποιασδήποτε εφαρμογής.
        string secretsId = $"aenaon-unit-{Guid.NewGuid():N}";
        string connectionString = SqlConnectionConfiguration.ForDevelopment("example.invalid", "AenaonERP_Dev",
            "unit-user", FixturePassword, trustServerCertificate: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            bool saved = await new DotnetUserSecretsWriter().SaveAsync(secretsId, connectionString, timeout.Token);

            Assert.IsTrue(saved, "Η εγγραφή με το εγκατεστημένο .NET SDK απέτυχε.");
            using var configuration = new ConfigurationManager();
            configuration.AddUserSecrets(secretsId);
            Assert.AreEqual(connectionString, configuration.GetConnectionString("Aenaon"));
        }
        finally
        {
            await ClearFixtureSecretsAsync(secretsId);
        }
    }

    private static async Task ClearFixtureSecretsAsync(string secretsId)
    {
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[] { "user-secrets", "clear", "--id", secretsId })
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Δεν εκκινήθηκε το SDK.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        Assert.AreEqual(0, process.ExitCode, "Δεν καθαρίστηκαν τα προσωρινά test secrets.");
    }
}

internal sealed class FakeSecretsWriter : IUserSecretsWriter
{
    public bool Succeeds { get; init; } = true;
    public string? SecretsId { get; private set; }
    public string? ConnectionString { get; private set; }

    public Task<bool> SaveAsync(string secretsId, string connectionString, CancellationToken cancellationToken)
    {
        SecretsId = secretsId;
        ConnectionString = connectionString;
        return Task.FromResult(Succeeds);
    }
}
