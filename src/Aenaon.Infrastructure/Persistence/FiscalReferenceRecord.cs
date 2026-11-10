using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Έκδοση κωδικού αναφοράς με σαφή προέλευση και ημερομηνία τεκμηρίωσης.</summary>
public sealed class FiscalReferenceRecord
{
    private FiscalReferenceRecord()
    {
        Code = string.Empty;
        SourceVersion = string.Empty;
        Description = string.Empty;
        SourceUrl = string.Empty;
    }

    public Guid Id { get; private set; }
    public FiscalReferenceSource Source { get; private set; }
    public FiscalCodeSystem CodeSystem { get; private set; }
    public string Code { get; private set; }
    public string SourceVersion { get; private set; }
    public string Description { get; private set; }
    public string SourceUrl { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveUntil { get; private set; }
    public DateTimeOffset VerifiedAtUtc { get; private set; }

    public static FiscalReferenceRecord Create(FiscalReferenceCode reference, string? description,
        string? sourceUrl, DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string label = (description ?? string.Empty).Trim();
        string url = (sourceUrl ?? string.Empty).Trim();
        if (label.Length is 0 or > 250 || label.Any(char.IsControl) ||
            url.Length is 0 or > 500 ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            verifiedAtUtc == default || verifiedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("fiscal.registry.evidence.invalid",
                "Απαιτούνται τεκμηρίωση, ασφαλής σύνδεσμος πηγής και UTC χρόνος ελέγχου.");
        }

        return new FiscalReferenceRecord
        {
            Id = Guid.NewGuid(),
            Source = reference.Source,
            CodeSystem = reference.System,
            Code = reference.Code,
            SourceVersion = reference.SourceVersion,
            EffectiveFrom = reference.EffectiveFrom,
            EffectiveUntil = reference.EffectiveUntil,
            Description = label,
            SourceUrl = url,
            VerifiedAtUtc = verifiedAtUtc
        };
    }

    /// <summary>Επιστρέφει την ίδια versioned αναφορά, χωρίς φορολογική πιστοποίηση.</summary>
    public FiscalReferenceCode ToReference() =>
        FiscalReferenceCode.Create(Source, CodeSystem, Code, SourceVersion, EffectiveFrom, EffectiveUntil);
}
