using System.Buffers.Binary;
using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Api.Tests;

/// <summary>Επαληθεύει ότι οι δύο πραγματικές εικόνες έχουν κοινές διαστάσεις και εξυπηρετούνται σωστά.</summary>
[TestClass]
public sealed class BrandingConsistencyTests
{
    [TestMethod]
    public async Task OfficialLightAndDarkWebP_UseIdentical960SquareCanvases()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        (int Width, int Height) dark = await ReadDimensionsAsync(client,
            "/images/aenaon-logo-dark.webp");
        (int Width, int Height) light = await ReadDimensionsAsync(client,
            "/images/aenaon-logo-light.webp");

        Assert.AreEqual((960, 960), dark);
        Assert.AreEqual(dark, light);
    }

    [TestMethod]
    public async Task LoginReferencesBothOfficialThemeAwareLogos()
    {
        await using var factory = new SessionApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/login");
        string html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"logo-for-dark\"", html);
        Assert.Contains("class=\"logo-for-light\"", html);
        Assert.Contains("/images/aenaon-logo-dark.webp", html);
        Assert.Contains("/images/aenaon-logo-light.webp", html);
    }

    private static async Task<(int Width, int Height)> ReadDimensionsAsync(
        HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("image/webp", response.Content.Headers.ContentType?.MediaType);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.IsTrue(bytes.Length >= 30, "Το αρχείο WebP δεν διαθέτει έγκυρη κεφαλίδα.");
        Assert.AreEqual("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.AreEqual("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.AreEqual("VP8X", Encoding.ASCII.GetString(bytes, 12, 4));

        // Το VP8X αποθηκεύει 24-bit πλάτος και ύψος ως τιμές μείον ένα.
        int width = bytes[24] | (bytes[25] << 8) | (bytes[26] << 16);
        int height = bytes[27] | (bytes[28] << 8) | (bytes[29] << 16);
        return (width + 1, height + 1);
    }
}
