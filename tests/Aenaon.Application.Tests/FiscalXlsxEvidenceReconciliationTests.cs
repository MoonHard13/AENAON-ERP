using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Επαληθεύει τεχνική συμφωνία δύο πραγματικών τοπικών αρχείων χωρίς φορολογική ενεργοποίηση.</summary>
[TestClass]
public sealed class FiscalXlsxEvidenceReconciliationTests
{
    [TestMethod]
    public async Task OriginalAndUntouchedExport_VerifyCellByCell()
    {
        using var fixture = new EvidenceFixture();
        await fixture.CreateAsync();

        FiscalXlsxEvidenceReconciliationResult result =
            await Reconciler().VerifyAsync(fixture.Spreadsheet, fixture.FullJson);

        Assert.IsTrue(result.IsOriginalFileContentMatched);
        Assert.AreEqual(1, result.SheetCount);
        Assert.AreEqual(8, result.CellCount);
        Assert.AreEqual(2, result.CandidatePairCount);
        Assert.AreEqual(1, result.ManualReviewCount);
        Assert.IsFalse(result.IsClassificationMatrixVerified);
        Assert.IsFalse(result.IsImpactContractValidated);
        Assert.IsFalse(result.IsFiscalRuleApproved);
        Assert.IsFalse(result.CanIssueFiscalDocument);
    }

    [TestMethod]
    public async Task ModifiedCellWithSameCounts_IsRejected()
    {
        using var fixture = new EvidenceFixture();
        await fixture.CreateAsync();

        JsonNode root = JsonNode.Parse(await File.ReadAllTextAsync(fixture.FullJson))!;
        root["Sheets"]![0]!["Cells"]![4]!["Text"] = "E3_561_999";
        await File.WriteAllTextAsync(fixture.FullJson, root.ToJsonString());

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Reconciler().VerifyAsync(fixture.Spreadsheet, fixture.FullJson));
        Assert.AreEqual("fiscal.xlsx.reconciliation.mismatch", error.Code);
    }

    [TestMethod]
    public async Task AlteredCandidateWithoutChangingCells_IsRejected()
    {
        using var fixture = new EvidenceFixture();
        await fixture.CreateAsync();

        JsonNode root = JsonNode.Parse(await File.ReadAllTextAsync(fixture.FullJson))!;
        root["ClassificationEvidence"]!["CandidatePairs"]![0]!["ClassificationCode"] = "E3_999";
        await File.WriteAllTextAsync(fixture.FullJson, root.ToJsonString());

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Reconciler().VerifyAsync(fixture.Spreadsheet, fixture.FullJson));
        Assert.AreEqual("fiscal.xlsx.reconciliation.mismatch", error.Code);
    }

    [TestMethod]
    public async Task AlteredMergeOrClaimedApproval_IsRejected()
    {
        using var fixture = new EvidenceFixture();
        await fixture.CreateAsync();
        string original = await File.ReadAllTextAsync(fixture.FullJson);

        JsonNode root = JsonNode.Parse(original)!;
        root["Sheets"]![0]!["MergedRanges"]![0] = "B1:B2";
        await File.WriteAllTextAsync(fixture.FullJson, root.ToJsonString());
        DomainRuleException merged = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Reconciler().VerifyAsync(fixture.Spreadsheet, fixture.FullJson));
        Assert.AreEqual("fiscal.xlsx.reconciliation.mismatch", merged.Code);

        root = JsonNode.Parse(original)!;
        root["CanIssueFiscalDocument"] = true;
        await File.WriteAllTextAsync(fixture.FullJson, root.ToJsonString());
        DomainRuleException approval = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Reconciler().VerifyAsync(fixture.Spreadsheet, fixture.FullJson));
        Assert.AreEqual("fiscal.xlsx.reconciliation.invalid", approval.Code);
    }

    [TestMethod]
    public async Task EvidenceFromDifferentXlsx_IsRejected()
    {
        using var fixture = new EvidenceFixture();
        await fixture.CreateAsync();
        using var other = new EvidenceFixture();
        await other.CreateAsync();
        File.AppendAllText(other.Spreadsheet, "appended-untrusted");

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Reconciler().VerifyAsync(other.Spreadsheet, fixture.FullJson));
        Assert.AreEqual("fiscal.xlsx.reconciliation.mismatch", error.Code);
    }

    [TestMethod]
    public async Task PreCancelledInspection_StopsImmediately()
    {
        using var fixture = new EvidenceFixture();
        await fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            Reconciler().VerifyAsync(fixture.Spreadsheet, fixture.FullJson,
                cancellation.Token));
    }

    private static FiscalXlsxEvidenceReconciliationService Reconciler() => new(
        new FiscalXlsxEvidenceInspector(),
        new FiscalXlsxEvidenceExportService(new FiscalXlsxClassificationEvidenceAnalyzer()));

    private sealed class EvidenceFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(),
            "aenaon-reconcile-" + Guid.NewGuid().ToString("N"));

        public string Spreadsheet => Path.Combine(directory, "synthetic.xlsx");
        public string FullJson => Path.Combine(directory, "synthetic-full.json");

        public async Task CreateAsync()
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(Spreadsheet, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Add(archive, "xl/workbook.xml", """
                    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                        xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                        <sheets><sheet name="1.1" sheetId="1" r:id="rId1"/></sheets>
                    </workbook>
                    """);
                Add(archive, "xl/_rels/workbook.xml.rels", """
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                    </Relationships>
                    """);
                Add(archive, "xl/worksheets/sheet1.xml", """
                    <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                      <sheetData>
                        <row r="1"><c r="B1" t="inlineStr"><is><t>SYNTHETIC TEST</t></is></c></row>
                        <row r="5"><c r="B5" t="inlineStr"><is><t>ΕΣΟΔΑ</t></is></c></row>
                        <row r="6"><c r="C6" t="inlineStr"><is><t>Χαρακτηρισμοί E3</t></is></c></row>
                        <row r="7">
                          <c r="B7" t="inlineStr" s="3"><is><t>category1_3</t></is></c>
                          <c r="C7" t="inlineStr"><is><t>E3_561_001</t></is></c>
                          <c r="E7" t="inlineStr"><is><t>category2_3</t></is></c>
                          <c r="F7" t="inlineStr"><is><t>E3_585_004</t></is></c>
                        </row>
                        <row r="8">
                          <c r="C8" t="inlineStr"><is><t>Δεν ενημερώνει E3</t></is></c>
                        </row>
                      </sheetData>
                      <mergeCells count="1"><mergeCell ref="B5:C5"/></mergeCells>
                    </worksheet>
                    """);
            }

            var inspector = new FiscalXlsxEvidenceInspector();
            var exporter = new FiscalXlsxEvidenceExportService(
                new FiscalXlsxClassificationEvidenceAnalyzer());
            await exporter.ExportNewAsync(await inspector.InspectAsync(Spreadsheet), FullJson);
        }

        private static void Add(ZipArchive archive, string path, string xml)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }

        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
