using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

[TestClass]
public sealed class FiscalDomainTests
{
    private static readonly DateOnly ValidFrom = new(2026, 1, 1);

    private static FiscalReferenceCode Code(FiscalCodeSystem system, string raw = "SYNTHETIC-TEST",
        DateOnly? from = null, DateOnly? until = null)
    {
        // Οι τεχνητοί κωδικοί αποτελούν αποκλειστικά fixtures και δεν είναι φορολογικές τιμές.
        return FiscalReferenceCode.Create(FiscalReferenceSource.Impact, system, raw,
            "TEST-CONTRACT-2026", from ?? ValidFrom, until);
    }

    private static FiscalProfileCandidate Candidate(Guid? tenantId = null, Guid? itemId = null,
        FiscalReferenceCode? vatCategory = null,
        FiscalReferenceCode? incomeCategory = null, FiscalReferenceCode? incomeType = null,
        FiscalReferenceCode? exemption = null, FiscalReferenceCode? unit = null,
        FiscalReferenceCode? unitEn = null)
    {
        return FiscalProfileCandidate.Create(
            tenantId ?? Guid.NewGuid(), itemId ?? Guid.NewGuid(),
            ValidFrom, null,
            vatCategory ?? Code(FiscalCodeSystem.MyDataVatCategory),
            incomeCategory ?? Code(FiscalCodeSystem.MyDataIncomeCategory),
            incomeType ?? Code(FiscalCodeSystem.MyDataIncomeType),
            exemption, unit, unitEn);
    }

    private static FiscalTransactionContext Context(Guid tenantId, Guid itemId,
        DateOnly? date = null, FiscalOperationKind operation = FiscalOperationKind.FiscalDocument)
    {
        return FiscalTransactionContext.Create(tenantId, Guid.NewGuid(), itemId,
            CatalogItemKind.Product, date ?? ValidFrom,
            FiscalCustomerChannel.B2C, operation);
    }

    [TestMethod]
    public void ReferenceCode_IsImmutableNormalizedAndVersioned()
    {
        var reference = FiscalReferenceCode.Create(FiscalReferenceSource.Impact,
            FiscalCodeSystem.MyDataIncomeCategory, "  test_code_1 ", "  test-v1  ",
            ValidFrom, new DateOnly(2026, 12, 31));

        Assert.AreEqual("TEST_CODE_1", reference.Code);
        Assert.AreEqual("test-v1", reference.SourceVersion);
        Assert.AreEqual(FiscalReferenceSource.Impact, reference.Source);
        Assert.AreEqual(FiscalCodeSystem.MyDataIncomeCategory, reference.System);
        Assert.IsFalse(reference.IsEffectiveOn(new DateOnly(2025, 12, 31)));
        Assert.IsTrue(reference.IsEffectiveOn(ValidFrom));
        Assert.IsTrue(reference.IsEffectiveOn(new DateOnly(2026, 12, 31)));
        Assert.IsFalse(reference.IsEffectiveOn(new DateOnly(2027, 1, 1)));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("BAD CODE")]
    [DataRow("BAD\nCODE")]
    public void ReferenceCode_RejectsInvalidCodes(string? input)
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() => FiscalReferenceCode.Create(
            FiscalReferenceSource.Aade, FiscalCodeSystem.MyDataVatCategory,
            input, "vTEST", ValidFrom));
        Assert.AreEqual("fiscal.reference.code.invalid", error.Code);
    }

    [TestMethod]
    public void ReferenceCode_RejectsCodesAboveLengthLimit()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() => FiscalReferenceCode.Create(
            FiscalReferenceSource.Aade, FiscalCodeSystem.MyDataVatCategory,
            new string('A', FiscalReferenceCode.MaxCodeLength + 1), "vTEST", ValidFrom));
        Assert.AreEqual("fiscal.reference.code.invalid", error.Code);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("bad\nversion")]
    public void ReferenceCode_RequiresSourceVersion(string? version)
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalReferenceCode.Create(FiscalReferenceSource.Impact, FiscalCodeSystem.MyDataVatCategory,
                "TEST", version, ValidFrom));
        Assert.AreEqual("fiscal.reference.version.invalid", error.Code);
    }

    [TestMethod]
    public void ReferenceCode_RejectsUnknownSourceOrSystem()
    {
        var sourceError = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalReferenceCode.Create((FiscalReferenceSource)999, FiscalCodeSystem.MyDataVatCategory,
                "TEST", "vTEST", ValidFrom));
        var systemError = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalReferenceCode.Create(FiscalReferenceSource.Impact, (FiscalCodeSystem)999,
                "TEST", "vTEST", ValidFrom));

        Assert.AreEqual("fiscal.reference.system.invalid", sourceError.Code);
        Assert.AreEqual("fiscal.reference.system.invalid", systemError.Code);
    }

    [TestMethod]
    public void ReferenceCode_RejectsInvertedPeriod()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalReferenceCode.Create(FiscalReferenceSource.Impact, FiscalCodeSystem.MyDataVatCategory,
                "TEST", "vTEST", new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 30)));
        Assert.AreEqual("fiscal.reference.period.invalid", error.Code);
    }

    [TestMethod]
    public void ReferenceCodes_DifferentSystemsAreNotEquivalent()
    {
        var vat = Code(FiscalCodeSystem.MyDataVatCategory, "1");
        var unit = Code(FiscalCodeSystem.MyDataMeasurementUnit, "1");
        var differentSource = FiscalReferenceCode.Create(FiscalReferenceSource.Aade,
            FiscalCodeSystem.MyDataVatCategory, "1", vat.SourceVersion, ValidFrom);

        Assert.AreNotEqual(vat, unit);
        Assert.AreNotEqual(vat, differentSource);
    }

    [TestMethod]
    public void Context_DistinguishesOrderFromFiscalDocument()
    {
        Guid tenantId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();
        var order = Context(tenantId, itemId, operation: FiscalOperationKind.Order);
        var document = Context(tenantId, itemId);

        Assert.AreEqual(FiscalOperationKind.Order, order.OperationKind);
        Assert.AreEqual(FiscalOperationKind.FiscalDocument, document.OperationKind);
        Assert.AreEqual(tenantId, document.TenantId);
        Assert.AreEqual(itemId, document.CatalogItemId);
        Assert.AreNotEqual(Guid.Empty, document.StoreId);
    }

    [TestMethod]
    public void Context_RejectsEmptyIdentifiersAndUnknownValues()
    {
        var identityError = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalTransactionContext.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
                CatalogItemKind.Product, ValidFrom, FiscalCustomerChannel.B2C,
                FiscalOperationKind.FiscalDocument));
        Assert.AreEqual("fiscal.context.identity.required", identityError.Code);

        var enumError = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalTransactionContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                CatalogItemKind.Product, ValidFrom, (FiscalCustomerChannel)999,
                FiscalOperationKind.FiscalDocument));
        Assert.AreEqual("fiscal.context.invalid", enumError.Code);

        var dateError = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalTransactionContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                CatalogItemKind.Product, default, FiscalCustomerChannel.B2C,
                FiscalOperationKind.FiscalDocument));
        Assert.AreEqual("fiscal.context.invalid", dateError.Code);
    }

    [TestMethod]
    public void Candidate_SeparatesVatAndClassificationCodeSystems()
    {
        var candidate = Candidate(
            exemption: Code(FiscalCodeSystem.MyDataVatExemption),
            unit: Code(FiscalCodeSystem.MyDataMeasurementUnit),
            unitEn: Code(FiscalCodeSystem.En16931MeasurementUnit));

        Assert.AreEqual(FiscalCodeSystem.MyDataVatCategory, candidate.VatCategory.System);
        Assert.AreEqual(FiscalCodeSystem.MyDataVatExemption, candidate.VatExemption!.System);
        Assert.AreEqual(FiscalCodeSystem.MyDataIncomeCategory, candidate.IncomeCategory.System);
        Assert.AreEqual(FiscalCodeSystem.MyDataIncomeType, candidate.IncomeType.System);
        Assert.AreEqual(FiscalCodeSystem.MyDataMeasurementUnit, candidate.MeasurementUnit!.System);
        Assert.AreEqual(FiscalCodeSystem.En16931MeasurementUnit, candidate.MeasurementUnitEn!.System);
    }

    [TestMethod]
    public void Candidate_RejectsSwappedVatAndUnitCode()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            Candidate(vatCategory: Code(FiscalCodeSystem.MyDataMeasurementUnit)));
        Assert.AreEqual("fiscal.profile.reference.system.invalid", error.Code);

        var optionalError = Assert.ThrowsExactly<DomainRuleException>(() =>
            Candidate(exemption: Code(FiscalCodeSystem.MyDataIncomeType)));
        Assert.AreEqual("fiscal.profile.reference.system.invalid", optionalError.Code);
    }

    [TestMethod]
    public void Candidate_RequiresMandatoryReferences()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalProfileCandidate.Create(Guid.NewGuid(), Guid.NewGuid(), ValidFrom, null,
                Code(FiscalCodeSystem.MyDataVatCategory), null,
                Code(FiscalCodeSystem.MyDataIncomeType)));
        Assert.AreEqual("fiscal.profile.reference.system.invalid", error.Code);
    }

    [TestMethod]
    public void Candidate_RejectsEmptyIdentityAndInvalidPeriod()
    {
        var identityError = Assert.ThrowsExactly<DomainRuleException>(() =>
            Candidate(tenantId: Guid.Empty));
        Assert.AreEqual("fiscal.profile.identity.required", identityError.Code);

        var periodError = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalProfileCandidate.Create(Guid.NewGuid(), Guid.NewGuid(),
                new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 31),
                Code(FiscalCodeSystem.MyDataVatCategory),
                Code(FiscalCodeSystem.MyDataIncomeCategory),
                Code(FiscalCodeSystem.MyDataIncomeType)));
        Assert.AreEqual("fiscal.profile.period.invalid", periodError.Code);
    }

    [TestMethod]
    public void Candidate_RejectsForeignTenantOrItemWhenCheckingScope()
    {
        Guid tenantId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();
        var candidate = Candidate(tenantId, itemId);

        var foreignTenant = Assert.ThrowsExactly<DomainRuleException>(() =>
            candidate.ValidateScope(Context(Guid.NewGuid(), itemId)));
        var foreignItem = Assert.ThrowsExactly<DomainRuleException>(() =>
            candidate.ValidateScope(Context(tenantId, Guid.NewGuid())));

        Assert.AreEqual("fiscal.profile.scope.mismatch", foreignTenant.Code);
        Assert.AreEqual("fiscal.profile.scope.mismatch", foreignItem.Code);
    }

    [TestMethod]
    public void Candidate_RefusesToTreatRestaurantOrderAsFiscalDocument()
    {
        Guid tenantId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();
        var candidate = Candidate(tenantId, itemId);

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            candidate.ValidateScope(Context(tenantId, itemId, operation: FiscalOperationKind.Order)));

        Assert.AreEqual("fiscal.profile.order.not_fiscal", error.Code);
    }

    [TestMethod]
    public void Candidate_RejectsExpiredProfileOrReferenceCode()
    {
        Guid tenantId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();
        var expiredVat = Code(FiscalCodeSystem.MyDataVatCategory,
            from: ValidFrom, until: new DateOnly(2026, 6, 30));
        var candidate = Candidate(tenantId, itemId, vatCategory: expiredVat);

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            candidate.ValidateScope(Context(tenantId, itemId, new DateOnly(2026, 7, 1))));

        Assert.AreEqual("fiscal.profile.period.inactive", error.Code);

        var limited = FiscalProfileCandidate.Create(tenantId, itemId,
            ValidFrom, new DateOnly(2026, 5, 1),
            Code(FiscalCodeSystem.MyDataVatCategory),
            Code(FiscalCodeSystem.MyDataIncomeCategory),
            Code(FiscalCodeSystem.MyDataIncomeType));
        var profileError = Assert.ThrowsExactly<DomainRuleException>(() =>
            limited.ValidateScope(Context(tenantId, itemId, new DateOnly(2026, 5, 2))));
        Assert.AreEqual("fiscal.profile.period.inactive", profileError.Code);
    }

    [TestMethod]
    public void Candidate_AcceptsMatchingScopeButDoesNotCertifyTaxValidity()
    {
        Guid tenantId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();
        var candidate = Candidate(tenantId, itemId);

        // Ελέγχονται μόνο το πλαίσιο και οι ημερομηνίες, όχι ο νόμιμος συνδυασμός κωδικών.
        candidate.ValidateScope(Context(tenantId, itemId));
        Assert.AreEqual(tenantId, candidate.TenantId);
        Assert.AreEqual(itemId, candidate.CatalogItemId);
    }
}
