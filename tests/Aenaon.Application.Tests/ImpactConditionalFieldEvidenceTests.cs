using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ασφαλής καταγραφή παρουσίας υπό όρους πεδίων χωρίς αυτόματη φορολογική απόφαση.</summary>
[TestClass]
public sealed class ImpactConditionalFieldEvidenceTests
{
    private readonly ImpactConditionalFieldEvidenceAnalyzer analyzer = new();

    [TestMethod]
    public void InvoiceCatalog_ObservesAll37DocumentedConditionalFieldsWithoutApprovals()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {
              "CounterParty": { "Vat": "SYNTHETIC", "RegisteredName": "TEST" },
              "Details": [
                { "MeasurementUnitCode": 1, "VatExemptionCategoryCode": null },
                { "MeasurementUnitCode": null, "ShipType": { "ApplicationId": "TEST" } }
              ],
              "Taxes": [ { "TaxTypeCode": 1 }, {} ]
            }
            """);

        IReadOnlyList<ImpactConditionalFieldEvidence> result = analyzer.Analyze(
            document.RootElement, ImpactContractProfile.InvoiceIapr);

        Assert.AreEqual(37, result.Count);
        ImpactConditionalFieldEvidence unit = Find(result, "Detail.MeasurementUnitCode");
        Assert.AreEqual(2, unit.LocationsExamined);
        Assert.AreEqual(1, unit.ObservedNonNull);
        Assert.AreEqual(1, unit.ObservedNull);
        Assert.AreEqual(0, unit.NotObserved);

        ImpactConditionalFieldEvidence exemption = Find(result, "Detail.VatExemptionCategoryCode");
        Assert.AreEqual(1, exemption.ObservedNull);
        Assert.AreEqual(1, exemption.NotObserved);

        ImpactConditionalFieldEvidence ship = Find(result, "Detail.ShipType.ApplicationId");
        Assert.AreEqual(1, ship.ObservedNonNull);
        Assert.AreEqual(1, ship.NotObserved);

        ImpactConditionalFieldEvidence taxType = Find(result, "Taxes.TaxTypeCode");
        Assert.AreEqual(2, taxType.LocationsExamined);
        Assert.AreEqual(1, taxType.ObservedNonNull);
        Assert.AreEqual(1, taxType.NotObserved);

        ImpactConditionalFieldEvidence correlated = Find(result, "CorrelatedInvoices");
        Assert.AreEqual(1, correlated.LocationsExamined);
        Assert.AreEqual(0, correlated.ObservedNonNull);
        Assert.AreEqual(1, correlated.NotObserved);

        Assert.IsTrue(result.All(item => !item.IsApplicabilityDetermined &&
            !item.IsFiscalRuleApproved && !item.IsProviderValidated &&
            item.SourceUrl.StartsWith("https://developers.s1ecos.com/", StringComparison.Ordinal)));
        string exported = JsonSerializer.Serialize(result);
        Assert.IsFalse(exported.Contains("SYNTHETIC", StringComparison.Ordinal));
        Assert.IsFalse(exported.Contains("TEST", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BaseAndB2G_UseCorrectCatalogWithoutPromotingUnknownApplicability()
    {
        using JsonDocument document = JsonDocument.Parse(
            """{"Details":[{"MeasurementUnitCodeEN":"C62"}]}""");

        Assert.AreEqual(0, analyzer.Analyze(
            document.RootElement, ImpactContractProfile.Base).Count);

        IReadOnlyList<ImpactConditionalFieldEvidence> b2g = analyzer.Analyze(
            document.RootElement, ImpactContractProfile.InvoiceB2G);
        Assert.AreEqual(36, b2g.Count);
        Assert.IsFalse(b2g.Any(item =>
            item.PublishedField == "Detail.MeasurementUnitCodeEN"));
        Assert.IsTrue(b2g.All(item => !item.IsApplicabilityDetermined));
    }

    [TestMethod]
    public void AbsentOptionalCollections_AreUnknownNotApprovedOrRequired()
    {
        using JsonDocument document = JsonDocument.Parse("{}");

        IReadOnlyList<ImpactConditionalFieldEvidence> fields = analyzer.Analyze(
            document.RootElement, ImpactContractProfile.InvoiceIapr);

        ImpactConditionalFieldEvidence taxes = Find(fields, "Taxes.TaxAmount");
        Assert.AreEqual(0, taxes.LocationsExamined);
        Assert.AreEqual(0, taxes.ObservedNonNull);
        Assert.AreEqual(0, taxes.NotObserved);
        Assert.IsFalse(taxes.IsApplicabilityDetermined);

        ImpactConditionalFieldEvidence recipient = Find(fields, "Recipient.Address.City");
        Assert.AreEqual(1, recipient.LocationsExamined);
        Assert.AreEqual(1, recipient.NotObserved);
        Assert.IsFalse(recipient.IsFiscalRuleApproved);
    }

    [TestMethod]
    public void InvalidTaxesContainer_IsFlaggedForHumanReviewWithoutParsingItsValues()
    {
        using JsonDocument document = JsonDocument.Parse(
            """{"Taxes":{"TaxTypeCode":1},"Details":[null,{}]}""");

        IReadOnlyList<ImpactConditionalFieldEvidence> fields = analyzer.Analyze(
            document.RootElement, ImpactContractProfile.InvoiceIapr);

        Assert.IsTrue(Find(fields, "Taxes.TaxTypeCode").ContainerShapeNeedsReview);
        Assert.IsTrue(Find(fields, "Detail.MeasurementUnitCode").ContainerShapeNeedsReview);
    }

    [TestMethod]
    public void Inspector_ReportsConditionalPresenceButDoesNotUpgradeReviewToApproval()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("""
            {
              "DocumentTypeCode":"INVOICE",
              "DateIssued":"2026-11-12T09:00:00",
              "Series":"TEST",
              "Number":"1",
              "CurrencyCode":"EUR",
              "InvoiceTypeCode":"1.1",
              "Issuer":{"Vat":"EL000000000","RegisteredName":"SYNTHETIC",
                        "Address":{"CountryCode":"GR"}},
              "CounterParty":{"Vat":"EL000000000","RegisteredName":"SYNTHETIC",
                              "Address":{"CountryCode":"GR"}},
              "DistributionDetails":{"InternalDocumentId":"TEST-1"},
              "Details":[{"LineNo":1,"UnitPrice":100,"NetTotal":100,
                          "VatCategoryCode":1,"VATTotal":24,
                          "IncomeClassification":{"ClassificationCategoryCode":"category1_1",
                              "ClassificationTypeCode":"E3_561_001","Amount":100}}],
              "Summaries":{"TotalNetAmount":100,"TotalVATAmount":24,"TotalGrossValue":124}
            }
            """);

        ImpactOfflinePayloadReport report = new ImpactOfflinePayloadInspector().Inspect(
            bytes, ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);

        Assert.AreEqual(0, report.StructuralErrorCount);
        Assert.AreEqual(6, report.PendingExpertReviewCount);
        Assert.AreEqual(37, report.ConditionalEvidence.Count);
        Assert.AreEqual(1, Find(report.ConditionalEvidence, "CounterParty.Vat").ObservedNonNull);
        Assert.AreEqual(1, Find(report.ConditionalEvidence, "Detail.MeasurementUnitCode").NotObserved);
        Assert.IsFalse(report.CanIssueFiscalDocument);
        Assert.IsFalse(report.IsFiscalRuleApproved);
        Assert.IsFalse(report.IsActualProviderContractValidated);
    }

    [TestMethod]
    public void InvalidProfileOrNonObject_FailsClosed()
    {
        using JsonDocument obj = JsonDocument.Parse("{}");
        using JsonDocument array = JsonDocument.Parse("[]");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            analyzer.Analyze(obj.RootElement, (ImpactContractProfile)997));
        Assert.ThrowsExactly<JsonException>(() =>
            analyzer.Analyze(array.RootElement, ImpactContractProfile.InvoiceIapr));
    }

    private static ImpactConditionalFieldEvidence Find(
        IReadOnlyList<ImpactConditionalFieldEvidence> evidence, string field) =>
        evidence.Single(item => item.PublishedField == field);
}
