using System.Text;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ελέγχει ασφαλή εμφάνιση δεδομένων στο Excel και απουσία δικαιώματος έγκρισης.</summary>
[TestClass]
public sealed class FiscalSemanticReviewWorklistCsvTests
{
    [TestMethod]
    public async Task Worklist_ExportsAllPendingRowsWithUnicodeAndExcelSafeCells()
    {
        using var directory = new TemporaryFolder();
        string path = Path.Combine(directory.Path, "review.csv");
        var writer = new FiscalSemanticReviewWorklistCsvWriter();

        await writer.WriteNewAsync(ValidDossier(), path);

        byte[] bytes = await File.ReadAllBytesAsync(path);
        Assert.AreEqual((byte)0xEF, bytes[0]);
        Assert.AreEqual((byte)0xBB, bytes[1]);
        Assert.AreEqual((byte)0xBF, bytes[2]);

        string contents = await File.ReadAllTextAsync(path);
        Assert.IsTrue(contents.StartsWith("sep=;\r\n", StringComparison.Ordinal));
        Assert.IsTrue(contents.Contains("ΑΑΔΕ", StringComparison.Ordinal));
        Assert.IsTrue(contents.Contains("Pending", StringComparison.Ordinal));
        Assert.IsTrue(contents.Contains("'=HYPERLINK", StringComparison.Ordinal));
        Assert.IsTrue(contents.Contains("'-2+5", StringComparison.Ordinal));
        Assert.IsTrue(contents.Contains("'@SUM", StringComparison.Ordinal));
        Assert.IsTrue(contents.Contains("\"\"quoted\"\"", StringComparison.Ordinal));
        Assert.AreEqual(3,
            contents.Split("\"Pending\"\r\n", StringSplitOptions.None).Length - 1);
        Assert.IsFalse(contents.Contains("Approved", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Worklist_DoesNotOverwritePreviousReviewerAnnotations()
    {
        using var directory = new TemporaryFolder();
        string path = Path.Combine(directory.Path, "existing.csv");
        await File.WriteAllTextAsync(path, "protected-annotations");
        var writer = new FiscalSemanticReviewWorklistCsvWriter();

        await Assert.ThrowsExactlyAsync<IOException>(() =>
            writer.WriteNewAsync(ValidDossier(), path));
        Assert.AreEqual("protected-annotations", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task Worklist_RejectsInvalidPathsAndAlteredReviewState()
    {
        using var directory = new TemporaryFolder();
        var writer = new FiscalSemanticReviewWorklistCsvWriter();
        DomainRuleException invalidPath = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            writer.WriteNewAsync(ValidDossier(), Path.Combine(directory.Path, "report.json")));
        Assert.AreEqual("fiscal.semantics.worklist.path.invalid", invalidPath.Code);

        FiscalSemanticReviewDossier original = ValidDossier();
        FiscalSemanticReviewDossier changed = original with
        {
            Items =
            [
                original.Items[0] with { IsHumanReviewed = true },
                original.Items[1],
                original.Items[2]
            ]
        };
        DomainRuleException altered = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            writer.WriteNewAsync(changed, Path.Combine(directory.Path, "altered.csv")));
        Assert.AreEqual("fiscal.semantics.worklist.invalid", altered.Code);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Path, "altered.csv")));
    }

    private static FiscalSemanticReviewDossier ValidDossier()
    {
        const string hash = "E541A87E9B2F8F320A3996253A8D47EFA0D21A2617FA45767F146A182CAFA9E8";
        FiscalSemanticReviewItem[] items =
        [
            new("EVIDENCE001", "1.1", FiscalXlsxClassificationDirection.Income,
                "C7", "=HYPERLINK(\"quoted\")", "ΑΑΔΕ",
                FiscalSemanticReviewKind.Unclassified, FiscalSemanticReviewPriority.Critical,
                "Χρειάζεται άνθρωπος, όχι υπολογισμός Excel",
                [new FiscalSemanticContextCell("B7", "@SUM", false, 1)]),
            new("EVIDENCE002", "2.4", FiscalXlsxClassificationDirection.Expense,
                "F8", "-2+5", "Συσχετιζόμενο", FiscalSemanticReviewKind.AssociatedDocumentDependency,
                FiscalSemanticReviewPriority.Critical, "Επιθεώρηση αρχικού παραστατικού", []),
            new("EVIDENCE003", "5.1", FiscalXlsxClassificationDirection.Income,
                "C9", "Δεν ενημερώνει E3", "Εξαίρεση",
                FiscalSemanticReviewKind.NoE3Update, FiscalSemanticReviewPriority.High,
                "Ανεξάρτητη επαλήθευση myDATA/IMPACT", [])
        ];
        FiscalSemanticReviewGroup[] groups =
        [
            new(FiscalSemanticReviewKind.Unclassified, FiscalSemanticReviewPriority.Critical, 1),
            new(FiscalSemanticReviewKind.AssociatedDocumentDependency,
                FiscalSemanticReviewPriority.Critical, 1),
            new(FiscalSemanticReviewKind.NoE3Update, FiscalSemanticReviewPriority.High, 1)
        ];
        return new FiscalSemanticReviewDossier(hash, 3, 100, 5,
            Array.AsReadOnly(items), Array.AsReadOnly(groups));
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "aenaon-worklist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
