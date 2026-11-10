using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Regression tests για πλήρη εξαγωγή, όρια δειγμάτων και ανθρώπινη επιθεώρηση.</summary>
[TestClass]
public sealed class FiscalXlsxCompleteExportTests
{
    [TestMethod]
    public async Task Inspector_RetainsEveryCellAndPreservesReviewMetadata()
    {
        using var workbook = new SampleWorkbook();
        workbook.Write();

        FiscalXlsxEvidenceInspection inspected =
            await new FiscalXlsxEvidenceInspector().InspectAsync(workbook.Path);

        Assert.HasCount(2, inspected.Sheets);
        Assert.IsTrue(inspected.ExportIsComplete);
        Assert.IsTrue(inspected.TotalNonEmptyCellCount > 30);
        Assert.AreEqual(inspected.TotalNonEmptyCellCount, inspected.TotalExportedCellCount);
        FiscalXlsxSheetOverview first = inspected.Sheets[0];
        Assert.HasCount(30, first.SampleCells);
        Assert.IsTrue(first.AllCells.Count > first.SampleCells.Count);
        Assert.IsTrue(first.ExportIsComplete);
        Assert.IsTrue(first.HasStyledCells);
        Assert.Contains("B7:B8", first.MergedRanges);
        Assert.AreEqual(1, first.AllCells.Single(cell => cell.Address == "B7").StyleIndex);
        Assert.IsTrue(first.AllCells.Single(cell => cell.Address == "A2").Text.Length > 180);
        Assert.IsTrue(first.SampleCells.Single(cell => cell.Address == "A2").Text.Length <= 180);
        Assert.IsFalse(inspected.IsClassificationMatrixVerified);
        Assert.IsFalse(inspected.IsFiscalRuleApproved);
        Assert.IsFalse(inspected.CanIssueFiscalDocument);
    }

    [TestMethod]
    public async Task Export_ContainsAllCellsAndKeepsRevenueExpenseSeparate()
    {
        using var workbook = new SampleWorkbook();
        workbook.Write();
        FiscalXlsxEvidenceInspection inspection =
            await new FiscalXlsxEvidenceInspector().InspectAsync(workbook.Path);

        var exporter = new FiscalXlsxEvidenceExportService(
            new FiscalXlsxClassificationEvidenceAnalyzer());
        string target = System.IO.Path.Combine(workbook.DirectoryPath, "complete-export.json");
        FiscalXlsxEvidenceExport report = await exporter.ExportNewAsync(inspection, target);

        Assert.IsTrue(report.IsComplete);
        Assert.AreEqual(report.NonEmptyCellCount, report.ExportedCellCount);
        Assert.AreEqual(2, report.SheetCount);
        Assert.IsFalse(report.IsFiscalRuleApproved);
        Assert.IsFalse(report.IsClassificationMatrixVerified);
        Assert.IsFalse(report.CanIssueFiscalDocument);

        Assert.IsTrue(report.ClassificationEvidence.CandidatePairs.Any(item =>
            item.Direction == FiscalXlsxClassificationDirection.Income &&
            item.CategoryCode == "CATEGORY1_3" &&
            item.ClassificationCode == "E3_561_001" &&
            item.CategoryCell == "B7" &&
            item.ClassificationCell == "C7"));
        Assert.IsTrue(report.ClassificationEvidence.CandidatePairs.Any(item =>
            item.Direction == FiscalXlsxClassificationDirection.Expense &&
            item.CategoryCode == "CATEGORY2_3" &&
            item.ClassificationCode == "E3_585_004" &&
            item.CategoryCell == "E7"));
        Assert.IsTrue(report.ClassificationEvidence.RequiresReview.Any(item =>
            item.Text.Contains("Δεν ενημερώνει E3", StringComparison.Ordinal)));
        Assert.IsTrue(report.ClassificationEvidence.RequiresReview.Any(item =>
            item.Text.Contains("συσχετιζόμενου παραστατικού", StringComparison.Ordinal)));
        Assert.IsFalse(report.ClassificationEvidence.IsFiscalRuleApproved);

        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(target));
        JsonElement root = json.RootElement;
        Assert.IsTrue(root.GetProperty("IsComplete").GetBoolean());
        Assert.IsFalse(root.GetProperty("CanIssueFiscalDocument").GetBoolean());
        Assert.AreEqual(inspection.TotalNonEmptyCellCount,
            root.GetProperty("ExportedCellCount").GetInt32());
        Assert.AreEqual(inspection.ArtifactSha256, root.GetProperty("ArtifactSha256").GetString());

        JsonElement sheets = root.GetProperty("Sheets");
        Assert.AreEqual(inspection.Sheets[0].AllCells.Count,
            sheets[0].GetProperty("Cells").GetArrayLength());
        Assert.AreEqual(inspection.Sheets[1].AllCells.Count,
            sheets[1].GetProperty("Cells").GetArrayLength());
        Assert.Contains("B7:B8", sheets[0].GetProperty("MergedRanges").EnumerateArray()
            .Select(element => element.GetString()!).ToArray());
        Assert.IsTrue(sheets[0].GetProperty("Cells").EnumerateArray()
            .Any(cell => cell.GetProperty("Address").GetString() == "A2" &&
                cell.GetProperty("Text").GetString()!.Length > 180));

        // Η δεύτερη απόπειρα δεν επιτρέπεται να αντικαταστήσει ήδη παραδομένο αρχείο.
        string before = await File.ReadAllTextAsync(target);
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            exporter.ExportNewAsync(inspection, target));
        Assert.AreEqual(before, await File.ReadAllTextAsync(target));
    }

    [TestMethod]
    public async Task Inspector_RejectsDuplicateAddresses()
    {
        using var workbook = new SampleWorkbook();
        workbook.Write(duplicateAddress: true);

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            new FiscalXlsxEvidenceInspector().InspectAsync(workbook.Path));
        Assert.AreEqual("fiscal.xlsx.structure.invalid", error.Code);
    }

    private sealed class SampleWorkbook : IDisposable
    {
        public SampleWorkbook()
        {
            DirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "aenaon-full-xlsx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }
        public string Path => System.IO.Path.Combine(DirectoryPath, "synthetic.xlsx");

        public void Write(bool duplicateAddress = false)
        {
            using var stream = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            Add(archive, "xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                    <sheets>
                        <sheet name="1.1" sheetId="1" r:id="rId1"/>
                        <sheet name="11.2" sheetId="2" r:id="rId2"/>
                    </sheets>
                </workbook>
                """);
            Add(archive, "xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                    <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                    <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
                </Relationships>
                """);
            Add(archive, "xl/sharedStrings.xml", """
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                    <si><t>CATEGORY1_3</t></si>
                    <si><t>CATEGORY2_3</t></si>
                </sst>
                """);

            var first = new StringBuilder();
            first.Append("""
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1"><c r="B1" t="inlineStr"><is><t>ΤΥΠΟΣ ΠΑΡΑΣΤΑΤΙΚΟΥ</t></is></c></row>
                """);
            first.Append("<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>")
                .Append(new string('X', 300)).Append("</t></is></c></row>");
            first.Append("""
                    <row r="7">
                      <c r="B7" t="s" s="1"><v>0</v></c>
                      <c r="C7" t="inlineStr"><is><t>E3_561_001</t></is></c>
                      <c r="E7" t="s"><v>1</v></c>
                      <c r="F7" t="inlineStr"><is><t>E3_585_004</t></is></c>
                    </row>
                    <row r="8">
                      <c r="C8" t="inlineStr"><is><t>E3_561_007</t></is></c>
                      <c r="F8" t="inlineStr"><is><t>E3_585_016</t></is></c>
                    </row>
                    <row r="9">
                      <c r="B9" t="inlineStr"><is><t>category1_95</t></is></c>
                      <c r="C9" t="inlineStr"><is><t>Δεν ενημερώνει E3</t></is></c>
                    </row>
                    <row r="10">
                      <c r="B10" t="inlineStr"><is><t>Επιτρέπονται εκείνοι οι συνδυασμοί του συσχετιζόμενου παραστατικού</t></is></c>
                    </row>
                    <row r="11">
                      <c r="B11" t="inlineStr"><is><t>category1_3</t></is></c>
                      <c r="C11" t="inlineStr"><is><t>E3_561_001</t></is></c>
                    </row>
                """);
            for (int row = 12; row <= 54; row++)
            {
                first.Append("<row r=\"").Append(row).Append("\"><c r=\"C").Append(row)
                    .Append("\" t=\"inlineStr\"><is><t>E3_561_")
                    .Append(row.ToString("D3")).Append("</t></is></c></row>");
            }

            if (duplicateAddress)
            {
                first.Append("""
                    <row r="55"><c r="C7"><v>9</v></c></row>
                    """);
            }

            first.Append("</sheetData><mergeCells count=\"1\"><mergeCell ref=\"B7:B8\"/></mergeCells></worksheet>");
            Add(archive, "xl/worksheets/sheet1.xml", first.ToString());

            Add(archive, "xl/worksheets/sheet2.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                    <sheetData>
                        <row r="7"><c r="B7" t="inlineStr"><is><t>category1_3</t></is></c>
                            <c r="C7" t="inlineStr"><is><t>E3_561_003</t></is></c></row>
                        <row r="8"><c r="B8" t="inlineStr"><is><t>category1_8</t></is></c>
                            <c r="C8" t="inlineStr"><is><t>Επιτρέπονται οι παραπάνω χαρακτηρισμοί ανά τιμή ΣΤ.9</t></is></c></row>
                    </sheetData>
                </worksheet>
                """);
        }

        private static void Add(ZipArchive archive, string path, string xml)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
