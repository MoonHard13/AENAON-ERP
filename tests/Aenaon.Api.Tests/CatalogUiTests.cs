using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>HTTP παλινδρομικοί έλεγχοι Blazor Back Office χωρίς πραγματική βάση δεδομένων.</summary>
[TestClass]
public sealed class CatalogUiTests
{
    private static readonly Guid TenantId = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task CatalogMobile_UsesReadableCardsAndNativeSortWithoutDuplicateFields()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string products = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog?tenantId={TenantId:D}&view=products&sort=name&desc=true"));

        Assert.Contains("mobile-items-table", products);
        Assert.Contains("mobile-card-title", products);
        Assert.Contains("data-label=\"Barcode\"", products);
        Assert.Contains("data-label=\"Κατηγορία\"", products);
        Assert.Contains("data-label=\"Ενέργειες\"", products);
        Assert.Contains("id=\"catalog-mobile-sort\"", products);
        Assert.Contains("id=\"catalog-mobile-direction\"", products);
        Assert.Contains("value=\"name\" selected", products);
        Assert.Contains("value=\"true\" selected", products);
        Assert.DoesNotContain("desktop-sort-param", products);
        Assert.AreEqual(1, Regex.Matches(products, "name=\"sort\"").Count);
        Assert.AreEqual(1, Regex.Matches(products, "name=\"desc\"").Count);

        string categories = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog?tenantId={TenantId:D}&view=categories"));
        Assert.Contains("mobile-category-table", categories);
        Assert.Contains("data-label=\"Κατάσταση\"", categories);
        Assert.DoesNotContain("option value=\"barcode\"", categories);
    }

    [TestMethod]
    public async Task AnonymousCatalog_RedirectsToLoginWithoutCatalogReads()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/catalog");
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/login", response.Headers.Location?.OriginalString);
        Assert.AreEqual(0, factory.Catalog.ItemReads);
    }

    [TestMethod]
    public async Task Catalog_StreamingHasBlazorBootstrapAndJavascriptIsServed()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = await client.GetStringAsync("/catalog");

        // Αποτρέπουμε την επανεμφάνιση ατέρμονου loading όταν λείπει ο Blazor bootstrap loader.
        Match script = Regex.Match(html,
            """<script\s+src="([^"]*blazor\.web[^"]*\.js(?:\?[^"]*)?)" """.TrimEnd(),
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(script.Success, "Το streaming SSR απαιτεί το blazor.web.js στο App.razor.");
        Assert.Contains("data-enhance-nav=\"false\"", html);

        using var response = await client.GetAsync(WebUtility.HtmlDecode(script.Groups[1].Value));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            "Ο Blazor browser bootstrap πρέπει να διατίθεται ως static web asset.");
    }

    [TestMethod]
    public async Task Catalog_DefaultViewShowsProductsOnlyWithSharedNavigation()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var response = await client.GetAsync("/catalog");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1 id=\"catalog-list-title\">Προϊόντα</h1>", html);
        Assert.Contains("SKU001", html);
        Assert.Contains("Πίτσα", html);
        Assert.DoesNotContain("SERV001", html);
        Assert.Contains("Aenaon Demo", html);
        Assert.Contains("Κεντρικό (MAIN)", html);
        Assert.Contains("class=\"workspace-indicator\"", html);
        Assert.DoesNotContain("class=\"catalog-context\"", html);
        Assert.Contains("Νέο προϊόν", html);
        Assert.Contains("action=\"/ui/session/logout\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("href=\"/catalog", html);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
        Assert.AreEqual(CatalogItemKind.Product, factory.Catalog.LastQuery!.Kind);
        Assert.AreEqual(TenantId, factory.Catalog.LastTenant);
    }

    [TestMethod]
    public async Task Catalog_ServicesAndCategoriesAreDistinctServerSideQueries()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var services = await client.GetAsync("/catalog?view=services");
        string servicesHtml = WebUtility.HtmlDecode(await services.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.OK, services.StatusCode);
        Assert.Contains("SERV001", servicesHtml);
        Assert.DoesNotContain("SKU001", servicesHtml);
        Assert.AreEqual(CatalogItemKind.Service, factory.Catalog.LastQuery!.Kind);
        Assert.Contains("Νέα υπηρεσία", servicesHtml);
        Assert.DoesNotContain("Νέο υπηρεσία", servicesHtml);

        using var categories = await client.GetAsync("/catalog?view=categories");
        string categoriesHtml = WebUtility.HtmlDecode(await categories.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.OK, categories.StatusCode);
        Assert.Contains("FOOD", categoriesHtml);
        Assert.Contains("Νέα κατηγορία", categoriesHtml);
        Assert.DoesNotContain("Νέο κατηγορία", categoriesHtml);
        Assert.AreEqual(1, factory.Catalog.CategoryReads);
    }

    [TestMethod]
    public async Task Catalog_EmptyStateIsCompactAndDoesNotShowUselessPager()
    {
        await using var factory = new SessionApiFactory();
        factory.Catalog.Items.Clear();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/catalog"));

        Assert.Contains("Δεν υπάρχουν ακόμη προϊόντα.", html);
        Assert.DoesNotContain("class=\"catalog-pager\"", html);
        Assert.DoesNotContain("class=\"catalog-context\"", html);
        Assert.AreEqual(1, factory.Catalog.ItemReads);
    }

    [TestMethod]
    public async Task Catalog_FilteredEmptyStateExplainsPrefixSearch()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/catalog?search=ZZZZ"));

        Assert.Contains("Δεν βρέθηκαν προϊόντα.", html);
        Assert.Contains("Δοκιμάστε διαφορετικό πρόθεμα", html);
        Assert.DoesNotContain("class=\"catalog-pager\"", html);
    }

    [TestMethod]
    public async Task Catalog_FiltersAreValidatedAndSentToCommonReadService()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var response = await client.GetAsync(
            "/catalog?view=products&search=%20SKU%20&pageSize=50&inactive=true");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SKU001", html);
        Assert.AreEqual("SKU", factory.Catalog.LastQuery!.Search);
        Assert.AreEqual(50, factory.Catalog.LastQuery.PageSize);
        Assert.IsTrue(factory.Catalog.LastQuery.IncludeInactive);
    }

    [TestMethod]
    public async Task SortingHeaders_UseAccessibleLinksAndPreserveFiltersPagingAndTenant()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string products = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog?view=products&tenantId={TenantId:D}&search=SKU&" +
            "pageSize=50&inactive=true&sort=name&desc=true"));

        Assert.Contains("aria-sort=\"descending\"", products);
        Assert.Contains("catalog-sort-header", products);
        Assert.Contains("sort=name", products);
        Assert.Contains("search=SKU", products);
        Assert.Contains("pageSize=50", products);
        Assert.Contains("inactive=true", products);
        Assert.AreEqual("name", factory.Catalog.LastQuery?.SortBy);
        Assert.IsTrue(factory.Catalog.LastQuery?.Descending == true);

        string categories = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/catalog?view=categories&sort=status&desc=true"));
        Assert.Contains("Κατηγορία", categories);
        Assert.Contains("aria-sort=\"descending\"", categories);
        Assert.AreEqual("status", factory.Catalog.LastQuery?.SortBy);
        Assert.IsTrue(factory.Catalog.LastQuery?.Descending == true);
    }

    [TestMethod]
    public async Task InvalidSortParameters_AreRejectedBeforeReadingDatabase()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        int readsBefore = factory.Catalog.ItemReads;

        string invalid = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/catalog?view=products&sort=code%3BDROP%20TABLE"));
        Assert.Contains("Μη έγκυρα φίλτρα", invalid);
        Assert.AreEqual(readsBefore, factory.Catalog.ItemReads);

        string invalidCategory = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/catalog?view=categories&sort=stock"));
        Assert.Contains("Μη έγκυρα φίλτρα", invalidCategory);
        Assert.AreEqual(0, factory.Catalog.CategoryReads);
    }

    [TestMethod]
    public async Task Catalog_InvalidInputsNeverReachCatalogStore()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        foreach (string url in new[]
        {
            "/catalog?view=unexpected",
            "/catalog?page=1001",
            "/catalog?pageSize=101",
            "/catalog?search=" + new string('z', 81)
        })
        {
            using var response = await client.GetAsync(url);
            string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Μη έγκυρα φίλτρα", html);
        }

        Assert.AreEqual(0, factory.Catalog.ItemReads);
        Assert.AreEqual(0, factory.Catalog.CategoryReads);
    }

    [TestMethod]
    public async Task Catalog_ForeignTenantAndForeignStoreNeverReadBusinessData()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var foreignTenant = await client.GetAsync(
            $"/catalog?tenantId={Guid.NewGuid():D}");
        string foreignHtml = WebUtility.HtmlDecode(
            await foreignTenant.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, foreignTenant.StatusCode);
        Assert.Contains("Η επιλεγμένη επιχείρηση δεν είναι διαθέσιμη", foreignHtml);
        Assert.DoesNotContain("SKU001", foreignHtml);

        using var foreignStore = await client.GetAsync(
            $"/catalog?tenantId={TenantId:D}&storeId={Guid.NewGuid():D}");
        string storeHtml = WebUtility.HtmlDecode(
            await foreignStore.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.OK, foreignStore.StatusCode);
        Assert.Contains("Το επιλεγμένο κατάστημα δεν είναι διαθέσιμο", storeHtml);
        Assert.DoesNotContain("SKU001", storeHtml);
        Assert.AreEqual(0, factory.Catalog.ItemReads);
    }

    [TestMethod]
    public async Task Catalog_PaginationKeepsViewAndOnlyDisplaysRequestedPage()
    {
        await using var factory = new SessionApiFactory();
        CatalogItemSummary original = factory.Catalog.Items[0];

        for (int index = 2; index <= 26; index++)
        {
            factory.Catalog.Items.Add(new CatalogItemSummary(
                Guid.NewGuid(), original.CategoryId,
                "SKU" + index.ToString("000"), "Δοκιμαστικό προϊόν",
                CatalogItemKind.Product, false, true, "Φαγητά"));
        }

        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        using var first = await client.GetAsync("/catalog?view=products");
        string firstHtml = WebUtility.HtmlDecode(await first.Content.ReadAsStringAsync());
        Assert.Contains("SKU025", firstHtml);
        Assert.DoesNotContain("SKU026", firstHtml);
        Assert.Contains("page=2", firstHtml);

        using var second = await client.GetAsync("/catalog?view=products&page=2");
        string secondHtml = WebUtility.HtmlDecode(await second.Content.ReadAsStringAsync());
        Assert.Contains("SKU026", secondHtml);
        Assert.DoesNotContain("SKU025", secondHtml);
        Assert.AreEqual(2, factory.Catalog.LastQuery!.Page);
    }

    [TestMethod]
    public async Task Catalog_EncodesUntrustedNamesInsteadOfRenderingMarkup()
    {
        await using var factory = new SessionApiFactory();
        var item = factory.Catalog.Items[0];
        factory.Catalog.Items[0] = item with { Name = "<img src=x onerror=alert(1)>" };
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string rawHtml = await client.GetStringAsync("/catalog");
        Assert.Contains("&lt;img", rawHtml);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", rawHtml);
    }

    [TestMethod]
    public async Task Catalog_DisplaysBarcodeAndAllowsSortableBarcodeWithoutShowingVerboseDescription()
    {
        await using var factory = new SessionApiFactory();
        var source = factory.Catalog.Items[0];
        factory.Catalog.Items[0] = source with { Barcode = "INT-123" };
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/catalog?view=products&sort=barcode"));
        Assert.Contains("INT-123", html);
        Assert.Contains("Barcode", html);
        Assert.AreEqual("barcode", factory.Catalog.LastQuery?.SortBy);
    }

    [TestMethod]
    public async Task ProductionCatalog_IsNotMapped()
    {
        await using var factory = new SessionApiFactory("Production");
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/catalog");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string html = await client.GetStringAsync("/login");
        Match match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success);
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value)
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }
}
