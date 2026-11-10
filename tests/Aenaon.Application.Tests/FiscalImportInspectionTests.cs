using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Δοκιμάζει αποκλειστικά offline τεκμήρια χωρίς φορολογική έγκριση.</summary>
[TestClass]
public sealed class FiscalImportInspectionTests
{
    private static readonly DateTimeOffset Verified = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Inspection_MatchesArtifactAndNeverApprovesTaxOrDatabaseWrites()
    {
        using var files = new Fixture();
        FiscalImportManifest manifest = files.CreateManifest();
        files.WriteManifest(manifest);

        FiscalImportInspection result = await Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath);

        Assert.AreEqual(1, result.CandidateCount);
        Assert.AreEqual(FiscalReferenceSource.Impact, result.Source);
        Assert.AreEqual(FiscalCodeSystem.MyDataVatCategory, result.CodeSystem);
        Assert.AreEqual(manifest.ArtifactSha256, result.ArtifactSha256);
        Assert.IsFalse(result.CanImportToDatabase);
        Assert.IsFalse(result.IsTaxRuleApproved);
        Assert.IsFalse(result.CanIssueFiscalDocument);
    }

    [TestMethod]
    public async Task Inspection_RejectsModifiedEvidenceBytes()
    {
        using var files = new Fixture();
        files.WriteManifest(files.CreateManifest());
        File.AppendAllText(files.ArtifactPath, "modified");

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath));

        Assert.AreEqual("fiscal.import.hash.mismatch", error.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsLookalikeOrWrongAuthority()
    {
        using var files = new Fixture();
        var original = files.CreateManifest();
        files.WriteManifest(new FiscalImportManifest
        {
            Source = original.Source,
            CodeSystem = original.CodeSystem,
            SourceVersion = original.SourceVersion,
            SourceUrl = "https://developers.s1ecos.com.attacker.example/vat-tax-codes",
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = original.VerifiedAtUtc,
            Rows = original.Rows
        });

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath));
        Assert.AreEqual("fiscal.import.manifest.invalid", error.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsOverlappingCodePeriods()
    {
        using var files = new Fixture();
        var original = files.CreateManifest();
        files.WriteManifest(new FiscalImportManifest
        {
            Source = original.Source,
            CodeSystem = original.CodeSystem,
            SourceVersion = original.SourceVersion,
            SourceUrl = original.SourceUrl,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = original.VerifiedAtUtc,
            Rows =
            [
                new FiscalImportRow
                {
                    Code = "SYNTHETIC-TEST", Description = "Δοκιμαστικός κωδικός",
                    EffectiveFrom = new DateOnly(2026, 1, 1),
                    EffectiveUntil = new DateOnly(2026, 9, 30)
                },
                new FiscalImportRow
                {
                    Code = "synthetic-test", Description = "Η ίδια δοκιμαστική εγγραφή",
                    EffectiveFrom = new DateOnly(2026, 9, 30)
                }
            ]
        });

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath));
        Assert.AreEqual("fiscal.import.period.overlap", error.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsFutureEvidenceReview()
    {
        using var files = new Fixture();
        var original = files.CreateManifest();
        files.WriteManifest(new FiscalImportManifest
        {
            Source = original.Source,
            CodeSystem = original.CodeSystem,
            SourceVersion = original.SourceVersion,
            SourceUrl = original.SourceUrl,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
            Rows = original.Rows
        });

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath));
        Assert.AreEqual("fiscal.import.manifest.invalid", error.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsUnsupportedEn16931Authority()
    {
        using var files = new Fixture();
        var original = files.CreateManifest();
        files.WriteManifest(new FiscalImportManifest
        {
            Source = "En16931",
            CodeSystem = "En16931MeasurementUnit",
            SourceVersion = original.SourceVersion,
            SourceUrl = original.SourceUrl,
            ArtifactSha256 = original.ArtifactSha256,
            VerifiedAtUtc = original.VerifiedAtUtc,
            Rows = original.Rows
        });

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath));
        Assert.AreEqual("fiscal.import.manifest.invalid", error.Code);
    }

    [TestMethod]
    public async Task Inspection_RejectsUnrecognizedManifestProperties()
    {
        using var files = new Fixture();
        files.WriteManifest(files.CreateManifest());
        string contents = File.ReadAllText(files.ManifestPath);
        File.WriteAllText(files.ManifestPath,
            contents.Replace("\"Source\":", "\"Unexpected\":true,\"Source\":", StringComparison.Ordinal));

        DomainRuleException error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
            Inspector().InspectAsync(files.ManifestPath, files.ArtifactPath));
        Assert.AreEqual("fiscal.import.manifest.invalid", error.Code);
    }

    private static FiscalImportInspectionService Inspector() => new(new FixedClock());

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory;

        public Fixture()
        {
            directory = Path.Combine(Path.GetTempPath(), "aenaon-fiscal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(ArtifactPath, "Synthetic evidence solely for isolated tests", Encoding.UTF8);
        }

        public string ManifestPath => Path.Combine(directory, "manifest.json");
        public string ArtifactPath => Path.Combine(directory, "evidence.bin");

        public FiscalImportManifest CreateManifest()
        {
            string sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ArtifactPath)));
            return new FiscalImportManifest
            {
                Source = "Impact",
                CodeSystem = "MyDataVatCategory",
                SourceVersion = "CI-ONLY",
                SourceUrl = "https://developers.s1ecos.com/vat-tax-codes-869826m0",
                ArtifactSha256 = sha,
                VerifiedAtUtc = Verified,
                Rows =
                [
                    new FiscalImportRow
                    {
                        Code = "SYNTHETIC-TEST",
                        Description = "Μη πραγματική φορολογική τιμή",
                        EffectiveFrom = new DateOnly(2026, 1, 1)
                    }
                ]
            };
        }

        public void WriteManifest(FiscalImportManifest manifest) =>
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);

        public void Dispose() => Directory.Delete(directory, recursive: true);
    }
}
