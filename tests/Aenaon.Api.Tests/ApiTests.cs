using System.Net;
using System.Net.Http.Json;
using Aenaon.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Aenaon.Api.Tests;

/// <summary>Ελέγχει το πραγματικό pipeline HTTP χωρίς εξωτερικό server ή δεδομένα παραγωγής.</summary>
[TestClass]
public sealed class ApiTests
{
    [TestMethod]
    public async Task Health_ReturnsHealthy()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/health");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", await response.Content.ReadAsStringAsync());
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task Info_ReturnsPublicApplicationName()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        var info = await client.GetFromJsonAsync<ApiInfoResponse>("/api/info");
        Assert.IsNotNull(info);
        Assert.AreEqual("Aenaon ERP API", info.Name);
    }

    [TestMethod]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/does-not-exist");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Info_RejectsPostRequests()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsync("/api/info", null);
        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [TestMethod]
    public async Task Http_RedirectsToHttpsWithoutServingInfo()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });
        using var response = await client.GetAsync("/api/info");
        Assert.AreEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.AreEqual("https://localhost:7140/api/info", response.Headers.Location?.AbsoluteUri);
    }
}

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services => services.PostConfigure<HttpsRedirectionOptions>(
            options => options.HttpsPort = 7140));
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }
}
