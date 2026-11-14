using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>HTTP έλεγχοι πλοήγησης, ρόλων, antiforgery και τιμών χωρίς παραγωγική βάση.</summary>
[TestClass]
public sealed class PriceListUiTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task Owner_CanBrowseCreateEditAndSeeBatchPriceTable()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string page = WebUtility.HtmlDecode(await client.GetStringAsync("/price-lists"));
        Assert.Contains("Νέος τιμοκατάλογος", page);
        Assert.Contains("RETAIL", page);
        Assert.Contains("/price-lists/manage", page);

        string create = WebUtility.HtmlDecode(await client.GetStringAsync("/price-lists/manage"));
        Assert.Contains("action=\"/ui/price-lists/save\"", create);
        Assert.Contains("name=\"scopeStoreId\"", create);
        Assert.Contains("name=\"__RequestVerificationToken\"", create);
        Assert.Contains("data-commercial-picker", create);
        Assert.Contains("data-commercial-native", create);
        Assert.Contains("name=\"channel\"", create);
        Assert.Contains("name=\"includeVat\"", create);
        Assert.Contains("placeholder=\"ηη/μμ/εεεε\"", create);
        Assert.Contains("pattern=\"[0-9]{2}/[0-9]{2}/[0-9]{4}\"", create);
        int card = create.IndexOf("<section class=\"surface-card catalog-editor\"", StringComparison.Ordinal);
        int title = create.IndexOf("id=\"price-editor-title\"", card, StringComparison.Ordinal);
        int actions = create.IndexOf("class=\"catalog-editor__header-actions\"", title, StringComparison.Ordinal);
        int back = create.IndexOf("Επιστροφή στους τιμοκαταλόγους", actions, StringComparison.Ordinal);
        int form = create.IndexOf("action=\"/ui/price-lists/save\"", back, StringComparison.Ordinal);
        Assert.IsTrue(card >= 0 && title > card && actions > title);
        Assert.IsTrue(back > actions && form > back,
            "Η επιστροφή πρέπει να βρίσκεται μέσα στην κεφαλίδα της κάρτας.");
        Assert.DoesNotContain("class=\"price-list-toolbar\"", create);

        string edit = await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}");
        Assert.Contains("action=\"/ui/price-lists/prices\"", edit);
        Assert.Contains($"price_{SessionPriceListFixture.ItemId:D}", edit);
        Assert.Contains("0.0001", edit);
        Assert.DoesNotContain("<nav class=\"catalog-pagination\"", edit);
        Assert.Contains("Αφαίρεση τιμής", edit);
    }

    [TestMethod]
    public async Task PriceListItems_SortHeadersAndPostRedirectsPreserveSelection()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string url = $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}";

        string initial = WebUtility.HtmlDecode(await client.GetStringAsync(url));
        Assert.Contains("Κωδικός: ταξινόμηση φθίνουσα", initial);
        Assert.Contains("sort=code&desc=true", initial);
        Assert.Contains("sort=price&desc=false", initial);
        Assert.Contains("sort=kind&desc=false", initial);
        Assert.Contains("sort=priced&desc=false", initial);

        string sorted = WebUtility.HtmlDecode(await client.GetStringAsync(
            url + "&sort=price&desc=true&search=SKU"));
        Assert.Contains("aria-sort=\"descending\"", sorted);
        Assert.AreEqual("price", factory.PriceLists.LastItemsSort);
        Assert.IsTrue(factory.PriceLists.LastItemsDescending);
        Assert.Contains("sort=price", sorted);
        Assert.Contains("name=\"desc\" value=\"true\"", sorted);
        Assert.Contains("search=SKU", sorted);

        string token = Token(sorted);
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["tenantId"] = Tenant.ToString("D"),
            ["id"] = SessionPriceListFixture.ListId.ToString("D"),
            ["page"] = "1",
            ["search"] = "SKU",
            ["sort"] = "price",
            ["desc"] = "true",
            [$"expected_{SessionPriceListFixture.ItemId:D}"] = "10.0000",
            [$"price_{SessionPriceListFixture.ItemId:D}"] = "11.5000"
        };

        using var saved = await client.PostAsync("/ui/price-lists/prices",
            new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("sort=price&desc=true", saved.Headers.Location?.OriginalString ?? "");
        Assert.Contains("search=SKU", saved.Headers.Location?.OriginalString ?? "");

        form["sort"] = "invalid";
        using var invalid = await client.PostAsync("/ui/price-lists/prices",
            new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.PriceCalls);

        var removal = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["tenantId"] = Tenant.ToString("D"),
            ["id"] = SessionPriceListFixture.ListId.ToString("D"),
            ["catalogItemId"] = SessionPriceListFixture.ItemId.ToString("D"),
            ["expectedPrice"] = "10.0000",
            ["page"] = "1",
            ["search"] = "SKU",
            ["sort"] = "name",
            ["desc"] = "true"
        };
        using var removed = await client.PostAsync("/ui/price-lists/remove-price",
            new FormUrlEncodedContent(removal));
        Assert.AreEqual(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Contains("sort=name&desc=true", removed.Headers.Location?.OriginalString ?? "");
    }

    [TestMethod]
    public async Task HistoryIsDisplayedReadOnlyForOwnerAndMember()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string owner = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}"));
        Assert.Contains("Ιστορικό αλλαγών τιμοκαταλόγου", owner);
        Assert.Contains("SKU001", owner);
        Assert.Contains("9.5000", owner);
        Assert.Contains("12.5000", owner);
        Assert.Contains("14/11/2026 16:30:00", owner);
        Assert.Contains("Προέλευση / Χρήστης", owner);
        Assert.DoesNotContain("action=\"/ui/price-lists/audit-delete\"", owner);

        factory.Access.GrantedRole = TenantMembershipRole.Member;
        string member = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}"));
        Assert.Contains("Ιστορικό αλλαγών τιμοκαταλόγου", member);
        Assert.DoesNotContain("Αποθήκευση τιμών σελίδας", member);
    }

    [TestMethod]
    public async Task PriceListEditor_UsesDineInLabelAndStatusBesideTitle()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string create = WebUtility.HtmlDecode(await client.GetStringAsync("/price-lists/manage"));
        Assert.Contains("Dine In", create);
        Assert.DoesNotContain("Σάλα", create);

        string edit = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}"));
        int card = edit.IndexOf("<section class=\"surface-card catalog-editor\"", StringComparison.Ordinal);
        int titleRow = edit.IndexOf("class=\"catalog-editor__title-row\"", card, StringComparison.Ordinal);
        int title = edit.IndexOf("id=\"price-editor-title\"", titleRow, StringComparison.Ordinal);
        int status = edit.IndexOf("Κατάσταση τιμοκαταλόγου:", title, StringComparison.Ordinal);
        int actions = edit.IndexOf("class=\"catalog-editor__header-actions\"", status, StringComparison.Ordinal);
        int back = edit.IndexOf("Επιστροφή στους τιμοκαταλόγους", actions, StringComparison.Ordinal);

        Assert.IsTrue(card >= 0 && titleRow > card && title > titleRow);
        Assert.IsTrue(status > title && actions > status && back > actions,
            "Η κατάσταση τιμοκαταλόγου πρέπει να εμφανίζεται δίπλα στον τίτλο.");
    }

    [TestMethod]
    public async Task ExistingPriceList_ProvidesAccessibleCollapsiblePanelsAndEditablePolicy()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}"));

        Assert.Contains("data-panel-toggle=\"price-list-details\"", html);
        Assert.Contains("data-panel-toggle=\"price-list-items-content\"", html);
        Assert.Contains("aria-controls=\"price-list-details\"", html);
        Assert.Contains("data-panel-content", html);
        Assert.Contains("name=\"expectedScopeStoreId\"", html);
        Assert.Contains("name=\"expectedChannel\"", html);
        Assert.Contains("name=\"expectedIncludeVat\"", html);
        Assert.Contains("name=\"expectedName\"", html);
        Assert.Contains("name=\"expectedValidFrom\"", html);
        Assert.Contains("name=\"expectedValidUntil\"", html);
        Assert.Contains("name=\"confirmPolicyChange\"", html);
        Assert.Contains("name=\"scopeStoreId\"", html);
        Assert.Contains("name=\"channel\"", html);
        Assert.Contains("name=\"includeVat\"", html);
        Assert.DoesNotContain("price-list-readonly-value", html);
    }

    [TestMethod]
    public async Task UpdateCommercialPolicy_HasExpectedSnapshotAndRequiresValidParameters()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string page = await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}");

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["tenantId"] = Tenant.ToString("D"),
            ["id"] = SessionPriceListFixture.ListId.ToString("D"),
            ["action"] = "update",
            ["name"] = "Νέο όνομα",
            ["scopeStoreId"] = "",
            ["expectedScopeStoreId"] = "",
            ["channel"] = "Delivery",
            ["expectedChannel"] = "Retail",
            ["includeVat"] = "false",
            ["expectedIncludeVat"] = "true",
            ["expectedName"] = "Βασική λιανική",
            ["expectedValidFrom"] = "13/11/2026",
            ["expectedValidUntil"] = "",
            ["confirmPolicyChange"] = "yes",
            ["validFrom"] = "13/11/2026",
            ["validUntil"] = ""
        };
        using var posted = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(fields));

        Assert.AreEqual(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.SaveCalls);
        Assert.IsNotNull(factory.PriceLists.LastChange);
        Assert.IsTrue(factory.PriceLists.LastChange.EditCommercialProfile);
        Assert.IsTrue(factory.PriceLists.LastChange.ConfirmPolicyChange);
        Assert.AreEqual(Aenaon.Domain.Catalog.PriceListChannel.Delivery,
            factory.PriceLists.LastChange.Channel);
        Assert.IsFalse(factory.PriceLists.LastChange.PricesIncludeVat);
        Assert.AreEqual(Aenaon.Domain.Catalog.PriceListChannel.Retail,
            factory.PriceLists.LastChange.ExpectedChannel);
        Assert.IsTrue(factory.PriceLists.LastChange.ExpectedPricesIncludeVat);
        Assert.IsTrue(factory.PriceLists.LastChange.CheckExpectedDetails);
        Assert.AreEqual("Βασική λιανική", factory.PriceLists.LastChange.ExpectedName);
        Assert.IsTrue(factory.PriceLists.LastChange.ExpectedValidFrom == new DateOnly(2026, 11, 13));

        fields.Remove("expectedName");
        using var missingSnapshot = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingSnapshot.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.SaveCalls);
        fields["expectedName"] = "Βασική λιανική";

        fields["channel"] = "INVALID";
        using var invalid = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.SaveCalls);
    }

    [TestMethod]
    public async Task MemberCanViewButCannotSave()
    {
        await using var factory = new SessionApiFactory();
        factory.Access.GrantedRole = TenantMembershipRole.Member;
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string page = await client.GetStringAsync("/price-lists");
        Assert.Contains("Μόνο ανάγνωση", page);
        Assert.DoesNotContain("Νέος τιμοκατάλογος", page);
        string details = await client.GetStringAsync(
            $"/price-lists/manage?id={SessionPriceListFixture.ListId:D}");
        // Το read-only rendering δεν προσφέρει πεδία τιμών ούτε κουμπί αποθήκευσης.
        Assert.DoesNotContain($"name=\"price_{SessionPriceListFixture.ItemId:D}\"", details);
        Assert.DoesNotContain("Αποθήκευση τιμών σελίδας", details);

        using var post = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(details),
                ["tenantId"] = Tenant.ToString("D"),
                ["action"] = "create",
                ["name"] = "Forbidden",
                ["code"] = "FORBIDDEN",
                ["validFrom"] = "13/11/2026",
                ["validUntil"] = "",
                ["channel"] = "Retail",
                ["includeVat"] = "true"
            }));
        Assert.AreEqual(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.AreEqual(0, factory.PriceLists.SaveCalls);
    }

    [TestMethod]
    public async Task CreateRequiresCsrfAndRejectsForeignTenant()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        Dictionary<string, string> payload = new()
        {
            ["tenantId"] = Tenant.ToString("D"),
            ["action"] = "create",
            ["name"] = "Δοκιμή",
            ["code"] = "RETAIL-NEW",
            ["validFrom"] = "13/11/2026",
            ["validUntil"] = "",
            ["channel"] = "Retail",
            ["includeVat"] = "true"
        };
        using var missingToken = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(payload));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingToken.StatusCode);

        string form = await client.GetStringAsync("/price-lists/manage");
        payload["__RequestVerificationToken"] = Token(form);
        using var saved = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(payload));
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("&saved=1", saved.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(1, factory.PriceLists.SaveCalls);

        payload["tenantId"] = Guid.NewGuid().ToString("D");
        using var denied = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(payload));
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.SaveCalls);
    }

    [TestMethod]
    public async Task PriceBatchRejectsInvalidNumberBeforeCallingStore()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string page = await client.GetStringAsync(
            $"/price-lists/manage?id={SessionPriceListFixture.ListId:D}");

        Dictionary<string, string> fields = new()
        {
            ["__RequestVerificationToken"] = Token(page),
            ["tenantId"] = Tenant.ToString("D"),
            ["id"] = SessionPriceListFixture.ListId.ToString("D"),
            ["page"] = "1",
            ["search"] = "",
            [$"expected_{SessionPriceListFixture.ItemId:D}"] = "10.0000",
            [$"price_{SessionPriceListFixture.ItemId:D}"] = "-1.0000"
        };
        using var invalid = await client.PostAsync("/ui/price-lists/prices",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.Redirect, invalid.StatusCode);
        Assert.Contains("&error=invalid", invalid.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(0, factory.PriceLists.PriceCalls);

        fields[$"price_{SessionPriceListFixture.ItemId:D}"] = "12.3456";
        using var saved = await client.PostAsync("/ui/price-lists/prices",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.PriceCalls);
        Assert.IsNotNull(factory.PriceLists.LastPriceChanges);
        Assert.AreEqual(10m, factory.PriceLists.LastPriceChanges[0].ExpectedUnitPrice);
        Assert.IsTrue(factory.PriceLists.LastPriceChanges[0].CheckExpected);

        fields[$"price_{SessionPriceListFixture.ItemId:D}"] = "10.0000";
        using var unchanged = await client.PostAsync("/ui/price-lists/prices",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.Redirect, unchanged.StatusCode);
        Assert.Contains("&unchanged=1", unchanged.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(1, factory.PriceLists.PriceCalls);
    }

    [TestMethod]
    public async Task RemovePrice_RequiresOwnerAntiforgeryAndOriginalPrice()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string page = await client.GetStringAsync(
            $"/price-lists/manage?id={SessionPriceListFixture.ListId:D}");
        string token = Token(page);

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["tenantId"] = Tenant.ToString("D"),
            ["id"] = SessionPriceListFixture.ListId.ToString("D"),
            ["catalogItemId"] = SessionPriceListFixture.ItemId.ToString("D"),
            ["expectedPrice"] = "10.0000",
            ["search"] = "",
            ["page"] = "1"
        };

        using var success = await client.PostAsync("/ui/price-lists/remove-price",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.Redirect, success.StatusCode);
        Assert.Contains("&removed=1", success.Headers.Location?.OriginalString ?? "");
        Assert.AreEqual(1, factory.PriceLists.RemoveCalls);

        fields["expectedPrice"] = "not a price";
        using var invalid = await client.PostAsync("/ui/price-lists/remove-price",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.RemoveCalls);

        fields["expectedPrice"] = "10.0000";
        fields["tenantId"] = Guid.NewGuid().ToString("D");
        using var foreign = await client.PostAsync("/ui/price-lists/remove-price",
            new FormUrlEncodedContent(fields));
        Assert.AreEqual(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.AreEqual(1, factory.PriceLists.RemoveCalls);
    }

    [TestMethod]
    public async Task PriceListCreation_RejectsNonGreekDate()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);
        string page = await client.GetStringAsync("/price-lists/manage");
        using var bad = await client.PostAsync("/ui/price-lists/save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(page),
                ["tenantId"] = Tenant.ToString("D"),
                ["action"] = "create",
                ["name"] = "Bad date",
                ["code"] = "BAD-DATE",
                ["validFrom"] = "11/13/2026",
                ["validUntil"] = "",
                ["channel"] = "Retail",
                ["includeVat"] = "true"
            }));
        Assert.AreEqual(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.AreEqual(0, factory.PriceLists.SaveCalls);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string page = await client.GetStringAsync("/login");
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = Token(page)
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string Token(string html)
    {
        Match value = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(value.Success);
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }
}
