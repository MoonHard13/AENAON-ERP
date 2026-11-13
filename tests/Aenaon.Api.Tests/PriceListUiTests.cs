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

        string create = await client.GetStringAsync("/price-lists/manage");
        Assert.Contains("action=\"/ui/price-lists/save\"", create);
        Assert.Contains("name=\"scopeStoreId\"", create);
        Assert.Contains("name=\"__RequestVerificationToken\"", create);
        Assert.Contains("data-commercial-picker", create);
        Assert.Contains("data-commercial-native", create);
        Assert.Contains("name=\"channel\"", create);
        Assert.Contains("name=\"includeVat\"", create);
        Assert.Contains("13/11/2026", create);
        Assert.Contains("pattern=\"[0-9]{2}/[0-9]{2}/[0-9]{4}\"", create);

        string edit = await client.GetStringAsync(
            $"/price-lists/manage?tenantId={Tenant:D}&id={SessionPriceListFixture.ListId:D}");
        Assert.Contains("action=\"/ui/price-lists/prices\"", edit);
        Assert.Contains($"price_{SessionPriceListFixture.ItemId:D}", edit);
        Assert.Contains("0.0001", edit);
        Assert.DoesNotContain("Σελίδα 1", edit);
        Assert.Contains("Αφαίρεση τιμής", edit);
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
