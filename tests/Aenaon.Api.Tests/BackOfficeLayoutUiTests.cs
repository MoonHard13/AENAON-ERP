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
