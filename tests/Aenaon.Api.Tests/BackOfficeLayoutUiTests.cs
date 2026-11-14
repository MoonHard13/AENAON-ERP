using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Έλεγχοι παλινδρόμησης της συμπαγούς Επισκόπησης και του Καταλόγου.</summary>
[TestClass]
public sealed class BackOfficeLayoutUiTests
{
    private static readonly Guid Tenant = Guid.Parse("9698df79-3c31-41c9-b292-b7240c7265fb");

    [TestMethod]
    public async Task Overview_HasOneUsefulHeadingAndPreservesWorkspaceSelection()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/dashboard"));

        Assert.DoesNotContain("ΚΕΝΤΡΙΚΗ ΕΠΙΣΚΟΠΗΣΗ", html);
        Assert.DoesNotContain("Καλώς ήρθατε,", html);
        Assert.DoesNotContain("ΕΠΙΛΟΓΗ ΠΛΑΙΣΙΟΥ", html);
        Assert.Contains("<h1 id=\"workspace-selection-title\">Επιχείρηση & κατάστημα</h1>", html);
        Assert.Contains("class=\"dashboard__body dashboard-overview\"", html);
        Assert.Contains("id=\"workspace-selection\"", html);
        Assert.Contains("action=\"/ui/workspace/select\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("Οι επιχειρήσεις μου", html);
        Assert.Contains("Τα καταστήματά μου", html);
        Assert.Contains("Άνοιγμα καταλόγου", html);
    }

    [TestMethod]
    [DataRow("products", "Προϊόντα", "Νέο προϊόν")]
    [DataRow("services", "Υπηρεσίες", "Νέα υπηρεσία")]
    [DataRow("categories", "Κατηγορίες", "Νέα κατηγορία")]
    public async Task Catalog_KeepsCreateActionInsideSelectedTableCard(
        string view, string title, string createText)
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog?view={view}&tenantId={Tenant:D}"));

        Assert.DoesNotContain("ΚΑΤΑΛΟΓΟΣ / ΔΙΑΧΕΙΡΙΣΗ", html);
        Assert.DoesNotContain("Προϊόντα, υπηρεσίες και κατηγορίες της επιχείρησής σας.", html);

        int panelStart = html.IndexOf("<section class=\"surface-card catalog-surface\"", StringComparison.Ordinal);
        int actionsStart = html.IndexOf("class=\"catalog-surface__actions\"", panelStart, StringComparison.Ordinal);
        int filtersStart = html.IndexOf("<form class=\"catalog-filters\"", panelStart, StringComparison.Ordinal);

        Assert.IsTrue(panelStart >= 0, "Το πλαίσιο του καταλόγου πρέπει να υπάρχει.");
        Assert.IsTrue(actionsStart > panelStart, "Η ενέργεια δημιουργίας ανήκει στο πλαίσιο.");
        Assert.IsTrue(filtersStart > actionsStart, "Η ενέργεια προηγείται των φίλτρων.");
        Assert.Contains($"<h1 id=\"catalog-list-title\">{title}</h1>", html);
        Assert.Contains("class=\"button button--primary catalog-create\"", html);
        Assert.Contains($"href=\"/catalog/manage?view={view}&tenantId={Tenant:D}", html);
        Assert.Contains(createText, html);
        Assert.Contains("class=\"catalog-tabs\"", html);
        Assert.Contains("role=\"search\"", html);

        // Διασφαλίζουμε ότι δεν εμφανίζεται δεύτερη ενέργεια δημιουργίας εκτός του πίνακα.
        Assert.AreEqual(1, Regex.Matches(html, "class=\"button button--primary catalog-create\"").Count);
    }

    [TestMethod]
    [DataRow("products")]
    [DataRow("services")]
    [DataRow("categories")]
    public async Task CatalogEditor_ReturnActionLivesInsideCardHeader(string view)
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view={view}&tenantId={Tenant:D}"));
        int card = html.IndexOf("<section class=\"surface-card catalog-editor\"", StringComparison.Ordinal);
        int header = html.IndexOf("class=\"catalog-editor__header\"", card, StringComparison.Ordinal);
        int actions = html.IndexOf("class=\"catalog-editor__header-actions\"", header, StringComparison.Ordinal);
        int back = html.IndexOf("Επιστροφή στον κατάλογο", actions, StringComparison.Ordinal);
        int form = html.IndexOf("action=\"/ui/catalog/save\"", header, StringComparison.Ordinal);

        Assert.IsTrue(card >= 0, "Η κάρτα του editor πρέπει να εμφανίζεται.");
        Assert.IsTrue(header > card && actions > header, "Οι ενέργειες ανήκουν στην κεφαλίδα της κάρτας.");
        Assert.IsTrue(back > actions && form > back, "Η επιστροφή είναι μέσα στην κεφαλίδα, πριν από τα πεδία.");
        Assert.DoesNotContain("class=\"catalog-editor__toolbar\"", html);
    }

    [TestMethod]
    public async Task ExistingProductEditor_ShowsStatusBesideHeading_NotAsNavigation()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/catalog/manage?view=products&id={SessionCatalogWriteFixture.ItemId:D}&tenantId={Tenant:D}"));
        int card = html.IndexOf("<section class=\"surface-card catalog-editor\"", StringComparison.Ordinal);
        int titleRow = html.IndexOf("class=\"catalog-editor__title-row\"", card, StringComparison.Ordinal);
        int heading = html.IndexOf("Βασικά στοιχεία", titleRow, StringComparison.Ordinal);
        int status = html.IndexOf("Κατάσταση καταχώρησης:", heading, StringComparison.Ordinal);
        int actions = html.IndexOf("class=\"catalog-editor__header-actions\"", status, StringComparison.Ordinal);
        int back = html.IndexOf("Επιστροφή στον κατάλογο", actions, StringComparison.Ordinal);

        Assert.IsTrue(card >= 0 && titleRow > card && heading > titleRow);
        Assert.IsTrue(status > heading && actions > status && back > actions,
            "Η κατάσταση πρέπει να συνοδεύει τον τίτλο, όχι το κουμπί επιστροφής.");
    }

    [TestMethod]
    public async Task SidebarToggle_IsInsideBottomSidebarFooterWithNoTopbarOverlap()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();
        await LoginAsync(client);

        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/dashboard"));
        int sidebarHeader = html.IndexOf("class=\"sidebar__header\"", StringComparison.Ordinal);
        int navigation = html.IndexOf("id=\"aenaon-primary-sidebar-nav\"", sidebarHeader, StringComparison.Ordinal);
        int footer = html.IndexOf("class=\"sidebar__foot\"", navigation, StringComparison.Ordinal);
        int toggle = html.IndexOf("data-sidebar-toggle", footer, StringComparison.Ordinal);
        int status = html.IndexOf("class=\"sidebar__status-dot\"", toggle, StringComparison.Ordinal);
        int sidebarEnd = html.IndexOf("</aside>", footer, StringComparison.Ordinal);
        int topbar = html.IndexOf("class=\"topbar\"", sidebarEnd, StringComparison.Ordinal);

        Assert.IsTrue(sidebarHeader >= 0 && navigation > sidebarHeader);
        Assert.IsTrue(footer > navigation && toggle > footer && status > toggle);
        Assert.IsTrue(sidebarEnd > status && topbar > sidebarEnd);
        Assert.DoesNotContain("data-sidebar-toggle", html[topbar..]);
        Assert.DoesNotContain("class=\"sidebar__toggle-label\"", html);
        Assert.Contains("aria-label=\"Σύμπτυξη πλαϊνού μενού\"", html);
        Assert.Contains("accesskey=\"x\"", html);
    }

    [TestMethod]
    public async Task Styles_PreserveResponsiveActionsAndVisibleFocus()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        string css = await client.GetStringAsync("/css/aenaon-backoffice.css");

        Assert.Contains(".catalog-surface__identity", css);
        Assert.Contains(".catalog-surface__actions", css);
        Assert.Contains(".catalog-surface__actions .catalog-create:focus-visible", css);
        Assert.Contains(".catalog-surface__actions .catalog-create { width: 100%; }", css);
        Assert.Contains(".dashboard-overview.dashboard__body", css);
        Assert.Contains(".dashboard-overview .workspace-selection__header h1", css);
        Assert.Contains("@media (max-width: 540px)", css);
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
