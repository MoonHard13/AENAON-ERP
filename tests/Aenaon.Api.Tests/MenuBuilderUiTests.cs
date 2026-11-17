using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Application.Menus;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>SSR/CSRF/Owner/Member έλεγχοι με δοκιμαστικά δεδομένα, όχι πραγματικό browser acceptance.</summary>
[TestClass]
public sealed class MenuBuilderUiTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task Anonymous_CannotOpenDraftBuilderOrPost()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var page = await client.GetAsync("/menus");
        Assert.IsTrue(page.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
        using var post = await client.PostAsync("/ui/menus/draft", new FormUrlEncodedContent([]));
        Assert.IsTrue(post.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);
    }

    [TestMethod]
    public async Task Owner_SeesThreeColumnsAndNativeDraftForms()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus?tenantId={Tenant:D}&menuId={SessionMenuDraftFixture.MenuId:D}"));
        Assert.Contains("menu-builder__column--categories", html);
        Assert.Contains("menu-builder__column--items", html);
        Assert.Contains("menu-builder__column--catalog", html);
        Assert.Contains("menu-builder__mobile-tabs", html);
        Assert.Contains("Δημιουργία μενού με είδη ERP", html);
        Assert.Contains("DRAFT ONLY", html);
        Assert.Contains("name=\"expectedRevision\"", html);
        Assert.Contains("name=\"requestId\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("action=\"/ui/menus/draft\"", html);
        Assert.Contains("Προσθήκη", html);
        Assert.DoesNotContain("name=\"unitPrice\"", html);
    }

    [TestMethod]
    public async Task Member_SeesMenuButCannotWrite()
    {
        await using var factory = new SessionApiFactory();
        factory.Access.GrantedRole = TenantMembershipRole.Member;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus?tenantId={Tenant:D}"));
        Assert.Contains("Μόνο ανάγνωση", html);
        Assert.DoesNotContain("action=\"/ui/menus/draft\"", html);

        using var response = await PostAsync(client, Token(html), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["requestId"] = Guid.NewGuid().ToString("D"),
            ["action"] = "create-menu",
            ["code"] = "MAIN",
            ["name"] = "Κυρίως"
        });
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);
    }

    [TestMethod]
    public async Task Owner_PostRequiresCsrfAndRejectsCrossTenant()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var noCsrf = await client.PostAsync("/ui/menus/draft",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["tenantId"] = Tenant.ToString("D"),
                ["requestId"] = Guid.NewGuid().ToString("D"),
                ["action"] = "create-menu",
                ["code"] = "MAIN",
                ["name"] = "Κυρίως"
            }));
        Assert.AreEqual(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);

        string token = Token(await client.GetStringAsync("/menus"));
        using var denied = await PostAsync(client, token, new()
        {
            ["tenantId"] = Guid.NewGuid().ToString("D"),
            ["requestId"] = Guid.NewGuid().ToString("D"),
            ["action"] = "create-menu",
            ["code"] = "OTHER",
            ["name"] = "Ξένο"
        });
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);
    }

    [TestMethod]
    public async Task Owner_CanWriteDraftAndGetsSafeRedirect()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string token = Token(await client.GetStringAsync($"/menus?tenantId={Tenant:D}"));
        using var saved = await PostAsync(client, token, new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["requestId"] = Guid.NewGuid().ToString("D"),
            ["action"] = "add-category",
            ["menuId"] = SessionMenuDraftFixture.MenuId.ToString("D"),
            ["expectedRevision"] = "1",
            ["name"] = "Νέα"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("/menus?tenantId=", saved.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(1, factory.MenuDrafts.ApplyCalls);
        Assert.AreEqual(MenuDraftAction.AddCategory, factory.MenuDrafts.LastCommand?.Action);
        Assert.AreEqual(Tenant, factory.MenuDrafts.LastTenant);
    }

    [TestMethod]
    public async Task ExistingEmptyDraft_OffersOwnerImportForCategoriesAndItems()
    {
        await using var factory = new SessionApiFactory();
        factory.MenuDrafts.EmptyDraft = true;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus?tenantId={Tenant:D}&menuId={SessionMenuDraftFixture.MenuId:D}"));
        Assert.Contains("Φόρτωση κατηγοριών και ειδών ERP", html);
        Assert.Contains("value=\"import-catalog\"", html);
        Assert.Contains("name=\"expectedRevision\"", html);

        using var saved = await PostAsync(client, Token(html), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["requestId"] = Guid.NewGuid().ToString("D"),
            ["action"] = "import-catalog",
            ["menuId"] = SessionMenuDraftFixture.MenuId.ToString("D"),
            ["expectedRevision"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.AreEqual(MenuDraftAction.ImportCatalogContents, factory.MenuDrafts.LastCommand?.Action);
        Assert.AreEqual(1, factory.MenuDrafts.ApplyCalls);
    }

    [TestMethod]
    public async Task Member_CannotImportCatalogIntoEmptyDraft()
    {
        await using var factory = new SessionApiFactory();
        factory.MenuDrafts.EmptyDraft = true;
        factory.Access.GrantedRole = TenantMembershipRole.Member;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus?tenantId={Tenant:D}&menuId={SessionMenuDraftFixture.MenuId:D}"));
        Assert.DoesNotContain("value=\"import-catalog\"", html);

        using var response = await PostAsync(client, Token(html), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["requestId"] = Guid.NewGuid().ToString("D"),
            ["action"] = "import-catalog",
            ["menuId"] = SessionMenuDraftFixture.MenuId.ToString("D"),
            ["expectedRevision"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string html = await client.GetStringAsync("/login");
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = Token(html)
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string Token(string html)
    {
        Match match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token,
        Dictionary<string, string> values)
    {
        values["__RequestVerificationToken"] = token;
        return client.PostAsync("/ui/menus/draft", new FormUrlEncodedContent(values));
    }
}
