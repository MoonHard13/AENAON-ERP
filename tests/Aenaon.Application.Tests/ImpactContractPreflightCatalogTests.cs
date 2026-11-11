using Aenaon.Application.Fiscal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ελέγχει την πιστή, μη δεσμευτική κλιμάκωση των προφίλ τεκμηρίωσης IMPACT.</summary>
[TestClass]
public sealed class ImpactContractPreflightCatalogTests
{
    private readonly ImpactContractPreflightCatalog catalog = new();

    [TestMethod]
    public void Base_ListsOnlyPublishedMinimumRequirements()
    {
        ImpactContractPreflightReport report = catalog.Build(ImpactContractProfile.Base);

        Assert.AreEqual(ImpactContractProfile.Base, report.Profile);
        Assert.AreEqual("2026-11-11", report.DocumentationSnapshotDate);
        Assert.AreEqual(10, report.Requirements.Count);
        Assert.AreEqual(10, report.RequiredCount);
        Assert.AreEqual(0, report.ConditionalCount);
        Assert.AreEqual(0, report.ManualReviewCount);
        Assert.IsTrue(report.Requirements.Any(field =>
            field.PublishedField == "distributionDetails.internalDocumentId"));
        Assert.IsTrue(report.Requirements.Any(field =>
            field.PublishedField == "Details[at least one detail line]"));
        Assert.IsTrue(report.Requirements.All(field =>
            field.SourceSection == "1.3.2.1" &&
            field.SourceUrl.StartsWith("https://developers.s1ecos.com/", StringComparison.Ordinal)));
        AssertNeverApproved(report);
    }

    [TestMethod]
    public void Invoice_AddsConditionalCounterpartyCorrelationsAndTaxReview()
    {
        ImpactContractPreflightReport report = catalog.Build(ImpactContractProfile.InvoiceIapr);

        Assert.IsTrue(report.Requirements.Count > 20);
        Assert.IsTrue(report.ConditionalCount > 30);
        Assert.IsTrue(report.Requirements.Any(field =>
            field.PublishedField == "CorrelatedInvoices" &&
            field.Kind == ImpactContractRequirementKind.Conditional &&
            field.Applicability.Contains("τύπος παραστατικού", StringComparison.Ordinal)));
        Assert.IsTrue(report.Requirements.Any(field =>
            field.PublishedField == "CounterParty.Vat" &&
            field.Kind == ImpactContractRequirementKind.Conditional &&
            field.Applicability.Contains("λιανικών", StringComparison.Ordinal)));
        Assert.IsTrue(report.Requirements.Any(field =>
            field.PublishedField == "Detail.MeasurementUnitCodeEN" &&
            field.Kind == ImpactContractRequirementKind.Conditional));
        Assert.IsTrue(report.Requirements.Any(field =>
            field.PublishedField == "Detail.IncomeClassification.Classification.TypeCode"));
        Assert.IsTrue(report.ReviewGates.Any(gate =>
            gate.Name == "VatAndTaxMapping" &&
            gate.SourceUrl.Contains("vat-tax-codes", StringComparison.Ordinal)));
        Assert.IsTrue(report.ReviewGates.Any(gate =>
            gate.Name == "TaxPlacement"));
        AssertAllEntriesUnreviewed(report);
        AssertNeverApproved(report);
    }

    [TestMethod]
    public void B2G_IsSupersetAndPromotesEnglishUnitToRequired()
    {
        ImpactContractPreflightReport invoice = catalog.Build(ImpactContractProfile.InvoiceIapr);
        ImpactContractPreflightReport b2g = catalog.Build(ImpactContractProfile.InvoiceB2G);

        foreach (ImpactContractRequirement requirement in invoice.Requirements)
        {
            Assert.IsTrue(b2g.Requirements.Any(item =>
                item.PublishedField == requirement.PublishedField));
        }

        Assert.AreEqual(invoice.Requirements.Count + 4, b2g.Requirements.Count);
        Assert.AreEqual(invoice.RequiredCount + 5, b2g.RequiredCount);
        Assert.AreEqual(invoice.ConditionalCount - 1, b2g.ConditionalCount);
        foreach (string expected in new[]
        {
            "Detail.MeasurementUnitCodeEN",
            "Detail.CpvCode",
            "B2GDetails.ContractingAuthorityCode",
            "B2GDetails.ContractReferenceCode",
            "B2GDetails.ContractTypeCode"
        })
        {
            Assert.IsTrue(b2g.Requirements.Any(item =>
                item.PublishedField == expected &&
                item.Kind == ImpactContractRequirementKind.Required &&
                item.SourceSection == "1.3.2.3"));
        }

        Assert.IsTrue(b2g.ReviewGates.Any(gate => gate.Name == "B2GEndToEnd"));
        AssertAllEntriesUnreviewed(b2g);
        AssertNeverApproved(b2g);
    }

    [TestMethod]
    public void AllProfiles_HaveUniqueFieldsAndOfficialSectionProvenance()
    {
        foreach (ImpactContractProfile profile in Enum.GetValues<ImpactContractProfile>())
        {
            ImpactContractPreflightReport report = catalog.Build(profile);

            Assert.AreEqual(report.Requirements.Count,
                report.Requirements.Select(item => item.PublishedField)
                    .Distinct(StringComparer.Ordinal).Count());
            Assert.IsTrue(report.Requirements.All(field =>
                field.SourceSection.StartsWith("1.3.2.", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(field.ReviewQuestion) &&
                !string.IsNullOrWhiteSpace(field.Applicability)));
            Assert.IsTrue(report.ReviewGates.All(gate =>
                Uri.TryCreate(gate.SourceUrl, UriKind.Absolute, out Uri? uri) &&
                uri.Scheme == Uri.UriSchemeHttps));
            AssertNeverApproved(report);
        }
    }

    [TestMethod]
    public void UnrecognizedProfile_FailsClosed()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            catalog.Build((ImpactContractProfile)999));
    }

    private static void AssertNeverApproved(ImpactContractPreflightReport report)
    {
        Assert.IsFalse(report.IsActualPayloadValidated);
        Assert.IsFalse(report.IsAadeComplianceApproved);
        Assert.IsFalse(report.IsImpactContractValidated);
        Assert.IsFalse(report.CanIssueFiscalDocument);
    }

    private static void AssertAllEntriesUnreviewed(ImpactContractPreflightReport report)
    {
        Assert.IsTrue(report.Requirements.All(item =>
            !item.IsReviewed && !item.IsPayloadValidated));
        Assert.IsTrue(report.ReviewGates.All(item => !item.IsReviewed));
    }
}
