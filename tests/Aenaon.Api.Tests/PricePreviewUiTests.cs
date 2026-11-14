using System.Net;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Πραγματικοί HTTP έλεγχοι SSR προεπισκόπησης με απομονωμένα δεδομένα.</summary>
[TestClass]
public sealed class PricePreviewUiTests
{
    private static readonly Guid Tenant =
        Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid Store =
        Guid.Parse("c697b9c3-e221-4156-b533-f54dee27d979");

    [TestMethod]
    public async Task Anonymous_CannotSeePricePreview()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/price-lists/preview");
        Assert.AreNotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, factory.PriceResolution.CandidateReads);
    }

    [TestMethod]
    public async Task Owner_CanSearchAndResolveRetailPriceWithoutWrites()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string list = WebUtility.HtmlDecode(await client.GetStringAsync("/price-lists"));
        Assert.Contains("/price-lists/preview", list);

        string page = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/preview?tenantId={Tenant:D}&scope=all&channel=Retail" +
            "&date=14%2F11%2F2026&search=SKU&itemId=" +
            SessionPriceResolutionFixture.ProductId.ToString("D")));

        Assert.Contains("Προεπισκόπηση τιμής", page);
        Assert.Contains("SKU001", page);
        Assert.Contains("10,5000", page);
        Assert.Contains("RETAIL", page);
        Assert.Contains("Τιμή με ΦΠΑ", page);
        Assert.Contains("3 — Όλα τα καταστήματα και κανάλι", page);
        Assert.Contains("Δεν υπολογίζει ΦΠΑ", page);
        Assert.AreEqual(1, factory.PriceResolution.CandidateReads);
        Assert.AreEqual(Tenant, factory.PriceResolution.LastTenantId);

        string editor = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}"));
        Assert.Contains("/price-lists/preview", editor);
    }

    [TestMethod]
    public async Task PriceSelection_SubmitsCurrentDateAndScopeWithinSameGetForm()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/preview?tenantId={Tenant:D}&scope=all&channel=Retail" +
            "&date=01%2F11%2F2026"));

        int formOpen = html.IndexOf("method=\"get\" action=\"/price-lists/preview\"", StringComparison.Ordinal);
        int dateField = html.IndexOf("name=\"date\"", formOpen, StringComparison.Ordinal);
        int scopeField = html.IndexOf("name=\"scope\"", formOpen, StringComparison.Ordinal);
        int channelField = html.IndexOf("name=\"channel\"", formOpen, StringComparison.Ordinal);
        int itemButton = html.IndexOf("name=\"itemId\"", formOpen, StringComparison.Ordinal);
        int formClose = html.IndexOf("</form>", formOpen, StringComparison.Ordinal);

        Assert.IsTrue(formOpen >= 0 && dateField > formOpen && dateField < formClose);
        Assert.IsTrue(scopeField > formOpen && scopeField < formClose);
        Assert.IsTrue(channelField > formOpen && channelField < formClose);
        Assert.IsTrue(itemButton > dateField && itemButton < formClose,
            "Η επιλογή είδους πρέπει να υποβάλει την τρέχουσα ημερομηνία και τα φίλτρα.");
        Assert.Contains("value=\"01/11/2026\"", html);
        Assert.Contains($"value=\"{SessionPriceResolutionFixture.ProductId:D}\"", html);
        Assert.Contains("type=\"submit\" class=\"catalog-edit-link price-preview-select\"", html);

        string css = await client.GetStringAsync("/css/aenaon-backoffice.css");
        Assert.Contains(".price-preview-search .catalog-search > input[type=\"search\"]", css);
        Assert.Contains("padding-left: 46px", css);
    }

    [TestMethod]
    public async Task SecondPage_ItemSelectionPreservesPageAndSearchStartsFromFirst()
    {
        await using var factory = new SessionApiFactory();
        for (int index = 1; index <= 24; index++)
        {
            factory.Catalog.Items.Add(new CatalogItemSummary(
                Guid.NewGuid(), Guid.NewGuid(), $"ZZZ{index:000}", $"Είδος {index:000}",
                CatalogItemKind.Product, false, true));
        }

        Guid secondPageItem = factory.Catalog.Items[^1].Id;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string pageTwo = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/price-lists/preview?scope=all&channel=Retail&date=01%2F11%2F2026&page=2"));

        Assert.Contains("ZZZ024", pageTwo);
        Assert.Contains("name=\"page\" value=\"2\"", pageTwo);
        Assert.Contains("name=\"requestedPage\" value=\"1\"", pageTwo);
        Assert.Contains($"name=\"itemId\" value=\"{secondPageItem:D}\"", pageTwo);

        string selected = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/price-lists/preview?scope=all&channel=Retail&date=01%2F11%2F2026" +
            $"&page=2&itemId={secondPageItem:D}"));
        Assert.Contains("ZZZ024 — Είδος 024", selected);
        Assert.DoesNotContain("Επιλέξτε ενεργό είδος", selected);

        string newSearch = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/price-lists/preview?scope=all&channel=Retail&date=01%2F11%2F2026" +
            "&page=2&pageAction=search"));
        Assert.Contains("name=\"page\" value=\"1\"", newSearch);
        Assert.DoesNotContain("ZZZ024", newSearch);
    }

    [TestMethod]
    public async Task Member_CanReadPreviewWithoutManageRights()
    {
        await using var factory = new SessionApiFactory();
        factory.Access.GrantedRole = TenantMembershipRole.Member;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string page = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/preview?tenantId={Tenant:D}&scope=all&channel=Retail" +
            "&date=14%2F11%2F2026&itemId=" +
            SessionPriceResolutionFixture.ProductId.ToString("D")));

        Assert.Contains("Μόνο ανάγνωση", page);
        Assert.Contains("10,5000", page);
        Assert.AreEqual(1, factory.PriceResolution.CandidateReads);
    }

    [TestMethod]
    public async Task AmbiguousPricing_IsExplicitAndNeverReturnsArbitraryAmount()
    {
        await using var factory = new SessionApiFactory();
        factory.PriceResolution.Candidates.Add(new PriceResolutionCandidate(
            Guid.NewGuid(), "RETAIL-SECOND", null, PriceListChannel.Retail,
            true, "EUR", new DateOnly(2026, 11, 1), null, true, 99m));

        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string page = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/preview?scope=all&channel=Retail&date=14%2F11%2F2026" +
            "&itemId=" + SessionPriceResolutionFixture.ProductId.ToString("D")));

        Assert.Contains("Αμφίσημη τιμή", page);
        Assert.Contains("Δεν επιλέχθηκε αυθαίρετη τιμή", page);
        Assert.Contains("Τιμοκατάλογος", page);
        Assert.DoesNotContain("99,0000 €", page);
    }

    [TestMethod]
    public async Task InvalidDateOrForeignStore_DoNotCallPriceResolution()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string invalidDate = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/preview?tenantId={Tenant:D}&scope=all&channel=Retail" +
            "&date=31%2F02%2F2026&itemId=" +
            SessionPriceResolutionFixture.ProductId.ToString("D")));
        Assert.Contains("Πληκτρολογήστε έγκυρη ημερομηνία", invalidDate);
        Assert.AreEqual(0, factory.PriceResolution.CandidateReads);

        string invalidStore = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/preview?tenantId={Tenant:D}&scope={Guid.NewGuid():D}" +
            "&channel=Retail&date=14%2F11%2F2026&itemId=" +
            SessionPriceResolutionFixture.ProductId.ToString("D")));
        Assert.Contains("Ελέγξτε τα φίλτρα", invalidStore);
        Assert.AreEqual(0, factory.PriceResolution.CandidateReads);
    }

    [TestMethod]
    public async Task InactiveItemOrUnpricedChannel_DoNotInventPrices()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        factory.PriceResolution.ItemActive = false;

        string unavailable = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/price-lists/preview?scope=all&channel=Retail&date=14%2F11%2F2026" +
            "&itemId=" + SessionPriceResolutionFixture.ProductId.ToString("D")));
        Assert.Contains("Το είδος δεν είναι διαθέσιμο", unavailable);

        factory.PriceResolution.ItemActive = true;
        string unpriced = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/price-lists/preview?scope=all&channel=Takeaway&date=14%2F11%2F2026" +
            "&itemId=" + SessionPriceResolutionFixture.ProductId.ToString("D")));
        Assert.Contains("Δεν βρέθηκε τιμή", unpriced);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string loginPage = await client.GetStringAsync("/login");
        string csrfToken = ExtractToken(loginPage);
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = csrfToken
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string ExtractToken(string html)
    {
        System.Text.RegularExpressions.Match match =
            System.Text.RegularExpressions.Regex.Match(html,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
