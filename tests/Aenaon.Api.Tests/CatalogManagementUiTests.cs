using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Application.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>HTTP παλινδρομικοί έλεγχοι πραγματικού SSR POST χωρίς παραγωγική SQL.</summary>
[TestClass]
public sealed class CatalogManagementUiTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task Owner_CanSeeCreateAndEditForms_AndCatalogLinks()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string catalog = WebUtility.HtmlDecode(await client.GetStringAsync("/catalog"));
        Assert.Contains("/catalog/manage?view=products", catalog);
        Assert.Contains("Νέο προϊόν", catalog);
        Assert.Contains("Επεξεργασία", catalog);

        string form = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&tenantId={Tenant:D}"));
        Assert.Contains("Νέο προϊόν", form);
        Assert.Contains("name=\"categoryId\"", form);
        Assert.Contains("name=\"tracksStock\"", form);
        Assert.Contains("action=\"/ui/catalog/save\"", form);
        Assert.Contains("name=\"__RequestVerificationToken\"", form);
        Assert.AreEqual(1, factory.CatalogWriter.DetailCalls);
    }

    [TestMethod]
    public async Task Member_CanReadButCannotManageCatalog()
    {
        await using var factory = new SessionApiFactory();
        factory.Access.GrantedRole = Aenaon.Domain.Tenancy.TenantMembershipRole.Member;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string catalog = WebUtility.HtmlDecode(await client.GetStringAsync("/catalog"));
        Assert.Contains("Μόνο ανάγνωση", catalog);
        Assert.DoesNotContain("class=\"catalog-create\"", catalog);

        string edit = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=categories&tenantId={Tenant:D}"));
        Assert.Contains("Δεν έχετε δικαίωμα διαχείρισης", edit);
        Assert.AreEqual(0, factory.CatalogWriter.DetailCalls);

        string token = ExtractToken(edit);
        using var forbidden = await PostAsync(client, token, new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "categories",
            ["action"] = "create",
            ["code"] = "FOOD",
            ["name"] = "Φαγητά"
        });
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);
    }

    [TestMethod]
    public async Task CreateCategory_RequiresCsrfAndAuthenticatedUser()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var missing = await client.PostAsync("/ui/catalog/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "categories",
                ["action"] = "create",
            ["code"] = "FOOD",
            ["name"] = "Φαγητά"
            }));
        Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);

        string form = await client.GetStringAsync("/catalog/manage?view=categories");
        using var saved = await PostAsync(client, ExtractToken(form), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "categories",
            ["action"] = "create",
            ["code"] = "FOOD",
            ["name"] = "Φαγητά"
        });

        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.AreEqual("/catalog?view=categories&saved=1", saved.Headers.Location?.OriginalString);
        Assert.AreEqual(1, factory.CatalogWriter.ApplyCalls);
        Assert.AreEqual(CatalogChangeAction.Create, factory.CatalogWriter.LastCommand!.Action);
        Assert.AreEqual(CatalogManagedEntity.Category, factory.CatalogWriter.LastCommand.Entity);
    }

    [TestMethod]
    public async Task ForeignTenant_IsDeniedBeforeAnyWriteOrDetailRead()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string form = await client.GetStringAsync("/catalog/manage?view=categories");
        using var forbidden = await PostAsync(client, ExtractToken(form), new()
        {
            ["tenantId"] = Guid.NewGuid().ToString("D"),
            ["view"] = "categories",
            ["action"] = "create",
            ["code"] = "FOOD",
            ["name"] = "Φαγητά"
        });
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&tenantId={Guid.NewGuid():D}"));
        Assert.Contains("Δεν έχετε δικαίωμα διαχείρισης", html);
        Assert.AreEqual(1, factory.CatalogWriter.DetailCalls);
    }

    [TestMethod]
    public async Task ItemEdit_PreservesKindAndUsesServerAuthorizedRecord()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string edit = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&id={SessionCatalogWriteFixture.ItemId:D}&tenantId={Tenant:D}"));
        Assert.Contains("SKU001", edit);
        Assert.Contains("Πίτσα", edit);

        using var saved = await PostAsync(client, ExtractToken(edit), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "products",
            ["action"] = "update",
            ["id"] = SessionCatalogWriteFixture.ItemId.ToString("D"),
            ["categoryId"] = SessionCatalogWriteFixture.CategoryId.ToString("D"),
            ["code"] = "SKU002",
            ["name"] = "Πίτσα Μεγάλη",
            ["tracksStock"] = "true"
        });

        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.AreEqual(CatalogManagedEntity.Product, factory.CatalogWriter.LastCommand!.Entity);
        Assert.AreEqual(CatalogChangeAction.Update, factory.CatalogWriter.LastCommand.Action);
        Assert.IsTrue(factory.CatalogWriter.LastCommand.TracksStock);
    }

    [TestMethod]
    public async Task InvalidServiceStockCannotReachStore_AndDuplicateRedirectsSafely()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string form = await client.GetStringAsync("/catalog/manage?view=services");

        using var service = await PostAsync(client, ExtractToken(form), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "services",
            ["action"] = "create",
            ["code"] = "SERV001",
            ["name"] = "Service",
            ["categoryId"] = SessionCatalogWriteFixture.CategoryId.ToString("D"),
            ["tracksStock"] = "true"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, service.StatusCode);
        Assert.AreEqual(CatalogManagedEntity.Service, factory.CatalogWriter.LastCommand!.Entity);
        Assert.IsFalse(factory.CatalogWriter.LastCommand.TracksStock);

        using var duplicate = await PostAsync(client, ExtractToken(form), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "categories",
            ["action"] = "create",
            ["code"] = "EXISTS",
            ["name"] = "Ήδη υπάρχει"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, duplicate.StatusCode);
        Assert.Contains("error=duplicate", duplicate.Headers.Location?.OriginalString ?? "");
    }

    [TestMethod]
    public async Task UnknownItemAndInvalidRoute_ExposeNoForeignRecord()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string missing = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&id={Guid.NewGuid():D}&tenantId={Tenant:D}"));
        Assert.Contains("δεν υπάρχει ή δεν ανήκει", missing);

        string wrong = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/catalog/manage?view=unknown"));
        Assert.Contains("δεν υπάρχει ή δεν ανήκει", wrong);
    }

    [TestMethod]
    public async Task Production_CatalogWriteUiAndEndpointRemainUnavailable()
    {
        await using var factory = new SessionApiFactory("Production");
        using var client = factory.CreateHttpsClient();
        using var page = await client.GetAsync("/catalog/manage?view=categories");
        using var post = await client.PostAsync("/ui/catalog/save",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.AreEqual(HttpStatusCode.NotFound, page.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, post.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string login = await client.GetStringAsync("/login");
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = ExtractToken(login)
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string ExtractToken(string html)
    {
        Match match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client,
        string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return client.PostAsync("/ui/catalog/save", new FormUrlEncodedContent(fields));
    }
}
