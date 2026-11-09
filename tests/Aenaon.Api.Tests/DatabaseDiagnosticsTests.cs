using System.Net;
using Aenaon.Api.Commands;
using Aenaon.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

[TestClass]
public sealed class DatabaseDiagnosticsTests
{
    [TestMethod]
    public async Task Liveness_DoesNotCallDatabaseProbe()
    {
        var probe = new FakeProbe(DatabaseConnectionStatus.Unavailable);
        await using var factory = new DatabaseApiFactory("Development", probe);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, probe.Calls);
    }

    [TestMethod]
    [DataRow(DatabaseConnectionStatus.Connected, HttpStatusCode.OK, "Healthy")]
    [DataRow(DatabaseConnectionStatus.NotConfigured, HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    [DataRow(DatabaseConnectionStatus.Unavailable, HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task DevelopmentDatabaseHealth_ReportsStatusWithoutDetails(DatabaseConnectionStatus status,
        HttpStatusCode expectedCode, string expectedBody)
    {
        var probe = new FakeProbe(status);
        await using var factory = new DatabaseApiFactory("Development", probe);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health/database");

        Assert.AreEqual(expectedCode, response.StatusCode);
        Assert.AreEqual(expectedBody, await response.Content.ReadAsStringAsync());
        Assert.AreEqual(1, probe.Calls);
    }

    [TestMethod]
    public async Task Production_DoesNotExposeDatabaseDiagnostics()
    {
        var probe = new FakeProbe(DatabaseConnectionStatus.Connected);
        await using var factory = new DatabaseApiFactory("Production", probe);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health/database");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, probe.Calls);
    }

    [TestMethod]
    [DataRow(DatabaseConnectionStatus.Connected, 0)]
    [DataRow(DatabaseConnectionStatus.NotConfigured, 2)]
    [DataRow(DatabaseConnectionStatus.InvalidConfiguration, 2)]
    [DataRow(DatabaseConnectionStatus.Unavailable, 1)]
    [DataRow(DatabaseConnectionStatus.TimedOut, 1)]
    public async Task Command_ReturnsMeaningfulExitCodes(DatabaseConnectionStatus status, int expectedCode)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = await DatabaseCheckCommand.ExecuteAsync(new FakeProbe(status), output, error, CancellationToken.None);

        Assert.AreEqual(expectedCode, code);
        if (status == DatabaseConnectionStatus.Connected)
        {
            Assert.Contains("160", output.ToString());
        }
        else
        {
            Assert.IsNotEmpty(error.ToString());
        }
    }

    [TestMethod]
    public async Task Command_HandlesCancellation()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        int code = await DatabaseCheckCommand.ExecuteAsync(new FakeProbe(DatabaseConnectionStatus.Connected),
            output, error, cancellation.Token);

        Assert.AreEqual(130, code);
    }
}

internal sealed class FakeProbe(DatabaseConnectionStatus status) : IDatabaseConnectionProbe
{
    public int Calls { get; private set; }

    public Task<DatabaseConnectionResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(new DatabaseConnectionResult(status, status == DatabaseConnectionStatus.Connected ? 160 : null));
    }
}

internal sealed class DatabaseApiFactory(string environment, IDatabaseConnectionProbe probe) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseConnectionProbe>();
            services.AddSingleton(probe);
        });
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }
}
