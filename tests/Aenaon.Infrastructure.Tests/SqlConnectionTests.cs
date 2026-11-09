using Aenaon.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Aenaon.Infrastructure.Tests;

[TestClass]
public sealed class SqlConnectionTests
{
    private const string Valid = "Server=example.invalid;Database=AenaonERP_Dev;User ID=unit-user;Password=unit-test-only;Encrypt=True;";

    [TestMethod]
    public void Policy_PreservesEncryptedConnectionAndDisablesCredentialRetention()
    {
        var builder = SqlConnectionPolicy.CreateBuilder(Valid + "Persist Security Info=True;Connect Timeout=90;", false);

        Assert.AreEqual(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
        Assert.IsFalse(builder.PersistSecurityInfo);
        Assert.AreEqual(5, builder.ConnectTimeout);
        Assert.AreEqual(0, builder.ConnectRetryCount);
    }

    [TestMethod]
    public void Policy_AllowsWindowsAuthentication()
    {
        var builder = SqlConnectionPolicy.CreateBuilder("Server=example.invalid;Database=AenaonERP_Dev;Integrated Security=True;Encrypt=True;", false);

        Assert.IsTrue(builder.IntegratedSecurity);
    }

    [TestMethod]
    [DataRow("master")]
    [DataRow("MODEL")]
    [DataRow("msdb")]
    [DataRow("tempdb")]
    [DataRow(" master ")]
    public void Policy_RejectsSystemDatabases(string database)
    {
        var candidate = new SqlConnectionStringBuilder(Valid) { InitialCatalog = database };

        Assert.ThrowsExactly<ArgumentException>(() => SqlConnectionPolicy.CreateBuilder(candidate.ConnectionString, false));
    }

    [TestMethod]
    [DataRow("Encrypt=False;")]
    [DataRow("TrustServerCertificate=True;")]
    public void Policy_RejectsInsecureProductionSettings(string setting)
    {
        Assert.ThrowsExactly<ArgumentException>(() => SqlConnectionPolicy.CreateBuilder(Valid + setting, false));
    }

    [TestMethod]
    public void Policy_AllowsDevelopmentCertificateExceptionWithoutDisablingEncryption()
    {
        var builder = SqlConnectionPolicy.CreateBuilder(Valid + "TrustServerCertificate=True;", true);

        Assert.IsTrue(builder.TrustServerCertificate);
        Assert.AreEqual(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
    }

    [TestMethod]
    [DataRow("Server=example.invalid;Integrated Security=True;")]
    [DataRow("Database=AenaonERP_Dev;Integrated Security=True;")]
    [DataRow("Server=example.invalid;Database=AenaonERP_Dev;User ID=unit-user;")]
    [DataRow("Server=example.invalid;Database=AenaonERP_Dev;Password=unit-test-only;")]
    public void Policy_RejectsIncompleteConfiguration(string candidate)
    {
        Assert.ThrowsExactly<ArgumentException>(() => SqlConnectionPolicy.CreateBuilder(candidate, false));
    }

    [TestMethod]
    public async Task Probe_ReportsMissingConfigurationWithoutConnecting()
    {
        var probe = new SqlServerConnectionProbe("", false, NullLogger<SqlServerConnectionProbe>.Instance);

        var result = await probe.CheckAsync();

        Assert.AreEqual(DatabaseConnectionStatus.NotConfigured, result.Status);
    }

    [TestMethod]
    public async Task Probe_RejectsInvalidConfigurationWithoutLoggingSecrets()
    {
        var logger = new CaptureLogger<SqlServerConnectionProbe>();
        var probe = new SqlServerConnectionProbe(Valid + "InvalidKeyword=unit-test-only;", false, logger);

        var result = await probe.CheckAsync();

        Assert.AreEqual(DatabaseConnectionStatus.InvalidConfiguration, result.Status);
        Assert.IsNotEmpty(logger.Messages);
        string logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("unit-test-only", logs);
        Assert.DoesNotContain("unit-user", logs);
        Assert.DoesNotContain("example.invalid", logs);
    }

    [TestMethod]
    public async Task Probe_HonorsCallerCancellationBeforeConnecting()
    {
        var probe = new SqlServerConnectionProbe(Valid, false, NullLogger<SqlServerConnectionProbe>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => probe.CheckAsync(cancellation.Token));
    }
}

internal sealed class CaptureLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}
