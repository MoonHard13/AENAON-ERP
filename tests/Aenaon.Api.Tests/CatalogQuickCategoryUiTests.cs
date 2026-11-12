using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Πλήρης δοκιμή δημιουργίας κατηγορίας από μη ολοκληρωμένη φόρμα είδους.</summary>
[TestClass]
public sealed class CatalogQuickCategoryUiTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task CreateCategoryFromProduct_RestoresUnsavedFieldsAndSelectsNewCategory()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string productForm = await client.GetStringAsync(
            $"/catalog/manage?view=products&tenantId={Tenant:D}");
        Assert.Contains("formaction=\"/ui/catalog/new-category\"", productForm);
        Assert.Contains("formnovalidate", productForm);

        // Η νέα κατηγορία ανοίγει χωρίς να δημιουργήσει κατά λάθος το προϊόν.
        using var start = await client.PostAsync("/ui/catalog/new-category",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(productForm),
                ["tenantId"] = Tenant.ToString("D"),
                ["view"] = "products",
                ["action"] = "create",
                ["codeMode"] = "manual",
                ["code"] = "SKU-900",
                ["name"] = "Αποθηκευμένα στοιχεία προϊόντος",
                ["tracksStock"] = "true"
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, start.StatusCode);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);
        string categoryUrl = start.Headers.Location?.OriginalString ?? "";
        Assert.Contains("/catalog/manage?view=categories&tenantId=", categoryUrl);
        Assert.Contains("&returnDraft=", categoryUrl);

        string categoryForm = WebUtility.HtmlDecode(await client.GetStringAsync(categoryUrl));
        Assert.Contains("name=\"returnDraft\"", categoryForm);
        Assert.Contains("Πίσω στην καταχώρηση", categoryForm);
        Assert.Contains("χωρίς να χαθούν", categoryForm);

        string returnToken = HiddenValue(categoryForm, "returnDraft");
        using var created = await client.PostAsync("/ui/catalog/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(categoryForm),
                ["tenantId"] = Tenant.ToString("D"),
                ["view"] = "categories",
                ["action"] = "create",
                ["codeMode"] = "auto",
                ["name"] = "ΝΕΑ ΚΑΤΗΓΟΡΙΑ",
                ["returnDraft"] = returnToken,
                ["saveMode"] = "close"
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, created.StatusCode);
        Assert.AreEqual(1, factory.CatalogWriter.ApplyCalls);
        Assert.AreEqual(CatalogManagedEntity.Category, factory.CatalogWriter.LastCommand?.Entity);

        string productReturn = created.Headers.Location?.OriginalString ?? "";
        Assert.Contains("/catalog/manage?view=products", productReturn);
        Assert.Contains("&draft=", productReturn);

        string restored = WebUtility.HtmlDecode(await client.GetStringAsync(productReturn));
        Assert.Contains("SKU-900", restored);
        Assert.Contains("Αποθηκευμένα στοιχεία προϊόντος", restored);
        Assert.Contains("name=\"tracksStock\"", restored);
        Assert.IsNotNull(factory.CatalogWriter.CreatedCategoryId);
        Assert.Contains(factory.CatalogWriter.CreatedCategoryId.Value.ToString("D"), restored);
        Assert.Contains("ΝΕΑ ΚΑΤΗΓΟΡΙΑ", restored);
    }

    [TestMethod]
    public async Task QuickCreateCancel_ReturnsToSameServiceWithoutSaving()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string serviceForm = await client.GetStringAsync(
            $"/catalog/manage?view=services&tenantId={Tenant:D}");
        using var start = await client.PostAsync("/ui/catalog/new-category",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(serviceForm),
                ["tenantId"] = Tenant.ToString("D"),
                ["view"] = "services",
                ["name"] = "Υπηρεσία σε εξέλιξη",
                ["codeMode"] = "auto"
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, start.StatusCode);
        string categoryForm = WebUtility.HtmlDecode(await client.GetStringAsync(
            start.Headers.Location?.OriginalString));
        Assert.Contains("Πίσω στην καταχώρηση", categoryForm);
        string returnDraft = HiddenValue(categoryForm, "returnDraft");
        string expectedReturn = $"/catalog/manage?view=services&tenantId={Tenant:D}&draft={returnDraft}";
        Assert.Contains(expectedReturn, categoryForm);

        string returned = WebUtility.HtmlDecode(await client.GetStringAsync(expectedReturn));
        Assert.Contains("Υπηρεσία σε εξέλιξη", returned);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);
    }

    [TestMethod]
    public async Task QuickCreate_MemberAndForeignTenantAreRejected()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string html = await client.GetStringAsync("/catalog/manage?view=products");
        string token = Token(html);
        factory.Access.GrantedRole = TenantMembershipRole.Member;

        using var member = await client.PostAsync("/ui/catalog/new-category",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["tenantId"] = Tenant.ToString("D"),
                ["view"] = "products",
                ["name"] = "Μη επιτρεπτό"
            }));
        Assert.AreEqual(HttpStatusCode.Forbidden, member.StatusCode);
        factory.Access.GrantedRole = TenantMembershipRole.Owner;

        using var foreign = await client.PostAsync("/ui/catalog/new-category",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["tenantId"] = Guid.NewGuid().ToString("D"),
                ["view"] = "products",
                ["name"] = "Ξένη επιχείρηση"
            }));
        Assert.AreEqual(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);
    }

    [TestMethod]
    public async Task QuickCreate_IsProtectedByAntiforgery()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        using var response = await client.PostAsync("/ui/catalog/new-category",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["tenantId"] = Tenant.ToString("D"),
                ["view"] = "products"
            }));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, factory.CatalogWriter.ApplyCalls);
    }

    [TestMethod]
    public async Task EditorStyles_UseAvailableWidthAndPreserveResponsiveGrid()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        string css = await client.GetStringAsync("/css/aenaon-backoffice.css");

        Assert.Contains(".catalog-manage-page.dashboard__body", css);
        Assert.Contains("max-width: none", css);
        Assert.Contains("width: 100%", css);
        Assert.Contains(".catalog-editor__category-toolbar", css);
        Assert.Contains(".catalog-editor__quick-create:focus-visible", css);
        Assert.Contains("@media (max-width: 800px)", css);
        Assert.DoesNotContain(".catalog-editor { max-width: 950px", css);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string login = await client.GetStringAsync("/login");
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = Token(login)
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string Token(string html) => HiddenValue(html, "__RequestVerificationToken");

    private static string HiddenValue(string html, string field)
    {
        Match match = Regex.Match(html,
            $"name=\"{Regex.Escape(field)}\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success, $"Το πεδίο {field} δεν εμφανίστηκε.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
