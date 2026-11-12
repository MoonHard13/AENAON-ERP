using System.Security.Cryptography;

namespace Aenaon.Api.Ui;

/// <summary>Παρέχει σταθερό αποτύπωμα περιεχομένου για την ασφαλή ανανέωση των στατικών αρχείων.</summary>
public sealed class UiAssetVersions
{
    public string Css { get; }
    public string BackOfficeCss { get; }
    public string JavaScript { get; }
    public string ThemeInitialization { get; }
    public string Favicon { get; }

    public UiAssetVersions(IWebHostEnvironment environment, ILogger<UiAssetVersions> logger)
    {
        Css = Fingerprint(environment, logger, "css/aenaon.css");
        BackOfficeCss = Fingerprint(environment, logger, "css/aenaon-backoffice.css");
        JavaScript = Fingerprint(environment, logger, "js/aenaon.js");
        ThemeInitialization = Fingerprint(environment, logger, "js/aenaon-theme-init.js");
        Favicon = Fingerprint(environment, logger, "images/aenaon-favicon.png");
    }

    private static string Fingerprint(
        IWebHostEnvironment environment,
        ILogger<UiAssetVersions> logger,
        string relativePath)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(relativePath);
        if (!file.Exists)
        {
            // Η προειδοποίηση δεν περιλαμβάνει filesystem paths ή ευαίσθητες πληροφορίες.
            logger.LogWarning("UI static asset is missing: {Asset}", relativePath);
            return "missing";
        }

        using var stream = file.CreateReadStream();
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
