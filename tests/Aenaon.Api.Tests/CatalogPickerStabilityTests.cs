using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Παλινδρομικοί έλεγχοι αποκατάστασης της πλοήγησης και του Category Picker.</summary>
[TestClass]
public sealed class CatalogPickerStabilityTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task WelcomeTitle_KeepsKeyboardFocusVisibleButSuppressesProgrammaticHeadingRing()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        string welcome = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        string css = await client.GetStringAsync("/css/aenaon.css");
        string routes = (await client.GetStringAsync("/login"));

        Assert.Contains("Ένας χώρος για κάθε επιχείρηση.", welcome);
        Assert.Contains("h1[tabindex=\"-1\"]:focus-visible", css);
        Assert.Contains(":focus-visible { outline: 3px", css);
        Assert.Contains("Σύνδεση στο Aenaon", routes);
    }

    [TestMethod]
    public async Task CategoryManage_ProvidesAlwaysVisibleQuickCreateAndClearGreekBreadcrumbs()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string form = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&tenantId={Tenant:D}"));
        Assert.Contains("data-picker-inline-action", form);
        Assert.Contains("data-picker-create", form);
        Assert.Contains("data-picker-popup", form);
        Assert.Contains("data-category-quick-submit", form);
        Assert.Contains("aria-label=\"Διαδρομή πλοήγησης\"", form);
        Assert.Contains("Κατάλογος", form);
        Assert.Contains("Προϊόντα", form);
        Assert.Contains("Νέο προϊόν", form);
        Assert.DoesNotContain("Εφαρμογή <span>/</span>", form);

        string category = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=categories&tenantId={Tenant:D}"));
        Assert.Contains("Κατηγορίες", category);
        Assert.Contains("Νέα κατηγορία", category);
        Assert.DoesNotContain("data-picker-inline-action", category);
    }

    [TestMethod]
    public async Task PickerAssets_UseViewportGeometryAndCancelStaleSearchesWithoutDependencies()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        string js = await client.GetStringAsync("/js/aenaon-category-picker.js");
        string css = await client.GetStringAsync("/css/aenaon-backoffice.css");
        Assert.Contains("positionPopup()", js);
        Assert.Contains("getBoundingClientRect()", js);
        Assert.Contains("window.visualViewport", js);
        Assert.Contains("availableAbove", js);
        Assert.Contains("availableBelow", js);
        Assert.Contains("useViewportPanel", js);
        Assert.Contains("new AbortController()", js);
        Assert.Contains("sequence !== this.sequence", js);
        Assert.Contains("form.requestSubmit(submit)", js);
        Assert.Contains("createButtons", js);
        Assert.Contains("addEventListener(\"mousedown\"", js);
        Assert.Contains("option.setAttribute(\"role\", \"option\")", js);
        Assert.Contains("catalog-category-picker__option--active", js);
        Assert.Contains("this.scheduleSearch(this.input.value.trim())", js);
        Assert.DoesNotContain("this.open();\n                this.scheduleSearch(search);", js);

        Assert.Contains(".catalog-category-picker__popup", css);
        Assert.Contains("position: fixed", css);
        Assert.Contains(".catalog-category-picker--ready .catalog-category-picker__inline-action", css);
        Assert.Contains(".catalog-category-picker__popup[hidden]", css);
        Assert.Contains(".catalog-category-picker__option--active", css);
        Assert.Contains("font-size: 13px", css);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        string login = await client.GetStringAsync("/login");
        Match token = Regex.Match(login,
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
