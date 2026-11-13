using System.Net;
using System.Text.RegularExpressions;
using Aenaon.Domain.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Παλινδρομικοί έλεγχοι της ενιαίας εμπορικής επιλογής σε πραγματικό HTTPS SSR.</summary>
[TestClass]
public sealed class CatalogCommercialPickerUiTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task ProductForm_UsesTwoAccessibleComboboxesWithOneNativeFallbackPerField()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&tenantId={Tenant:D}"));

        Assert.AreEqual(2, Regex.Matches(html, "data-commercial-picker").Count);
        Assert.AreEqual(2, Regex.Matches(html, "data-commercial-search").Count);
        Assert.AreEqual(2, Regex.Matches(html, "data-commercial-popup").Count);
        Assert.AreEqual(2, Regex.Matches(html, "data-commercial-native").Count);
        Assert.AreEqual(2, Regex.Matches(html, "data-commercial-value").Count);
        Assert.AreEqual(2, Regex.Matches(html, "role=\"combobox\"").Count + -1,
            "Οι δύο εμπορικοί επιλογείς και η κατηγορία παρέχουν προσβάσιμο combobox.");
        Assert.Contains("aria-autocomplete=\"list\"", html);
        Assert.Contains("aria-expanded=\"false\"", html);
        Assert.Contains("aria-activedescendant", await client.GetStringAsync(
            "/js/aenaon-commercial-picker.js"));

        // Η εφεδρική επιλογή κρατά το name, ενώ το κρυφό πεδίο είναι disabled.
        foreach (string field in new[] { "unitOfMeasure", "accountingType" })
        {
            Assert.AreEqual(2, Regex.Matches(html,
                "name=\"" + field + "\"").Count);
            Assert.IsTrue(Regex.IsMatch(html,
                "<input[^>]*name=\"" + field + "\"[^>]*disabled",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
            Assert.IsTrue(Regex.IsMatch(html,
                "<select[^>]*name=\"" + field + "\"[^>]*required",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
        }

        Assert.Contains("value=\"" + (int)CatalogUnitOfMeasure.Piece + "\"", html);
        Assert.Contains("value=\"" + (int)CatalogAccountingType.Merchandise + "\"", html);
        Assert.Contains("Τεμάχιο (τεμ.)", html);
        Assert.Contains("Εμπόρευμα", html);
    }

    [TestMethod]
    public async Task ServiceForm_RestrictsOptionsAndRetainsNumericValues()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=services&tenantId={Tenant:D}"));
        Match unit = Regex.Match(html,
            "<select[^>]*name=\"unitOfMeasure\"[^>]*>(.*?)</select>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Match accounting = Regex.Match(html,
            "<select[^>]*name=\"accountingType\"[^>]*>(.*?)</select>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(unit.Success);
        Assert.IsTrue(accounting.Success);
        Assert.Contains("value=\"8\"", unit.Groups[1].Value);
        Assert.Contains("value=\"7\"", unit.Groups[1].Value);
        Assert.DoesNotContain("value=\"1\"", unit.Groups[1].Value);
        Assert.Contains("value=\"5\"", accounting.Groups[1].Value);
        Assert.DoesNotContain("value=\"1\"", accounting.Groups[1].Value);
        Assert.AreEqual(2, Regex.Matches(unit.Groups[1].Value, "<option value=\"[0-9]+\"").Count);
        Assert.AreEqual(1, Regex.Matches(accounting.Groups[1].Value,
            "<option value=\"[0-9]+\"").Count);
    }

    [TestMethod]
    public async Task FingerprintedCommercialScript_UsesNoHtmlInjectionAndMaintainsNativeFallback()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        string login = await client.GetStringAsync("/login");
        Match script = Regex.Match(login,
            "src=\"([^\"]*aenaon-commercial-picker\\.js\\?v=[a-f0-9]{16})\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(script.Success, "Το JavaScript χρειάζεται αποτύπωμα cache busting.");

        using var asset = await client.GetAsync(WebUtility.HtmlDecode(script.Groups[1].Value));
        Assert.AreEqual(HttpStatusCode.OK, asset.StatusCode);
        string js = await asset.Content.ReadAsStringAsync();
        Assert.Contains("class CatalogCommercialPicker", js);
        Assert.Contains("positionPopup()", js);
        Assert.Contains("window.visualViewport", js);
        Assert.Contains("ArrowDown", js);
        Assert.Contains("ArrowUp", js);
        Assert.Contains("Enter", js);
        Assert.Contains("Escape", js);
        Assert.Contains("Tab", js);
        Assert.Contains("aria-activedescendant", js);
        Assert.Contains("this.native.disabled = true", js);
        Assert.Contains("this.value.disabled = false", js);
        Assert.Contains("event.stopImmediatePropagation()", js);
        Assert.DoesNotContain("innerHTML", js);
        Assert.Contains("textContent", js);

        string css = await client.GetStringAsync("/css/aenaon-backoffice.css");
        Assert.Contains(".catalog-commercial-picker", css);
        Assert.Contains(".catalog-category-picker__popup", css);
        Assert.Contains("input:focus-visible", css);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string html = await client.GetStringAsync("/login");
        Match token = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.IsTrue(token.Success);
        using var response = await client.PostAsync("/ui/session/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "admin",
                ["password"] = "Fixture-only-Secret!17",
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
            }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }
}
