using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Δοκιμές μη δεσμευτικής ταξινόμησης πραγματικών κελιών και προστασίας αποφάσεων.</summary>
[TestClass]
public sealed class FiscalXlsxSemanticReviewTests
{
    private static readonly FiscalXlsxClassificationEvidenceAnalyzer Analyzer = new();

    [TestMethod]
    public void Triage_RecognizesDistinctSpecialInstructions()
    {
        var cases = new (string Text, FiscalSemanticReviewKind Expected)[]
        {
            ("Επιτρέπονται οι συνδυασμοί εκείνων των τιμών που επιτρέπονται για τον τύπο του συσχετιζόμενου παραστατικού",
                FiscalSemanticReviewKind.AssociatedDocumentDependency),
            ("Συμπληρώνεται ανά περίπτωση τύπου παραστατικών 1.1, 1.2",
                FiscalSemanticReviewKind.AssociatedDocumentDependency),
            ("και μεταφορά προς υπολογισμό στην προηγούμενη χρήση.",
                FiscalSemanticReviewKind.PreviousFiscalYearTransfer),
            ("και μεταφορά προς υπολογισμό στην επόμενη χρήση.",
                FiscalSemanticReviewKind.NextFiscalYearTransfer),
            ("είναι επιτρεπτοί οι παραπάνω χαρακτηρισμοί ανά τιμή ΣΤ.9",
                FiscalSemanticReviewKind.InheritedByColumnNine),
            ("Δεν ενημερώνει E3", FiscalSemanticReviewKind.NoE3Update),
            ("Δεν γίνεται χαρακτηρισμός E3", FiscalSemanticReviewKind.NoE3Update),
            ("ή κενό", FiscalSemanticReviewKind.OptionalBlank),
            ("(Δημόσιο) δεν υφίσταται ποσό, αναγραφή", FiscalSemanticReviewKind.PublicSectorSpecialCase),
            ("Δεν ενημερώνει το Αναλυτικό Βιβλίο ΑΑΔΕ – αντισυμβαλλόμενος μη υπόχρεος σε τήρηση βιβλίων",
                FiscalSemanticReviewKind.AccountingBookException),
            ("ΕΠΙΤΡΕΠΤΕΣ ΤΙΜΕΣ E3", FiscalSemanticReviewKind.DocumentHeader),
            ("ΕΠΙΤΡΕΠΤΕΣ ΤΙΜΕΣ ΣΤΗΛΗΣ 9", FiscalSemanticReviewKind.DocumentHeader),
            ("Τιμολόγιο Πώλησης / Εκκαθάριση Πωλήσεων Τρίτων",
                FiscalSemanticReviewKind.DocumentHeader),
            ("Μη αναγνωρισμένη οδηγία XXX", FiscalSemanticReviewKind.Unclassified)
        };

        foreach ((string value, FiscalSemanticReviewKind kind) in cases)
        {
            Assert.AreEqual(kind, FiscalXlsxSemanticReviewService.SuggestKind(value), value);
        }
    }

    [TestMethod]
    public void Dossier_PreservesExactSourceAndKeepsEveryItemPending()
    {
        FiscalXlsxEvidenceInspection original = SyntheticInspection();
        var service = new FiscalXlsxSemanticReviewService(Analyzer);
        FiscalSemanticReviewDossier first = service.Build(original);
        FiscalSemanticReviewDossier second = service.Build(original);

        Assert.AreEqual(1, first.SheetCount);
        Assert.AreEqual(1, first.CandidatePairCount);
        Assert.AreEqual(5, first.PendingHumanReviewCount);
        Assert.AreEqual(1, first.UnclassifiedCount);
        Assert.IsTrue(first.RequiresHumanReview);
        Assert.IsFalse(first.IsOriginalAuthenticityVerified);
        Assert.IsFalse(first.IsImpactContractValidated);
        Assert.IsFalse(first.IsFiscalRuleApproved);
        Assert.IsFalse(first.CanIssueFiscalDocument);
        Assert.AreEqual(5, first.Items.Select(item => item.EvidenceId)
            .Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.AreEqual(first.Items.Select(item => item.EvidenceId).ToArray(),
            second.Items.Select(item => item.EvidenceId).ToArray());
        Assert.IsTrue(first.Items.All(item =>
            !item.IsHumanReviewed && !item.IsFiscalRuleApproved));
        Assert.IsTrue(first.Items.All(item => item.RequiredHumanCheck.Length > 10));
        Assert.IsTrue(first.Items.Any(item =>
            item.SuggestedKind == FiscalSemanticReviewKind.Unclassified &&
            item.Priority == FiscalSemanticReviewPriority.Critical));

        FiscalSemanticReviewItem previous = first.Items.Single(item => item.Address == "C8");
        Assert.AreEqual(FiscalSemanticReviewKind.PreviousFiscalYearTransfer, previous.SuggestedKind);
        Assert.IsTrue(previous.NearbyCells.Any(cell =>
            cell.Address == "B7" && cell.Text == "category1_1"));
        Assert.IsTrue(previous.NearbyCells.Any(cell =>
            cell.Address == "C8" && cell.Text == previous.ExactText));
        Assert.AreEqual(original.ArtifactSha256, first.OriginalXlsxSha256);
        Assert.AreEqual(original.TotalNonEmptyCellCount, first.OriginalCellCount);
        Assert.AreEqual(first.Items.Count, first.Groups.Sum(group => group.Count));
    }

    [TestMethod]
    public void Dossier_RejectsIncompleteEvidence()
    {
        FiscalXlsxEvidenceInspection original = SyntheticInspection();
        var bad = new FiscalXlsxEvidenceInspection(original.ArtifactSha256,
            original.FileSizeBytes,
            [
                new FiscalXlsxSheetOverview("1.1", 99, [])
                {
                    AllCells = original.Sheets[0].AllCells
                }
            ]);

        DomainRuleException exception = Assert.ThrowsExactly<DomainRuleException>(() =>
            new FiscalXlsxSemanticReviewService(Analyzer).Build(bad));
        Assert.AreEqual("fiscal.semantics.evidence.invalid", exception.Code);
    }

    [TestMethod]
    public async Task DossierWriter_CreatesNewOnlyAndKeepsApprovalFalse()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "aenaon-semantic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var dossier = new FiscalXlsxSemanticReviewService(Analyzer).Build(SyntheticInspection());
            var writer = new FiscalSemanticReviewDossierWriter();
            string path = Path.Combine(directory, "review.json");
            await writer.WriteNewAsync(dossier, path);
            using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.IsFalse(json.RootElement.GetProperty("IsFiscalRuleApproved").GetBoolean());
            Assert.IsFalse(json.RootElement.GetProperty("CanIssueFiscalDocument").GetBoolean());
            Assert.AreEqual(5,
                json.RootElement.GetProperty("PendingHumanReviewCount").GetInt32());

            string before = await File.ReadAllTextAsync(path);
            await Assert.ThrowsExactlyAsync<IOException>(() =>
                writer.WriteNewAsync(dossier, path));
            Assert.AreEqual(before, await File.ReadAllTextAsync(path));

            DomainRuleException extension = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
                writer.WriteNewAsync(dossier, Path.Combine(directory, "review.txt")));
            Assert.AreEqual("fiscal.semantics.report.path.invalid", extension.Code);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FiscalXlsxEvidenceInspection SyntheticInspection()
    {
        FiscalXlsxCellSample[] cells =
        [
            new("B7", "category1_1", false),
            new("C7", "E3_561_001", false),
            new("C8", "και μεταφορά προς υπολογισμό στην προηγούμενη χρήση.", false),
            new("B9", "category1_8", false),
            new("C9", "είναι επιτρεπτοί οι παραπάνω χαρακτηρισμοί ανά τιμή ΣΤ.9", false),
            new("B10", "category1_95", false),
            new("C10", "Δεν ενημερώνει E3", false),
            new("B11", "Επιτρέπονται οι συνδυασμοί του συσχετιζόμενου παραστατικού", false),
            new("C12", "Μη αναγνωρισμένη οδηγία XXX", false)
        ];
        var sheet = new FiscalXlsxSheetOverview("1.1", cells.Length, [])
        {
            AllCells = Array.AsReadOnly(cells),
            MergedRanges = Array.Empty<string>()
        };
        string sha = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes("synthetic-semantic-evidence")));
        return new FiscalXlsxEvidenceInspection(sha, 128,
            Array.AsReadOnly([sheet]));
    }
}
