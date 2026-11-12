using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
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
        Assert.DoesNotContain("catalog-create", catalog);

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
        Assert.Contains($"/catalog?view=categories&tenantId={Tenant:D}&saved=1",
            saved.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(1, factory.CatalogWriter.ApplyCalls);
        Assert.AreEqual(CatalogChangeAction.Create, factory.CatalogWriter.LastCommand!.Action);
        Assert.AreEqual(CatalogManagedEntity.Category, factory.CatalogWriter.LastCommand.Entity);
    }

    [TestMethod]
    public async Task NativeSubmit_DoesNotDuplicateSaveMode_AndOldIdenticalValuesRemainCompatible()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string form = await client.GetStringAsync("/catalog/manage?view=categories");
        string token = ExtractToken(form);
        string javascript = await client.GetStringAsync("/js/aenaon.js");

        // Το κουμπί συμπεριλαμβάνεται ήδη στο native POST: δεν εισάγουμε δεύτερο hidden input.
        Assert.Contains("name=\"saveMode\" value=\"close\"", form);
        Assert.DoesNotContain("field.name = \"saveMode\"", javascript);

        // Συμβατότητα με παλιό ανοιχτό tab, που υπέβαλλε δύο φορές την ίδια επιλογή.
        KeyValuePair<string, string>[] values =
        [
            new("__RequestVerificationToken", token),
            new("tenantId", Tenant.ToString("D")),
            new("view", "categories"),
            new("action", "create"),
            new("codeMode", "auto"),
            new("name", "ΦΑΓΗΤΑ"),
            new("saveMode", "close"),
            new("saveMode", "close")
        ];

        using var accepted = await client.PostAsync("/ui/catalog/save",
            new FormUrlEncodedContent(values));
        Assert.AreEqual(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Contains("/catalog?view=categories", accepted.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(1, factory.CatalogWriter.ApplyCalls);

        // Αν οι δύο εντολές διαφέρουν, απορρίπτουμε την αμφίσημη υποβολή.
        values[^1] = new("saveMode", "new");
        using var ambiguous = await client.PostAsync("/ui/catalog/save",
            new FormUrlEncodedContent(values));
        Assert.AreEqual(HttpStatusCode.BadRequest, ambiguous.StatusCode);
        Assert.AreEqual(1, factory.CatalogWriter.ApplyCalls);
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
        string redirect = duplicate.Headers.Location?.OriginalString ?? "";
        Assert.Contains("&draft=", redirect);
        string errorPage = WebUtility.HtmlDecode(await client.GetStringAsync(redirect));
        Assert.Contains("Ο κωδικός χρησιμοποιείται ήδη", errorPage);
        Assert.Contains("EXISTS", errorPage);
        Assert.Contains("Ήδη υπάρχει", errorPage);
    }

    [TestMethod]
    public async Task AutomaticCodeAndSaveNew_UseServerGeneratedRequestId()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string form = await client.GetStringAsync("/catalog/manage?view=categories");
        Assert.Contains("data-code-mode=\"auto\"", form);
        Assert.Contains("name=\"parentCategoryId\"", form);
        Match request = Regex.Match(form, "name=\"requestId\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(request.Success);

        using var posted = await PostAsync(client, ExtractToken(form), new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["view"] = "categories",
            ["action"] = "create",
            ["codeMode"] = "auto",
            ["name"] = "Αυτόματη κατηγορία",
            ["requestId"] = request.Groups[1].Value,
            ["saveMode"] = "new"
        });

        Assert.AreEqual(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Contains("/catalog/manage?view=categories", posted.Headers.Location?.OriginalString ?? "");
        Assert.IsTrue(factory.CatalogWriter.LastCommand!.AutoCode);
        Assert.IsNotNull(factory.CatalogWriter.LastCommand.RequestId);
    }

    [TestMethod]
    public async Task CategoryLookup_IsBoundedAndOwnerOnly()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var response = await client.GetAsync(
            $"/ui/catalog/categories?tenantId={Tenant:D}&search=FOOD");
        string body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Τρόφιμα / Φαγητά", body);

        factory.Access.GrantedRole = Aenaon.Domain.Tenancy.TenantMembershipRole.Member;
        using var denied = await client.GetAsync(
            $"/ui/catalog/categories?tenantId={Tenant:D}&search=FOOD");
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [TestMethod]
    public async Task LazyCategoryTree_AllowsMemberReadButNeverForeignTenant()
    {
        await using var factory = new SessionApiFactory();
        Guid root = SessionCatalogWriteFixture.CategoryId;
        factory.Catalog.Categories[0] = factory.Catalog.Categories[0] with { HasChildren = true };
        factory.Catalog.Categories.Add(new CatalogCategorySummary(
            Guid.NewGuid(), "CHILD", "Υποκατηγορία", true, root, "Φαγητά / Υποκατηγορία"));
        factory.Access.GrantedRole = Aenaon.Domain.Tenancy.TenantMembershipRole.Member;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string catalog = await client.GetStringAsync("/catalog?view=categories");
        Assert.Contains("data-category-branch", catalog);
        using var rows = await client.GetAsync(
            $"/ui/catalog/children?tenantId={Tenant:D}&parentId={root:D}");
        Assert.AreEqual(HttpStatusCode.OK, rows.StatusCode);
        Assert.Contains("CHILD", await rows.Content.ReadAsStringAsync());
        using var foreign = await client.GetAsync(
            $"/ui/catalog/children?tenantId={Guid.NewGuid():D}&parentId={root:D}");
        Assert.AreEqual(HttpStatusCode.Forbidden, foreign.StatusCode);
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
