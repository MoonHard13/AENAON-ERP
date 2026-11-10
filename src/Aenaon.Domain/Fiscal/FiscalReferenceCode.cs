using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Fiscal;

/// <summary>Αμετάβλητη αναφορά κωδικού με πηγή, έκδοση και χρονική ισχύ.</summary>
public sealed record FiscalReferenceCode
{
    public const int MaxCodeLength = 64;
    public const int MaxSourceVersionLength = 80;

    private FiscalReferenceCode(FiscalReferenceSource source, FiscalCodeSystem system, string code,
        string sourceVersion, DateOnly effectiveFrom, DateOnly? effectiveUntil)
    {
        Source = source;
        System = system;
        Code = code;
        SourceVersion = sourceVersion;
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
    }

    public FiscalReferenceSource Source { get; }
    public FiscalCodeSystem System { get; }
    public string Code { get; }
    public string SourceVersion { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveUntil { get; }

    public static FiscalReferenceCode Create(FiscalReferenceSource source, FiscalCodeSystem system,
        string? code, string? sourceVersion, DateOnly effectiveFrom, DateOnly? effectiveUntil = null)
    {
        if (!Enum.IsDefined(source) || !Enum.IsDefined(system))
        {
            throw new DomainRuleException("fiscal.reference.system.invalid",
                "Απαιτούνται έγκυρη πηγή και έγκυρο σύστημα φορολογικού κωδικού.");
        }

        string normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedCode.Length == 0 || normalizedCode.Length > MaxCodeLength ||
            normalizedCode.Any(char.IsWhiteSpace) || normalizedCode.Any(char.IsControl))
        {
            throw new DomainRuleException("fiscal.reference.code.invalid",
                "Ο φορολογικός κωδικός πρέπει να είναι μη κενός, χωρίς κενά ή χαρακτήρες ελέγχου.");
        }

        string version = (sourceVersion ?? string.Empty).Trim();
        if (version.Length == 0 || version.Length > MaxSourceVersionLength ||
            version.Any(char.IsControl))
        {
            throw new DomainRuleException("fiscal.reference.version.invalid",
                "Απαιτείται έκδοση τεκμηρίωσης χωρίς χαρακτήρες ελέγχου.");
        }

        if (effectiveFrom == default || (effectiveUntil is DateOnly until && until < effectiveFrom))
        {
            throw new DomainRuleException("fiscal.reference.period.invalid",
                "Το διάστημα ισχύος του φορολογικού κωδικού δεν είναι έγκυρο.");
        }

        return new FiscalReferenceCode(source, system, normalizedCode, version, effectiveFrom, effectiveUntil);
    }

    public bool IsEffectiveOn(DateOnly date)
    {
        return date >= EffectiveFrom && (EffectiveUntil is null || date <= EffectiveUntil.Value);
    }
}
