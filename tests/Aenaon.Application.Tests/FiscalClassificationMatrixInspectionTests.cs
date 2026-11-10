using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Απομονωμένοι έλεγχοι υποψήφιου πίνακα χωρίς πραγματικά φορολογικά δεδομένα.</summary>
[TestClass]
public sealed class FiscalClassificationMatrixInspectionTests
{
    private static readonly DateTimeOffset ReviewDate = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Inspection_RecognizesCandidateButNeverApprovesTaxUse()
    {
        using var files = new Fixture();
        files.WriteManifest(files.ValidManifest());

        FiscalClassificationMatrixInspection result =
            await Inspector().InspectAsync(files.ManifestPath, files.EvidencePath);
        Assert.AreEqual(1, result.CandidateCount);
        Assert.IsFalse(result.IsSourceTranscriptionVerified);
        Assert.IsFalse(result.IsImpactContractValidated);
        Assert.IsFalse(result.IsFiscalRuleApproved);
        Assert.IsFalse(result.CanIssueFiscalDocument);

        FiscalClassificationCandidateMatch match = result.FindCandidate("1.1", "CATEGORY1_1",
            "E3_SYNTHETIC", new DateOnly(2026, 11, 1));
        Assert.IsTrue(match.PresentInCandidateManifest);
        Assert.AreEqual("B2", match.SourceCell);
        Assert.IsFalse(match.IsFiscalRuleApproved);
        Assert.IsFalse(match.CanIssueFiscalDocument);

        FiscalClassificationCandidateMatch absent = result.FindCandidate("1.1", "CATEGORY1_1",
            "E3_DIFFERENT", new DateOnly(2026, 11, 1));
        Assert.IsFalse(absent.PresentInCandidateManifest);
        Assert.IsFalse(result.FindCandidate("1.1", "CATEGORY1_1",
            "E3_SYNTHETIC", new DateOnly(2025, 1, 1)).PresentInCandidateManifest);
    }

    [TestMethod]
    public async Task Inspection_RejectsEditedOriginalEvidence()
    {
        using var files = new Fixture();
        files.WriteManifest(files.ValidManifest());
        File.AppendAllText(files.EvidencePath, "-tampered");

        DomainRuleException result = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.hash.mismatch", result.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsLookalikeAuthority()
    {
        using var files = new Fixture();
        var original = files.ValidManifest();
        files.WriteManifest(new FiscalClassificationMatrixManifest
        {
            SourceUrl = "https://aade.gr.attacker.example/tax/matrix.xlsx",
            SourceVersion = original.SourceVersion,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = original.VerifiedAtUtc,
            Rows = original.Rows
        });

        DomainRuleException result = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.manifest.invalid", result.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsOverlappingExactTuples()
    {
        using var files = new Fixture();
        var original = files.ValidManifest();
        files.WriteManifest(new FiscalClassificationMatrixManifest
        {
            SourceUrl = original.SourceUrl,
            SourceVersion = original.SourceVersion,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = original.VerifiedAtUtc,
            Rows =
            [
                new FiscalClassificationMatrixRow
                {
                    InvoiceType = "1.1", IncomeCategory = "CATEGORY1_1",
                    IncomeType = "E3_SYNTHETIC", EffectiveFrom = new DateOnly(2026, 1, 1),
                    EffectiveUntil = new DateOnly(2026, 9, 30), SourceSheet = "Synthetic", SourceCell = "B2"
                },
                new FiscalClassificationMatrixRow
                {
                    InvoiceType = "1.1", IncomeCategory = "category1_1",
                    IncomeType = "E3_SYNTHETIC", EffectiveFrom = new DateOnly(2026, 9, 30),
                    SourceSheet = "Synthetic", SourceCell = "B3"
                }
            ]
        });

        DomainRuleException result = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.period.overlap", result.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsBadCellAndUnknownFields()
    {
        using var files = new Fixture();
        var original = files.ValidManifest();
        files.WriteManifest(new FiscalClassificationMatrixManifest
        {
            SourceUrl = original.SourceUrl,
            SourceVersion = original.SourceVersion,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = original.VerifiedAtUtc,
            Rows =
            [
                new FiscalClassificationMatrixRow
                {
                    InvoiceType = "1.1", IncomeCategory = "CATEGORY1_1",
                    IncomeType = "E3_SYNTHETIC", EffectiveFrom = new DateOnly(2026, 1, 1),
                    SourceSheet = "Synthetic", SourceCell = "A0"
                }
            ]
        });

        DomainRuleException badCell = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.manifest.invalid", badCell.Code);

        files.WriteManifest(original);
        string json = File.ReadAllText(files.ManifestPath);
        File.WriteAllText(files.ManifestPath,
            json.Replace("\"SourceUrl\":", "\"Unauthorized\":true,\"SourceUrl\":",
                StringComparison.Ordinal));
        DomainRuleException unknown = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.manifest.invalid", unknown.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsFutureReviewAndNullHash()
    {
        using var files = new Fixture();
        var original = files.ValidManifest();
        files.WriteManifest(new FiscalClassificationMatrixManifest
        {
            SourceUrl = original.SourceUrl,
            SourceVersion = original.SourceVersion,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = ReviewDate.AddDays(2),
            Rows = original.Rows
        });
        DomainRuleException future = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.manifest.invalid", future.Code);

        files.WriteManifest(original);
        string json = File.ReadAllText(files.ManifestPath).Replace(
            "\"ArtifactSha256\":\"" + original.ArtifactSha256 + "\"",
            "\"ArtifactSha256\":null", StringComparison.Ordinal);
        File.WriteAllText(files.ManifestPath, json);
        DomainRuleException missingHash = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.EvidencePath));
        Assert.AreEqual("fiscal.matrix.manifest.invalid", missingHash.Code);
    }

    private static FiscalClassificationMatrixInspectionService Inspector() =>
        new(new FixedClock());

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ReviewDate.AddDays(1);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory;

        public Fixture()
        {
            directory = Path.Combine(Path.GetTempPath(), "aenaon-matrix-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(EvidencePath, Encoding.UTF8.GetBytes("Synthetic XLS bytes for isolated tests only"));
        }

        public string ManifestPath => Path.Combine(directory, "manifest.json");
        public string EvidencePath => Path.Combine(directory, "aade-synthetic.xlsx");

        public FiscalClassificationMatrixManifest ValidManifest() => new()
        {
            SourceVersion = "CI-ONLY",
            SourceUrl = "https://aade.gr/sites/default/files/2026-09/syndiasmoi_xaraktirismwn_v2.0.2_0.xlsx",
            ArtifactSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(EvidencePath))),
            VerifiedAtUtc = ReviewDate,
            Rows =
            [
                new FiscalClassificationMatrixRow
                {
                    InvoiceType = "1.1", IncomeCategory = "CATEGORY1_1",
                    IncomeType = "E3_SYNTHETIC",
                    EffectiveFrom = new DateOnly(2026, 1, 1),
                    SourceSheet = "Synthetic", SourceCell = "B2"
                }
            ]
        };

        public void WriteManifest(FiscalClassificationMatrixManifest manifest) =>
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);

        public void Dispose() => Directory.Delete(directory, recursive: true);
    }
}
