using Aenaon.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Εκτελείται μόνο σε ρητά ενεργοποιημένο, απομονωμένο SQL container του CI.</summary>
[TestClass]
public sealed class SqlServerLiveTests
{
    [TestMethod]
    [TestCategory("SqlServer")]
    public async Task Sql2022_VerifiesConnectivityMigrationsAndTenancyPersistence()
    {
        string? value = Environment.GetEnvironmentVariable("AENAON_TEST_SQL_CONNECTION");
        if (Environment.GetEnvironmentVariable("AENAON_RUN_SQL_INTEGRATION") != "1" || string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive("Ο έλεγχος SQL ενεργοποιείται μόνο στο απομονωμένο CI container.");
        }

        var server = new SqlConnectionStringBuilder(value);
        Assert.AreEqual("127.0.0.1,14333", server.DataSource);
        Assert.AreEqual("master", server.InitialCatalog);
        server.ConnectTimeout = 2;
        server.ConnectRetryCount = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using var connection = new SqlConnection(server.ConnectionString);

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await connection.OpenAsync(timeout.Token);
                break;
            }
            catch (SqlException) when (attempt < 40)
            {
                SqlConnection.ClearPool(connection);
                await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE DATABASE [AenaonERP_CI] COLLATE Greek_CI_AS;";
            command.CommandTimeout = 30;
            await command.ExecuteNonQueryAsync(timeout.Token);
        }

        var application = new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog = "AenaonERP_CI" };
        var logger = new CaptureLogger<SqlServerConnectionProbe>();
        var probe = new SqlServerConnectionProbe(application.ConnectionString, true, logger);
        var result = await probe.CheckAsync(timeout.Token);
        Assert.AreEqual(DatabaseConnectionStatus.Connected, result.Status);
        Assert.AreEqual(160, result.CompatibilityLevel);

        await SqlServerSchemaAssertions.RunAsync(application.ConnectionString, timeout.Token);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE DATABASE [AenaonERP_Startup_CI] COLLATE Greek_CI_AS;";
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
        var startup = new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog = "AenaonERP_Startup_CI" };
        await SqlServerInitialBusinessAssertions.RunAsync(startup.ConnectionString, timeout.Token);

        application.Password = "invalid-unit-test-password";
        var rejected = await new SqlServerConnectionProbe(application.ConnectionString, true, logger).CheckAsync(timeout.Token);
        Assert.AreEqual(DatabaseConnectionStatus.Unavailable, rejected.Status);
        Assert.AreEqual(18456, rejected.SqlErrorNumber);
        string logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain(server.Password, logs);
        Assert.DoesNotContain(application.Password, logs);
    }
}
