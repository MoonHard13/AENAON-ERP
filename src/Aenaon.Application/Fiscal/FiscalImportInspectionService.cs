using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;

namespace Aenaon.Application.Fiscal;

/// <summary>Δομημένο υποψήφιο αρχείο κωδικών, χωριστό από το πρωτότυπο τεκμήριο.</summary>
public sealed class FiscalImportManifest
{
    public string Source { get; init; } = string.Empty;
    public string CodeSystem { get; init; } = string.Empty;
    public string SourceVersion { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string ArtifactSha256 { get; init; } = string.Empty;
    public DateTimeOffset VerifiedAtUtc { get; init; }
    public List<FiscalImportRow>? Rows { get; init; }
}

/// <summary>Εγγραφή προς επιθεώρηση και όχι εγκεκριμένος φορολογικός κωδικός.</summary>
public sealed class FiscalImportRow
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveUntil { get; init; }
}

/// <summary>Αποτέλεσμα προεπισκόπησης που δεν επιτρέπει αλλαγές στη βάση ή φορολογική χρήση.</summary>
public sealed record FiscalImportInspection(
    FiscalReferenceSource Source,
    FiscalCodeSystem CodeSystem,
    string SourceVersion,
    string ArtifactSha256,
    int CandidateCount)
{
    public bool CanImportToDatabase => false;
    public bool IsTaxRuleApproved => false;
    public bool CanIssueFiscalDocument => false;
}

/// <summary>
/// Επιθεωρεί τοπικό manifest με πρωτότυπο αρχείο και SHA-256 χωρίς δικτυακή κλήση ή αποθήκευση.
/// Η συμφωνία hash δεν αποδεικνύει ότι η πηγή ή οι φορολογικοί συνδυασμοί είναι νόμιμοι.
/// </summary>
public sealed class FiscalImportInspectionService(TimeProvider timeProvider)
{
    private const long MaximumManifestBytes = 2 * 1024 * 1024;
    private const long MaximumArtifactBytes = 25 * 1024 * 1024;
    private const int MaximumRows = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<FiscalImportInspection> InspectAsync(string manifestPath, string artifactPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(manifestPath) || string.IsNullOrWhiteSpace(artifactPath))
        {
            throw Invalid("Απαιτούνται manifest και πρωτότυπο αρχείο τεκμηρίου.");
        }

        string manifestFullPath = Path.GetFullPath(manifestPath);
        string artifactFullPath = Path.GetFullPath(artifactPath);
        if (string.Equals(manifestFullPath, artifactFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("Το manifest πρέπει να είναι διαφορετικό αρχείο από το τεκμήριο.");
        }

        FileInfo manifestInfo = new(manifestFullPath);
        FileInfo artifactInfo = new(artifactFullPath);
        if (!manifestInfo.Exists || manifestInfo.Length is <= 0 or > MaximumManifestBytes ||
            !artifactInfo.Exists || artifactInfo.Length is <= 0 or > MaximumArtifactBytes)
        {
            throw Invalid("Λείπει κάποιο αρχείο ή παραβιάζει τα επιτρεπόμενα όρια μεγέθους.");
        }

        FiscalImportManifest manifest;
        await using (var stream = new FileStream(manifestFullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            try
            {
                manifest = await JsonSerializer.DeserializeAsync<FiscalImportManifest>(
                    stream, JsonOptions, cancellationToken)
                    ?? throw Invalid("Το αρχείο JSON είναι κενό.");
            }
            catch (JsonException)
            {
                throw Invalid("Το manifest δεν έχει έγκυρη και αναγνωρίσιμη δομή JSON.");
            }
        }

        if (!Enum.TryParse(manifest.Source, ignoreCase: false, out FiscalReferenceSource source) ||
            !Enum.IsDefined(source) || !Enum.TryParse(manifest.CodeSystem, ignoreCase: false,
                out FiscalCodeSystem system) || !Enum.IsDefined(system) ||
            !IsAllowedSystem(source, system))
        {
            throw Invalid("Η πηγή και το σύστημα κωδικών δεν αντιστοιχούν σε επιτρεπτό namespace.");
        }

        if (!Uri.TryCreate(manifest.SourceUrl, UriKind.Absolute, out Uri? sourceUri) ||
            sourceUri.Scheme != Uri.UriSchemeHttps || sourceUri.UserInfo.Length != 0 ||
            !IsTrustedHost(source, sourceUri.Host) ||
            sourceUri.Query.Length != 0 || sourceUri.Fragment.Length != 0)
        {
            throw Invalid("Απαιτείται συγκεκριμένη HTTPS σελίδα επίσημης πηγής.");
        }

        if (manifest.VerifiedAtUtc == default || manifest.VerifiedAtUtc.Offset != TimeSpan.Zero ||
            manifest.VerifiedAtUtc > timeProvider.GetUtcNow() ||
            manifest.Rows is not { Count: > 0 and <= MaximumRows } ||
            string.IsNullOrWhiteSpace(manifest.ArtifactSha256) ||
            manifest.ArtifactSha256.Length != 64 ||
            !manifest.ArtifactSha256.All(Uri.IsHexDigit))
        {
            throw Invalid("Απουσιάζουν έγκυρη UTC επαλήθευση, γραμμές ή αποτύπωμα SHA-256.");
        }

        // Υπολογίζουμε SHA-256 από τα πραγματικά bytes του αποθηκευμένου τεκμηρίου.
        string calculatedHash;
        await using (var sourceStream = new FileStream(artifactFullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            calculatedHash = Convert.ToHexString(await SHA256.HashDataAsync(sourceStream, cancellationToken));
        }

        if (!string.Equals(calculatedHash, manifest.ArtifactSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("fiscal.import.hash.mismatch",
                "Το SHA-256 του τεκμηρίου δεν συμφωνεί με το manifest.");
        }

        var intervals = new Dictionary<string, List<FiscalReferenceCode>>(StringComparer.Ordinal);
        foreach (FiscalImportRow? row in manifest.Rows)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.Description) ||
                row.Description.Length > 250 || row.Description.Any(char.IsControl))
            {
                throw Invalid("Απαιτείται ελεγμένη σύντομη περιγραφή σε κάθε γραμμή.");
            }

            FiscalReferenceCode reference = FiscalReferenceCode.Create(source, system, row.Code,
                manifest.SourceVersion, row.EffectiveFrom, row.EffectiveUntil);

            if (!intervals.TryGetValue(reference.Code, out List<FiscalReferenceCode>? existing))
            {
                existing = [];
                intervals.Add(reference.Code, existing);
            }

            // Ακόμη και για τον ίδιο κωδικό και version, δεν δεχόμαστε αλληλοεπικαλυπτόμενες περιόδους.
            foreach (FiscalReferenceCode previous in existing)
            {
                if ((previous.EffectiveUntil is null || previous.EffectiveUntil >= reference.EffectiveFrom) &&
                    (reference.EffectiveUntil is null || reference.EffectiveUntil >= previous.EffectiveFrom))
                {
                    throw new DomainRuleException("fiscal.import.period.overlap",
                        "Ένας κωδικός εμφανίζεται σε επικαλυπτόμενες περιόδους.");
                }
            }

            existing.Add(reference);
        }

        return new FiscalImportInspection(source, system, manifest.SourceVersion,
            calculatedHash, manifest.Rows.Count);
    }

    private static bool IsAllowedSystem(FiscalReferenceSource source, FiscalCodeSystem system) =>
        source switch
        {
            FiscalReferenceSource.Aade => system is not
                (FiscalCodeSystem.ImpactDocumentType or FiscalCodeSystem.En16931MeasurementUnit),
            FiscalReferenceSource.Impact => true,
            // Οι απευθείας πηγές EN16931 απαιτούν ξεχωριστή επιθεώρηση πριν ενεργοποιηθούν.
            FiscalReferenceSource.En16931 => false,
            _ => false
        };

    private static bool IsTrustedHost(FiscalReferenceSource source, string host) =>
        source switch
        {
            FiscalReferenceSource.Aade => host is "aade.gr" or "www.aade.gr",
            FiscalReferenceSource.Impact => host is "developers.s1ecos.com",
            _ => false
        };

    private static DomainRuleException Invalid(string message) =>
        new("fiscal.import.manifest.invalid", message);
}
