using System.Net;
using System.Net.Http.Json;
using Aenaon.Api.Contracts;
using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Ελέγχει το πραγματικό HTTP pipeline με απομονωμένες υπηρεσίες ταυτότητας και χωρίς SQL credentials.</summary>
[TestClass]
public sealed class SessionEndpointTests
{
    private static readonly Guid UserId = Guid.Parse("76d2c37f-2e26-4311-a129-e6073057c8dd");
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid StoreId = Guid.Parse("c697b9c3-e221-4156-b533-f54dee27d979");
    private const string FixturePassword = "Fixture-only-Secret!17";

    [TestMethod]
    public async Task AnonymousSession_IsRejectedAndCsrfTokenIsAvailable()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var me = await client.GetAsync("/api/auth/me");
        using var access = await client.GetAsync($"/api/auth/access?tenantId={TenantId}&permission=Read");
        using var csrf = await client.GetAsync("/api/auth/csrf");
        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, access.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, csrf.StatusCode);
        var token = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.IsNotNull(token);
        Assert.IsNotEmpty(token.Token);
        Assert.IsTrue(csrf.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.IsTrue(cookies.Any(cookie => cookie.Contains("Secure", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task Login_RequiresAntiforgeryEvenWithValidCredentials()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", FixturePassword));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, factory.Identity.LoginCalls);
    }

    [TestMethod]
    public async Task Login_RejectsWrongPasswordWithoutCookie()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        string csrf = await FetchCsrfAsync(client);
        using var response = await PostAsync(client, "/api/auth/login",
            new LoginRequest("admin", "incorrect-password"), csrf);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(1, factory.Identity.LoginCalls);
        Assert.IsFalse(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [TestMethod]
    public async Task Login_CookieAndMe_ExposeOnlyServerValidatedTenantMemberships()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var login = await LoginAsync(client);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        Assert.IsTrue(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        string ticket = Assert.ContainsSingle(cookies.Where(value =>
            value.StartsWith("__Host-AenaonERP.Session=", StringComparison.Ordinal)));
        Assert.IsTrue(ticket.Contains("secure", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(ticket.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(ticket.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        using var me = await client.GetAsync("/api/auth/me");
        Assert.AreEqual(HttpStatusCode.OK, me.StatusCode);
        CurrentSessionResponse? session = await me.Content.ReadFromJsonAsync<CurrentSessionResponse>();
        Assert.IsNotNull(session);
        Assert.AreEqual(UserId, session.UserId);
        Assert.AreEqual("admin", session.UserName);
        var tenant = Assert.ContainsSingle(session.Tenants);
        Assert.AreEqual(TenantId, tenant.TenantId);
        Assert.AreEqual(TenantMembershipRole.Owner, tenant.Role);
        Assert.IsFalse(me.Content.Headers.ToString().Contains(FixturePassword, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TenantAccess_UsesCookieUserAndBlocksForeignStore()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var login = await LoginAsync(client);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        using var granted = await client.GetAsync(
            $"/api/auth/access?tenantId={TenantId}&storeId={StoreId}&permission=Manage");
        using var foreign = await client.GetAsync(
            $"/api/auth/access?tenantId={TenantId}&storeId={Guid.NewGuid()}&permission=Read");
        using var otherTenant = await client.GetAsync(
            $"/api/auth/access?tenantId={Guid.NewGuid()}&permission=Read");
        using var unknownPermission = await client.GetAsync(
            $"/api/auth/access?tenantId={TenantId}&permission=Delete");

        Assert.AreEqual(HttpStatusCode.OK, granted.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, otherTenant.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, unknownPermission.StatusCode);
    }

    [TestMethod]
    public async Task Logout_RequiresFreshAuthenticatedCsrfTokenAndInvalidatesCookie()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        string anonymousToken = await FetchCsrfAsync(client);
        using var login = await PostAsync(client, "/api/auth/login",
            new LoginRequest("admin", FixturePassword), anonymousToken);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        using var stale = await PostAsync(client, "/api/auth/logout", null, anonymousToken);
        Assert.AreEqual(HttpStatusCode.BadRequest, stale.StatusCode);

        string authenticatedToken = await FetchCsrfAsync(client);
        using var logout = await PostAsync(client, "/api/auth/logout", null, authenticatedToken);
        Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [TestMethod]
    public async Task RevokedSecurityStamp_ImmediatelyRejectsAuthenticatedCookie()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var login = await LoginAsync(client);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        factory.Identity.Revoked = true;

        using var me = await client.GetAsync("/api/auth/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [TestMethod]
    public async Task AuthenticationRateLimiter_RejectsExcessRequests()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        for (int index = 0; index < 10; index++)
        {
            using var response = await client.GetAsync("/api/auth/csrf");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejected = await client.GetAsync("/api/auth/csrf");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [TestMethod]
    public async Task Production_DoesNotMapAuthEndpoints()
    {
        await using var factory = new SessionApiFactory("Production");
        using var client = factory.CreateHttpsClient();

        using var csrf = await client.GetAsync("/api/auth/csrf");
        using var me = await client.GetAsync("/api/auth/me");
        using var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", FixturePassword));

        Assert.AreEqual(HttpStatusCode.NotFound, csrf.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, me.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, login.StatusCode);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        string csrf = await FetchCsrfAsync(client);
        return await PostAsync(client, "/api/auth/login",
            new LoginRequest("admin", FixturePassword), csrf);
    }

    private static async Task<string> FetchCsrfAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf");
        Assert.IsNotNull(token);
        return token.Token;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string path, LoginRequest? payload, string csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Aenaon-CSRF", csrf);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await client.SendAsync(request);
    }
}

internal sealed class SessionApiFactory(string environment = "Development") : WebApplicationFactory<Program>
{
    public SessionIdentityFixture Identity { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISessionIdentityService>();
            services.AddSingleton<ISessionIdentityService>(Identity);
            services.RemoveAll<ITenantAccessStore>();
            services.AddSingleton<ITenantAccessStore, SessionTenancyFixture>();
            services.RemoveAll<ITenantStoreCatalog>();
            services.AddSingleton<ITenantStoreCatalog, SessionStoreCatalogFixture>();
        });
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }
}

internal sealed class SessionIdentityFixture : ISessionIdentityService
{
    private static readonly Guid UserId = Guid.Parse("76d2c37f-2e26-4311-a129-e6073057c8dd");
    public int LoginCalls { get; private set; }
    public bool Revoked { get; set; }

    public Task<SessionIdentity?> AuthenticateAsync(string? userName, string? password,
        CancellationToken cancellationToken = default)
    {
        LoginCalls++;
        SessionIdentity? identity = !Revoked && userName == "admin" && password == "Fixture-only-Secret!17"
            ? new SessionIdentity(UserId, "admin", "fixture-security-stamp")
            : null;
        return Task.FromResult(identity);
    }

    public Task<bool> ValidateSessionAsync(Guid userId, string? securityStamp,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!Revoked && userId == UserId && securityStamp == "fixture-security-stamp");
    }
}

internal sealed class SessionTenancyFixture : ITenantAccessStore
{
    private static readonly Guid UserId = Guid.Parse("76d2c37f-2e26-4311-a129-e6073057c8dd");
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid StoreId = Guid.Parse("c697b9c3-e221-4156-b533-f54dee27d979");

    public Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        TenantMembershipRole? role = userId == UserId && tenantId == TenantId
            ? TenantMembershipRole.Owner : null;
        return Task.FromResult(role);
    }

    public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(tenantId == TenantId && storeId == StoreId);
    }

    public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AccessibleTenant> tenants = userId == UserId
            ? [new AccessibleTenant(TenantId, "Aenaon Demo", TenantMembershipRole.Owner)]
            : [];
        return Task.FromResult(tenants);
    }
}

internal sealed class SessionStoreCatalogFixture : ITenantStoreCatalog
{
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid StoreId = Guid.Parse("c697b9c3-e221-4156-b533-f54dee27d979");

    public Task<IReadOnlyList<TenantStoreSummary>> ListStoresAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TenantStoreSummary> result = tenantId == TenantId
            ? [new TenantStoreSummary(StoreId, TenantId, "MAIN", "Κεντρικό")]
            : [];
        return Task.FromResult(result);
    }
}
