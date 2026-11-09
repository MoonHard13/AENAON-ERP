using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>HTTP έλεγχοι της Blazor SSR σύνδεσης και προστατευμένου Dashboard, χωρίς παραγωγική βάση.</summary>
[TestClass]
public sealed class BlazorUiTests
{
    [TestMethod]
    public async Task LoginPage_ShowsBrandingAndCsrfHiddenField()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/login");
        string html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Σύνδεση στο Aenaon", html);
        Assert.Contains("/images/aenaon-logo-dark.webp", html);
        Assert.Contains("/images/aenaon-logo-light.webp", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("name=\"username\"", html);
        Assert.Contains("name=\"password\"", html);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
    }

    [TestMethod]
    public async Task AnonymousDashboard_RedirectsToLogin()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/dashboard");
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/login", response.Headers.Location?.OriginalString);
    }

    [TestMethod]
    public async Task UiLogin_MissingCsrfIsRejected()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17"
            }));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, factory.Identity.LoginCalls);
    }

    [TestMethod]
    public async Task UiLogin_WrongPasswordRedirectsToGenericError()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        string csrf = await RequestUiTokenAsync(client, "/login");

        using var response = await SubmitFormAsync(client, "/ui/session/login",
            ["username", "admin", "password", "not-the-correct-secret", "__RequestVerificationToken", csrf]);
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/login?error=invalid", response.Headers.Location?.OriginalString);
        Assert.AreEqual(1, factory.Identity.LoginCalls);
    }

    [TestMethod]
    public async Task UiLogin_AuthorizedDashboardShowsOnlyAssignedTenantAndStore()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var response = await client.GetAsync("/dashboard");
        string html = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Aenaon Demo", html);
        Assert.Contains("Κεντρικό", html);
        Assert.Contains("MAIN", html);
        Assert.Contains("admin", html);
        Assert.DoesNotContain("Fixture-only-Secret!17", html);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);

        using var foreign = await client.GetAsync($"/dashboard?tenantId={Guid.NewGuid():D}");
        string foreignHtml = await foreign.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Contains("Aenaon Demo", foreignHtml);
    }

    [TestMethod]
    public async Task UiLogout_CsrfProtectedAndEndsSession()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var missingCsrf = await client.PostAsync("/ui/session/logout", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        string csrf = await RequestUiTokenAsync(client, "/dashboard");
        using var logout = await SubmitFormAsync(client, "/ui/session/logout",
            ["__RequestVerificationToken", csrf]);
        Assert.AreEqual(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.AreEqual("/login", logout.Headers.Location?.OriginalString);

        using var dashboard = await client.GetAsync("/dashboard");
        Assert.AreEqual(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.AreEqual("/login", dashboard.Headers.Location?.OriginalString);
    }

    [TestMethod]
    public async Task Production_DoesNotExposeUiPagesOrForms()
    {
        await using var factory = new SessionApiFactory("Production");
        using var client = factory.CreateHttpsClient();

        using var login = await client.GetAsync("/login");
        using var dashboard = await client.GetAsync("/dashboard");
        using var post = await client.PostAsync("/ui/session/login", new FormUrlEncodedContent([]));

        Assert.AreEqual(HttpStatusCode.NotFound, login.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, dashboard.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, post.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string csrf = await RequestUiTokenAsync(client, "/login");
        using var login = await SubmitFormAsync(client, "/ui/session/login",
            ["username", "admin", "password", "Fixture-only-Secret!17", "__RequestVerificationToken", csrf]);
        Assert.AreEqual(HttpStatusCode.Redirect, login.StatusCode);
        Assert.AreEqual("/dashboard", login.Headers.Location?.OriginalString);
    }

    private static async Task<string> RequestUiTokenAsync(HttpClient client, string path)
    {
        string html = await client.GetStringAsync(path);
        Match match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success, "Η φόρμα πρέπει να περιλαμβάνει antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> SubmitFormAsync(HttpClient client, string path,
        string[] parts)
    {
        Assert.IsTrue(parts.Length % 2 == 0);
        var data = new List<KeyValuePair<string, string>>();
        for (int index = 0; index < parts.Length; index += 2)
        {
            data.Add(new KeyValuePair<string, string>(parts[index], parts[index + 1]));
        }
        return client.PostAsync(path, new FormUrlEncodedContent(data));
    }
}
