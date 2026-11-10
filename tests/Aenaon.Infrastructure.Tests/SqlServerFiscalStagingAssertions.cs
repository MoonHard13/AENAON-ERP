using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Aenaon.Infrastructure.Fiscal;
using Aenaon.Infrastructure.Identity;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Ελέγχει αληθινή SQL συναλλαγή staging/review σε απομονωμένη βάση CI.</summary>
internal static class SqlServerFiscalStagingAssertions
{
    public static async Task RunAsync(SqlConnection connection, AenaonDbContext context,
        CancellationToken cancellationToken)
    {
        var submitter = new AenaonUser
        {
            UserName = "fiscal.submit@ci.invalid",
            NormalizedUserName = "FISCAL.SUBMIT@CI.INVALID"
        };
        var reviewer = new AenaonUser
        {
            UserName = "fiscal.review@ci.invalid",
            NormalizedUserName = "FISCAL.REVIEW@CI.INVALID"
        };
        context.Users.AddRange(submitter, reviewer);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        string directory = Path.Combine(Path.GetTempPath(), "aenaon-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string evidencePath = Path.Combine(directory, "evidence.bin");
            string manifestPath = Path.Combine(directory, "manifest.json");
            await File.WriteAllTextAsync(evidencePath, "SYNTHETIC evidence (CI only)", Encoding.UTF8,
                cancellationToken);
            string hash = Convert.ToHexString(SHA256.HashData(
                await File.ReadAllBytesAsync(evidencePath, cancellationToken)));
            var manifest = new FiscalImportManifest
            {
                Source = "Impact",
                CodeSystem = "MyDataVatCategory",
                SourceVersion = "CI-ONLY",
                SourceUrl = "https://developers.s1ecos.com/vat-tax-codes-869826m0",
                ArtifactSha256 = hash,
                VerifiedAtUtc = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero),
                Rows =
                [
                    new FiscalImportRow
                    {
                        Code = "SYNTHETIC-CI-VAT",
                        Description = "Μόνο για αυτοματοποιημένους ελέγχους",
                        EffectiveFrom = new DateOnly(2026, 1, 1)
                    }
                ]
            };
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest),
                Encoding.UTF8, cancellationToken);
            var clock = new FixedClock();
            var inspection = new FiscalImportInspectionService(clock);
            var policy = new ApprovedTestPolicy();
            var service = new FiscalStagingService(context, inspection, policy, clock,
                NullLogger<FiscalStagingService>.Instance);

            Guid batchId = await service.StageAsync(manifestPath, evidencePath, submitter.Id, cancellationToken);
            context.ChangeTracker.Clear();

            FiscalImportBatch loaded = await context.FiscalImportBatches.AsNoTracking()
                .Include(value => value.Rows)
                .SingleAsync(value => value.Id == batchId, cancellationToken);
            Assert.AreEqual(FiscalImportReviewStatus.Staged, loaded.Status);
            Assert.HasCount(1, loaded.Rows);
            loaded.VerifyContentFingerprint();
            Assert.AreEqual(64, loaded.ContentSha256.Length);
            Assert.AreEqual(0, await context.FiscalImportReviews.CountAsync(cancellationToken));
            Assert.AreEqual(0, await context.FiscalReferenceCodes.CountAsync(cancellationToken)
                - 3);

            DomainRuleException self = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
                service.ReviewAsync(batchId, submitter.Id, true, "Απόπειρα αυτοέγκρισης",
                    cancellationToken));
            Assert.AreEqual("fiscal.staging.review.invalid", self.Code);

            context.ChangeTracker.Clear();
            FiscalImportReviewStatus status = await service.ReviewAsync(batchId, reviewer.Id,
                true, "Επιβεβαιώθηκε χειροκίνητα η πηγή του δοκιμαστικού αρχείου.", cancellationToken);
            Assert.AreEqual(FiscalImportReviewStatus.SourceReviewed, status);
            context.ChangeTracker.Clear();
            FiscalImportReviewRecord audit = await context.FiscalImportReviews.AsNoTracking()
                .SingleAsync(cancellationToken);
            Assert.AreEqual(loaded.ContentSha256, audit.ContentSha256);
            Assert.AreEqual(reviewer.Id, audit.ReviewerUserId);
            Assert.AreEqual(submitter.Id, audit.SubmittedByUserId);

            DomainRuleException duplicate = await Assert.ThrowsExactlyAsync<DomainRuleException>(() =>
                service.ReviewAsync(batchId, reviewer.Id, false, "Δεν επιτρέπεται επανάληψη",
                    cancellationToken));
            Assert.AreEqual("fiscal.staging.review.completed", duplicate.Code);

            // Η βάση προστατεύει τις αποφάσεις ακόμη κι αν παρακαμφθεί το Domain.
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE dbo.FiscalImportBatches
                SET ReviewedByUserId = SubmittedByUserId
                WHERE Id = @batch;
                """;
            command.Parameters.AddWithValue("@batch", batchId);
            SqlException rejected = await Assert.ThrowsExactlyAsync<SqlException>(async () =>
                await command.ExecuteNonQueryAsync(cancellationToken));
            Assert.AreEqual(547, rejected.Number);
            Assert.AreEqual(1, await context.FiscalImportReviews.CountAsync(cancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ApprovedTestPolicy : IFiscalStagingAuthorizationPolicy
    {
        public Task<bool> CanSubmitAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanReviewAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    }
}
