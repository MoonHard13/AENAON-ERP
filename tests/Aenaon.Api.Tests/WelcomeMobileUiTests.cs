using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Έλεγχος ότι η δημόσια πρώτη οθόνη διατηρεί τη σύνδεση και το mobile stylesheet.</summary>
[TestClass]
public sealed class WelcomeMobileUiTests
{
    [TestMethod]
    public async Task AnonymousWelcome_HasBrandingLoginLinkAndMobileLayoutStyles()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using HttpResponseMessage page = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync();

        Assert.Contains("class=\"welcome-shell\"", html);
        Assert.Contains("class=\"welcome-card\"", html);
        Assert.Contains("class=\"welcome-card__brand\"", html);
        Assert.Contains("/images/aenaon-logo-light.webp", html);
        Assert.Contains("/images/aenaon-logo-dark.webp", html);
        Assert.Contains("Ένας χώρος για κάθε επιχείρηση.", html);
        Assert.Contains("href=\"/login\"", html);

        using HttpResponseMessage cssResponse = await client.GetAsync("/css/aenaon.css");
        Assert.AreEqual(HttpStatusCode.OK, cssResponse.StatusCode);
        string css = await cssResponse.Content.ReadAsStringAsync();
        Assert.Contains("Mobile landing: συμπαγής πρώτη οθόνη", css);
        Assert.Contains(".welcome-shell", css);
        Assert.Contains("justify-content: flex-start", css);
        Assert.Contains(".welcome-card > .eyebrow { display: none; }", css);
        Assert.Contains("white-space: normal", css);
        Assert.Contains("@media (max-width: 650px)", css);
    }
}
