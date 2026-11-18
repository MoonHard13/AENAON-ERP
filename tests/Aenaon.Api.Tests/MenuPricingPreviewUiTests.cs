using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Application.Menus;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Πραγματικό HTTPS/SSR pipeline με fixtures αντί για πραγματικό SQL Server.</summary>
[TestClass]
public sealed class MenuPricingPreviewUiTests
{
    private static readonly Guid Tenant =
        Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");
    private static readonly Guid Store =
        Guid.Parse("c697b9c3-e221-4156-b533-f54dee27d979");

    [TestMethod]
    public async Task Anonymous_CannotReadMenuPreview()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(
            $"/menus/preview?tenantId={Tenant:D}&menuId={SessionMenuDraftFixture.MenuId:D}");
        Assert.IsTrue(response.StatusCode is HttpStatusCode.Redirect or
            HttpStatusCode.Unauthorized);
        Assert.AreEqual(0, factory.MenuPreviewPrices.ReadCalls);
    }

    [TestMethod]
    public async Task Member_CanReadResolvedPriceWithoutPublishingOrSqlWrite()
    {
        await using var factory = new SessionApiFactory();
        factory.Access.GrantedRole = TenantMembershipRole.Member;
        factory.MenuPreviewPrices.Candidates.Add(new(
            SessionMenuDraftFixture.ProductId,
            new PriceResolutionCandidate(Guid.NewGuid(), "RETAIL", null,
                PriceListChannel.DineIn, true, "EUR",
                new DateOnly(2026, 11, 1), null, true, 12.5000m)));
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string builder = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus?tenantId={Tenant:D}&menuId={SessionMenuDraftFixture.MenuId:D}"));
        Assert.Contains("Προεπισκόπηση τιμών", builder);

        string preview = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus/preview?tenantId={Tenant:D}" +
            $"&menuId={SessionMenuDraftFixture.MenuId:D}&storeId={Store:D}" +
            "&channel=DineIn&businessDate=2026-11-18"));
        Assert.Contains("Μόνο ανάγνωση — DRAFT", preview);
        Assert.Contains("Πίτσα", preview);
        Assert.Contains("Έγκυρη τιμή", preview);
        Assert.Contains("12,50", preview);
        Assert.Contains("RETAIL", preview);
        Assert.Contains("1", preview);
        Assert.Contains("Οι τιμές δεν αποτελούν κλειδωμένη", preview);
        Assert.AreEqual(1, factory.MenuPreviewPrices.ReadCalls);
        Assert.AreEqual(Tenant, factory.MenuPreviewPrices.LastTenant);
        Assert.AreEqual(Store, factory.MenuPreviewPrices.LastStore);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);
    }

    [TestMethod]
    public async Task MissingPriceAndInvalidInput_AreSafeAndNeverPublish()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string noDate = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus/preview?tenantId={Tenant:D}&menuId={SessionMenuDraftFixture.MenuId:D}"));
        Assert.Contains("Επιλέξτε συγκεκριμένο κατάστημα", noDate);
        Assert.AreEqual(0, factory.MenuPreviewPrices.ReadCalls);

        string missing = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus/preview?tenantId={Tenant:D}" +
            $"&menuId={SessionMenuDraftFixture.MenuId:D}&storeId={Store:D}" +
            "&channel=DineIn&businessDate=2026-11-18"));
        Assert.Contains("Δεν βρέθηκε τιμή", missing);
        Assert.Contains("αποκλεισμένα", missing);
        Assert.DoesNotContain("Αποθήκευση τιμής", missing);

        string invalid = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus/preview?tenantId={Tenant:D}" +
            $"&menuId={SessionMenuDraftFixture.MenuId:D}&storeId={Store:D}" +
            "&channel=All&businessDate=2026-11-18"));
        Assert.Contains("Απαιτείται έγκυρο μενού", invalid);

        string foreign = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/menus/preview?tenantId={Tenant:D}" +
            $"&menuId={SessionMenuDraftFixture.MenuId:D}&storeId={Guid.NewGuid():D}" +
            "&channel=DineIn&businessDate=2026-11-18"));
        Assert.Contains("Δεν έχετε πρόσβαση", foreign);
        Assert.AreEqual(0, factory.MenuDrafts.ApplyCalls);
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
