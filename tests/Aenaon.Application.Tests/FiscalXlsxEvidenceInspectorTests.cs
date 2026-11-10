using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Απομονωμένες δοκιμές πραγματικών OOXML bytes χωρίς φορολογική χρήση.</summary>
[TestClass]
public sealed class FiscalXlsxEvidenceInspectorTests
{
    [TestMethod]
    public async Task Inspector_ReadsRealSharedInlineNumericAndFormulaCells()
    {
        using var fixture = new WorkbookFixture();
        fixture.Write();

        var inspector = new FiscalXlsxEvidenceInspector();
        FiscalXlsxEvidenceInspection result = await inspector.InspectAsync(fixture.Path);

        Assert.AreEqual(1, result.Sheets.Count);
        Assert.AreEqual("Classification Matrix", result.Sheets[0].Name);
        Assert.AreEqual(4, result.Sheets[0].NonEmptyCellCount);
        Assert.AreEqual("TYPE HEADER", result.Sheets[0].SampleCells[0].Text);
        Assert.AreEqual("Κατηγορία", result.Sheets[0].SampleCells[1].Text);
        Assert.AreEqual("1.1", result.Sheets[0].SampleCells[2].Text);
        Assert.IsTrue(result.Sheets[0].SampleCells[3].IsFormula);
        Assert.DoesNotContain("CACHED-UNTRUSTED", result.Sheets[0].SampleCells[3].Text);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture.Path))),
            result.ArtifactSha256);
        Assert.IsFalse(result.IsClassificationMatrixVerified);
        Assert.IsFalse(result.IsFiscalRuleApproved);
        Assert.IsFalse(result.CanIssueFiscalDocument);
    }

    [TestMethod]
    public async Task Inspector_RejectsExternalSheetRelationship()
    {
        using var fixture = new WorkbookFixture();
        fixture.Write(externalRelationship: true);

        var exception = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            new FiscalXlsxEvidenceInspector().InspectAsync(fixture.Path));

        Assert.AreEqual("fiscal.xlsx.structure.invalid", exception.Code);
    }

    [TestMethod]
    public async Task Inspector_RejectsXlsxWithXmlDtd()
    {
        using var fixture = new WorkbookFixture();
        fixture.Write(withDtd: true);

        var exception = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            new FiscalXlsxEvidenceInspector().InspectAsync(fixture.Path));

        Assert.AreEqual("fiscal.xlsx.structure.invalid", exception.Code);
    }

    [TestMethod]
    public async Task Inspector_RejectsMissingWorkbookAndOtherFormats()
    {
        using var fixture = new WorkbookFixture();
        fixture.Write(skipWorkbook: true);
        var exception = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            new FiscalXlsxEvidenceInspector().InspectAsync(fixture.Path));
        Assert.AreEqual("fiscal.xlsx.structure.invalid", exception.Code);

        var badExtension = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            new FiscalXlsxEvidenceInspector().InspectAsync(fixture.Path + ".xls"));
        Assert.AreEqual("fiscal.xlsx.structure.invalid", badExtension.Code);
    }

    [TestMethod]
    public async Task Inspector_PropagatesCancellation()
    {
        using var fixture = new WorkbookFixture();
        fixture.Write();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new FiscalXlsxEvidenceInspector().InspectAsync(fixture.Path, cancellation.Token));
    }

    private sealed class WorkbookFixture : IDisposable
    {
        private readonly string directory;

        public WorkbookFixture()
        {
            directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "aenaon-real-xlsx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        public string Path => System.IO.Path.Combine(directory, "synthetic-evidence.xlsx");

        public void Write(bool externalRelationship = false, bool withDtd = false,
            bool skipWorkbook = false)
        {
            using var stream = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

            if (!skipWorkbook)
            {
                string workbook = withDtd
                    ? "<!DOCTYPE x [ <!ENTITY example 'foo'> ]><x>&example;</x>"
                    : """
                      <?xml version="1.0" encoding="utf-8"?>
                      <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                        <sheets><sheet name="Classification Matrix" sheetId="1" r:id="rId1"/></sheets>
                      </workbook>
                      """;
                Add(zip, "xl/workbook.xml", workbook);
            }

            Add(zip, "xl/_rels/workbook.xml.rels", externalRelationship
                ? """
                  <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                    <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"
                        TargetMode="External" Target="https://evil.example/sheet.xml"/>
                  </Relationships>
                  """
                : """
                  <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                    <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"
                        Target="worksheets/sheet1.xml"/>
                  </Relationships>
                  """);

            Add(zip, "xl/sharedStrings.xml", """
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                    <si><t>TYPE HEADER</t></si>
                </sst>
                """);
            Add(zip, "xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                    <sheetData>
                        <row r="1"><c r="A1" t="s"><v>0</v></c></row>
                        <row r="2"><c r="B2" t="inlineStr"><is><t>Κατηγορία</t></is></c></row>
                        <row r="3"><c r="C3"><v>1.1</v></c></row>
                        <row r="4"><c r="D4"><f>SUM(C3)</f><v>CACHED-UNTRUSTED</v></c></row>
                    </sheetData>
                </worksheet>
                """);
        }

        private static void Add(ZipArchive archive, string name, string value)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(value);
        }

        public void Dispose() => Directory.Delete(directory, recursive: true);
    }
}
