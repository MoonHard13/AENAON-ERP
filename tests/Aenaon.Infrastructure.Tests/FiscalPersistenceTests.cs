using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;
using Aenaon.Infrastructure.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Δοκιμές εγκυρότητας των στοιχείων τεκμηρίωσης χωρίς εξωτερικές υπηρεσίες.</summary>
[TestClass]
public sealed class FiscalPersistenceTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [TestMethod]
    public void Registry_PreservesVersionAndOfficialSourceEvidence()
    {
        var reference = FiscalReferenceCode.Create(FiscalReferenceSource.Impact,
            FiscalCodeSystem.MyDataVatCategory, "SYNTHETIC-TEST", "CI-ONLY", Start);
        var record = FiscalReferenceRecord.Create(reference, "Μόνο για δοκιμές",
            "https://developers.s1ecos.com/vat-tax-codes-869826m0",
            new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(FiscalReferenceSource.Impact, record.Source);
        Assert.AreEqual("CI-ONLY", record.ToReference().SourceVersion);
        Assert.AreEqual(FiscalCodeSystem.MyDataVatCategory, record.ToReference().System);
        Assert.AreEqual(Start, record.EffectiveFrom);
    }

    [TestMethod]
    public void Registry_RejectsMissingOrUnsafeEvidence()
    {
        var reference = FiscalReferenceCode.Create(FiscalReferenceSource.Aade,
            FiscalCodeSystem.MyDataIncomeType, "TEST", "CI-ONLY", Start);
        var failure = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalReferenceRecord.Create(reference, " ", "http://example.test", DateTimeOffset.UtcNow));
        Assert.AreEqual("fiscal.registry.evidence.invalid", failure.Code);

        var timezone = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalReferenceRecord.Create(reference, "Τεκμηρίωση", "https://www.aade.gr/mydata",
                new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.FromHours(2))));
        Assert.AreEqual("fiscal.registry.evidence.invalid", timezone.Code);
    }

    [TestMethod]
    public void Mapping_RejectsInvalidIdentifiersAndIntervals()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalTaxMappingRecord.Create(Guid.Empty, Guid.NewGuid(), FiscalCustomerChannel.B2C,
                Start, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.AreEqual("fiscal.mapping.invalid", error.Code);

        var period = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalTaxMappingRecord.Create(Guid.NewGuid(), Guid.NewGuid(), FiscalCustomerChannel.B2B,
                Start, Start.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.AreEqual("fiscal.mapping.invalid", period.Code);
    }
}
