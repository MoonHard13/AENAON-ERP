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
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
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

    [TestMethod]
    public async Task Dashboard_ExplicitUnknownTenant_DoesNotSilentlySwitchToAnotherTenant()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var response = await client.GetAsync($"/dashboard?tenantId={Guid.NewGuid():D}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Η επιλεγμένη επιχείρηση δεν είναι διαθέσιμη", html);
        Assert.DoesNotContain("ΕΝΕΡΓΟ ΠΛΑΙΣΙΟ ΕΡΓΑΣΙΑΣ", html);
        Assert.DoesNotContain("Κεντρικό", html);
        Assert.Contains("Aenaon Demo", html);
    }

    [TestMethod]
    public async Task Dashboard_ExplicitUnknownStore_DoesNotMarkAnyStoreActive()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        var tenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
        using var response = await client.GetAsync(
            $"/dashboard?tenantId={tenantId:D}&storeId={Guid.NewGuid():D}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Το επιλεγμένο κατάστημα δεν είναι διαθέσιμο", html);
        Assert.DoesNotContain("ΕΝΕΡΓΟ ΠΛΑΙΣΙΟ ΕΡΓΑΣΙΑΣ", html);
        Assert.Contains("Κεντρικό", html);
        Assert.DoesNotContain("store-entry store-entry--active", html);
    }

    [TestMethod]
    public async Task Dashboard_SelectedStore_IsVisibleAndUsesExplicitTenantStoreUrl()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        var tenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
        var storeId = Guid.Parse("c697b9c3-e221-4156-b533-f54dee27d979");

        using var response = await client.GetAsync(
            $"/dashboard?tenantId={tenantId:D}&storeId={storeId:D}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ΕΝΕΡΓΟ ΠΛΑΙΣΙΟ ΕΡΓΑΣΙΑΣ", html);
        Assert.Contains("Aenaon Demo / Κεντρικό", html);
        Assert.Contains($"name=\"storeId\" value=\"{storeId:D}\"", html);
        Assert.Contains("store-entry store-entry--active", html);
    }

    [TestMethod]
    public async Task LoggedInUser_VisitingWelcome_IsRedirectedToDashboard()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var anonymous = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, anonymous.StatusCode);

        await LoginAsync(client);
        using var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/dashboard", response.Headers.Location?.OriginalString);
    }

    [TestMethod]
    public async Task ExcessHtmlLoginRequests_DisplayRateLimitGuidance()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        for (int index = 0; index < 10; index++)
        {
            using var response = await client.PostAsync("/ui/session/login",
                new FormUrlEncodedContent([]));
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var limited = await client.PostAsync("/ui/session/login", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.Redirect, limited.StatusCode);
        Assert.AreEqual("/login?error=rate-limited", limited.Headers.Location?.OriginalString);

        using var notice = await client.GetAsync("/login?error=rate-limited");
        Assert.AreEqual(HttpStatusCode.OK, notice.StatusCode);
        Assert.Contains("πολλές προσπάθειες σύνδεσης", await notice.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task LoginPage_LoadsThemePreferenceBeforeCss()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        string html = await client.GetStringAsync("/login");
        int themeIndex = html.IndexOf("aenaon-theme-init.js", StringComparison.Ordinal);
        int cssIndex = html.IndexOf("aenaon.css", StringComparison.Ordinal);
        Assert.IsTrue(themeIndex >= 0);
        Assert.IsTrue(cssIndex > themeIndex);
    }

    [TestMethod]
    public async Task LoginPage_UsesHashedStaticAssetsAndBoundedSvgDimensions()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        string html = await client.GetStringAsync("/login");
        Assert.Contains("width=\"20\" height=\"20\" viewBox=\"0 0 24 24\"", html);

        foreach (string path in new[]
        {
            "/css/aenaon.css",
            "/js/aenaon.js",
            "/js/aenaon-theme-init.js",
            "/images/aenaon-favicon.png"
        })
        {
            Match match = Regex.Match(html,
                Regex.Escape(path) + @"\?v=([0-9a-f]{16})",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            Assert.IsTrue(match.Success, $"Λείπει fingerprint για {path}.");

            byte[] fileContent = await client.GetByteArrayAsync(path + "?v=" + match.Groups[1].Value);
            string expectedVersion = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(fileContent))
                .ToLowerInvariant()[..16];
            Assert.AreEqual(expectedVersion, match.Groups[1].Value);
        }
    }

    [TestMethod]
    public async Task Dashboard_RendersAllIconsWithBoundedIntrinsicDimensions()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = await client.GetStringAsync("/dashboard");
        int icons = Regex.Matches(html, "<svg class=\"app-icon\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Count;
        int bounded = Regex.Matches(html,
            "<svg class=\"app-icon\"[^>]*width=\"20\" height=\"20\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Count;

        Assert.IsTrue(icons > 5, "Το Dashboard πρέπει να περιλαμβάνει πραγματικά SVG icons.");
        Assert.AreEqual(icons, bounded, "Κάθε SVG πρέπει να έχει intrinsic διαστάσεις 20x20.");
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
