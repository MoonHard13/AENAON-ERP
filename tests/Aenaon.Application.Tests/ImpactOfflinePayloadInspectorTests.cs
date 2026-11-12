using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Δοκιμές offline IMPACT μόνο με συνθετικά, μη παραγωγικά JSON παραστατικά.</summary>
[TestClass]
public sealed class ImpactOfflinePayloadInspectorTests
{
    private readonly ImpactOfflinePayloadInspector inspector = new();

    [TestMethod]
    public void BaseSyntheticDocument_HasNoMissingDocumentedFieldsButIsNotApproved()
    {
        ImpactOfflinePayloadReport report = Inspect(BaseJson(), ImpactContractProfile.Base,
            ImpactCounterpartyScenario.Unknown);

        Assert.AreEqual(1, report.LineCount);
        Assert.AreEqual(0, report.StructuralErrorCount);
        Assert.IsTrue(report.PendingExpertReviewCount > 0);
        AssertAlwaysUnapproved(report);
    }

    [TestMethod]
    public void InvoiceBusinessSyntheticDocument_ChecksDocumentedRequiredPaths()
    {
        ImpactOfflinePayloadReport report = Inspect(InvoiceJson(),
            ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);

        Assert.AreEqual(0, report.StructuralErrorCount);
        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "IncomeClassificationShapeAmbiguous"));
        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "ClassificationMatrixPending"));
        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "VatAndTotalsPending"));
        AssertAlwaysUnapproved(report);
    }

    [TestMethod]
    public void InvoiceMissingAmount_IsFlaggedBecauseRequiredDocumentContradictsExample()
    {
        string original = InvoiceJson();
        // Σκόπιμη αφαίρεση μόνο του Amount από το συνθετικό δείγμα.
        string adjusted = original.Replace(",\"Amount\":100", "", StringComparison.Ordinal);
        ImpactOfflinePayloadReport report = Inspect(adjusted,
            ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);

        Assert.IsTrue(report.HasDetectedStructuralErrors);
        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "MissingDocumentedField" &&
            finding.JsonPath == "$.Details[0].IncomeClassification.Amount"));
        AssertAlwaysUnapproved(report);
    }

    [TestMethod]
    public void RetailScenario_DoesNotRequireCounterparty()
    {
        var payload = System.Text.Json.Nodes.JsonNode.Parse(InvoiceJson())!.AsObject();
        Assert.IsTrue(payload.Remove("CounterParty"));
        string retail = payload.ToJsonString();

        ImpactOfflinePayloadReport b2c = Inspect(retail, ImpactContractProfile.InvoiceIapr,
            ImpactCounterpartyScenario.Retail);
        ImpactOfflinePayloadReport b2b = Inspect(retail, ImpactContractProfile.InvoiceIapr,
            ImpactCounterpartyScenario.Business);
        ImpactOfflinePayloadReport unknown = Inspect(retail, ImpactContractProfile.InvoiceIapr,
            ImpactCounterpartyScenario.Unknown);

        Assert.AreEqual(0, b2c.StructuralErrorCount);
        Assert.IsTrue(b2b.Findings.Any(finding =>
            finding.JsonPath == "$.CounterParty.Vat" &&
            finding.Severity == ImpactOfflineFindingSeverity.Error));
        Assert.IsTrue(unknown.Findings.Any(finding =>
            finding.Code == "CounterpartyScenarioUnknown"));
        AssertAlwaysUnapproved(b2c);
    }

    [TestMethod]
    public void B2GSyntheticDocument_RequiresAdditionalFieldsPerLine()
    {
        string invoice = InvoiceJson().Replace(
            "\"VatTotal\":24", "\"VatTotal\":24,\"MeasurementUnitCodeEN\":\"C62\",\"CpvCode\":\"00000000\"",
            StringComparison.Ordinal)
            .Replace("\"Summaries\":{",
                "\"B2GDetails\":{\"ContractingAuthorityCode\":\"TEST\",\"ContractReferenceCode\":\"0\",\"ContractTypeCode\":\"3\"},\"Summaries\":{",
                StringComparison.Ordinal);
        ImpactOfflinePayloadReport b2g = Inspect(invoice, ImpactContractProfile.InvoiceB2G,
            ImpactCounterpartyScenario.Business);

        Assert.AreEqual(0, b2g.StructuralErrorCount);
        Assert.IsTrue(b2g.Findings.Any(finding => finding.Code == "B2GContractPending"));
        AssertAlwaysUnapproved(b2g);

        ImpactOfflinePayloadReport missingB2G = Inspect(InvoiceJson(),
            ImpactContractProfile.InvoiceB2G, ImpactCounterpartyScenario.Business);
        Assert.IsTrue(missingB2G.StructuralErrorCount >= 5);
    }

    [TestMethod]
    public void DocumentAndLineTaxesConflict_FailsClosed()
    {
        string taxed = InvoiceJson().Replace(
            "\"VatTotal\":24", "\"VatTotal\":24,\"WithheldAmount\":1",
            StringComparison.Ordinal).Replace(
            "\"Summaries\":{",
            "\"Taxes\":[{\"TaxTypeCode\":1,\"TaxAmount\":1}],\"Summaries\":{",
            StringComparison.Ordinal);

        ImpactOfflinePayloadReport report = Inspect(taxed,
            ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);

        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "PossibleTaxPlacementConflict" &&
            finding.Severity == ImpactOfflineFindingSeverity.Error));
        AssertAlwaysUnapproved(report);
    }

    [TestMethod]
    public void DuplicateCaseInsensitiveKeysAndInvalidRoot_Reject()
    {
        string duplicated = InvoiceJson().Replace(
            "\"Series\":\"S\",",
            "\"Series\":\"S\",\"series\":\"Z\",", StringComparison.Ordinal);
        Assert.ThrowsExactly<JsonException>(() =>
            Inspect(duplicated, ImpactContractProfile.InvoiceIapr,
                ImpactCounterpartyScenario.Business));
        Assert.ThrowsExactly<JsonException>(() =>
            Inspect("[]", ImpactContractProfile.Base, ImpactCounterpartyScenario.Unknown));
        try
        {
            Inspect("{\"a\":1,", ImpactContractProfile.Base, ImpactCounterpartyScenario.Unknown);
            Assert.Fail("Η μη ολοκληρωμένη JSON δομή πρέπει να απορρίπτεται.");
        }
        catch (JsonException)
        {
            // Ο parser μπορεί να επιστρέψει ειδικότερο τύπο εξαίρεσης ανά έκδοση SDK.
        }
    }

    [TestMethod]
    public void WindowsPowerShellUtf8Bom_IsAcceptedWithoutTaxApproval()
    {
        byte[] source = Encoding.UTF8.GetBytes(InvoiceJson());
        byte[] withBom = new byte[source.Length + 3];
        withBom[0] = 0xEF;
        withBom[1] = 0xBB;
        withBom[2] = 0xBF;
        source.AsSpan().CopyTo(withBom.AsSpan(3));

        ImpactOfflinePayloadReport plain = inspector.Inspect(source,
            ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);
        ImpactOfflinePayloadReport prefixed = inspector.Inspect(withBom,
            ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);

        Assert.AreEqual(plain.StructuralErrorCount, prefixed.StructuralErrorCount);
        Assert.AreEqual(plain.PendingExpertReviewCount, prefixed.PendingExpertReviewCount);
        Assert.AreEqual(1, prefixed.LineCount);
        Assert.AreEqual(0, prefixed.StructuralErrorCount);
        Assert.IsTrue(prefixed.PendingExpertReviewCount > 0);
        AssertAlwaysUnapproved(prefixed);
    }

    [TestMethod]
    public void BomNotAtStartOrDifferentEncoding_RemainsRejected()
    {
        byte[] embedded = Encoding.UTF8.GetBytes("{\uFEFF\"DocumentTypeCode\":\"INVOICE\"}");
        // Εσωτερικό BOM και UTF-16 δεν μετατρέπονται σιωπηρά σε έγκυρο UTF-8 JSON.
        AssertRejectsJson(embedded);

        byte[] utf16 = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(BaseJson())).ToArray();
        AssertRejectsJson(utf16);

        byte[] original = Encoding.UTF8.GetBytes(BaseJson());
        byte[] repeatedBom = [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. original];
        AssertRejectsJson(repeatedBom);
    }

    private void AssertRejectsJson(byte[] bytes)
    {
        try
        {
            inspector.Inspect(bytes, ImpactContractProfile.Base,
                ImpactCounterpartyScenario.Unknown);
            Assert.Fail("Το αλλοιωμένο ή μη UTF-8 JSON πρέπει να απορρίπτεται.");
        }
        catch (JsonException)
        {
            // Ο αναγνώστης JSON απορρίπτει την άκυρη κωδικοποίηση ή ενδιάμεσο BOM.
        }
    }

    [TestMethod]
    public void WrongProfileAndInvalidDocumentType_Reject()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Inspect(InvoiceJson(), (ImpactContractProfile)999,
                ImpactCounterpartyScenario.Business));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Inspect(InvoiceJson(), ImpactContractProfile.InvoiceB2G,
                ImpactCounterpartyScenario.Retail));

        string wrong = InvoiceJson().Replace(
            "\"DocumentTypeCode\":\"INVOICE\"",
            "\"DocumentTypeCode\":\"OTHER\"", StringComparison.Ordinal);
        ImpactOfflinePayloadReport report = Inspect(wrong,
            ImpactContractProfile.InvoiceIapr, ImpactCounterpartyScenario.Business);
        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "WrongDocumentType" &&
            finding.Severity == ImpactOfflineFindingSeverity.Error));
    }

    [TestMethod]
    public void MissingDetailsAndBadSummaryValues_AreReported()
    {
        string altered = BaseJson()
            .Replace("\"Details\":[{\"LineNo\":1}]", "\"Details\":[]", StringComparison.Ordinal)
            .Replace("\"TotalNetAmount\":100", "\"TotalNetAmount\":null",
                StringComparison.Ordinal);

        ImpactOfflinePayloadReport report = Inspect(altered, ImpactContractProfile.Base,
            ImpactCounterpartyScenario.Unknown);
        Assert.IsTrue(report.Findings.Any(finding => finding.Code == "MissingDetails"));
        Assert.IsTrue(report.Findings.Any(finding =>
            finding.Code == "InvalidDocumentedValue" &&
            finding.JsonPath == "$.Summaries.TotalNetAmount"));
        AssertAlwaysUnapproved(report);
    }

    [TestMethod]
    public void Cancellation_StopsBeforeParsing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            inspector.Inspect(Encoding.UTF8.GetBytes(BaseJson()),
                ImpactContractProfile.Base, ImpactCounterpartyScenario.Unknown,
                cancellation.Token));
    }

    private ImpactOfflinePayloadReport Inspect(
        string json, ImpactContractProfile profile, ImpactCounterpartyScenario scenario) =>
        inspector.Inspect(Encoding.UTF8.GetBytes(json), profile, scenario);

    private static void AssertAlwaysUnapproved(ImpactOfflinePayloadReport report)
    {
        Assert.IsFalse(report.IsActualProviderContractValidated);
        Assert.IsFalse(report.IsAadeClassificationApproved);
        Assert.IsFalse(report.IsFiscalRuleApproved);
        Assert.IsFalse(report.CanIssueFiscalDocument);
    }

    private static string BaseJson() => """
        {
            "DocumentTypeCode":"INVOICE",
            "DateIssued":"2026-11-11T10:30:00",
            "Series":"S",
            "Number":"1",
            "Issuer":{"Vat":"EL000000000"},
            "DistributionDetails":{"InternalDocumentId":"SYNTHETIC-ID"},
            "Details":[{"LineNo":1}],
            "Summaries":{"TotalNetAmount":100,"TotalVATAmount":24,"TotalGrossValue":124}
        }
        """;

    private static string InvoiceJson() =>
        """
        {
            "DocumentTypeCode":"INVOICE","DateIssued":"2026-11-11T10:30:00",
            "Series":"S","Number":"1","CurrencyCode":"EUR","InvoiceTypeCode":"1.1",
            "Issuer":{"Vat":"EL000000000","RegisteredName":"Test issuer","Address":{"CountryCode":"GR"}},
            "CounterParty":{"RegisteredName":"Test business","Vat":"EL000000000","Address":{"CountryCode":"GR"}},
            "DistributionDetails":{"InternalDocumentId":"SYNTHETIC-ID"},
            "Details":[{"LineNo":1,"UnitPrice":100,"NetTotal":100,"VatCategoryCode":1,"VatTotal":24,
                "IncomeClassification":{"ClassificationCategoryCode":"category1_1",
                    "ClassificationTypeCode":"E3_561_001","Amount":100}}],
            "Summaries":{"TotalNetAmount":100,"TotalVATAmount":24,"TotalGrossValue":124}
        }
        """;
}
