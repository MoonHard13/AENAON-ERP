using Aenaon.Application.Fiscal;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Fiscal;
using Aenaon.Infrastructure.Fiscal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Επαληθεύει αποτυπώματα και ανεξάρτητο review χωρίς πραγματικά φορολογικά δεδομένα.</summary>
[TestClass]
public sealed class FiscalStagingDomainTests
{
    private static readonly DateTimeOffset Submitted = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static FiscalImportInspection Sample()
    {
        FiscalReferenceCode reference = FiscalReferenceCode.Create(
            FiscalReferenceSource.Impact, FiscalCodeSystem.MyDataVatCategory,
            "SYNTHETIC-CI", "CI-ONLY", new DateOnly(2026, 1, 1));
        return new FiscalImportInspection(FiscalReferenceSource.Impact,
            FiscalCodeSystem.MyDataVatCategory, "CI-ONLY", new string('A', 64), 1)
        {
            SourceUrl = "https://developers.s1ecos.com/vat-tax-codes-869826m0",
            VerifiedAtUtc = Submitted.AddHours(-1),
            InspectedRows = [new FiscalInspectedRow(reference, "Τεχνητός κωδικός")]
        };
    }

    [TestMethod]
    public void Batch_RejectsSelfApprovalAndRequiresReason()
    {
        Guid submitter = Guid.NewGuid();
        FiscalImportBatch batch = FiscalImportBatch.Create(Sample(), submitter, Submitted);

        DomainRuleException selfReview = Assert.ThrowsExactly<DomainRuleException>(() =>
            batch.RecordDecision(submitter, Submitted.AddMinutes(1), true, "Έλεγχος"));
        Assert.AreEqual("fiscal.staging.review.invalid", selfReview.Code);

        DomainRuleException noReason = Assert.ThrowsExactly<DomainRuleException>(() =>
            batch.RecordDecision(Guid.NewGuid(), Submitted.AddMinutes(1), false, " "));
        Assert.AreEqual("fiscal.staging.review.invalid", noReason.Code);
    }

    [TestMethod]
    public void Review_HasOneFinalDecisionWithMatchingFingerprint()
    {
        Guid submitter = Guid.NewGuid();
        Guid reviewer = Guid.NewGuid();
        FiscalImportBatch batch = FiscalImportBatch.Create(Sample(), submitter, Submitted);
        batch.VerifyContentFingerprint();

        FiscalImportReviewRecord record = batch.RecordDecision(reviewer,
            Submitted.AddMinutes(5), true, "Επιθεωρήθηκε το πρωτότυπο τεκμήριο.");
        Assert.AreEqual(FiscalImportReviewStatus.SourceReviewed, batch.Status);
        Assert.AreEqual(batch.ContentSha256, record.ContentSha256);
        Assert.AreEqual(submitter, record.SubmittedByUserId);
        Assert.AreEqual(reviewer, record.ReviewerUserId);
        Assert.HasCount(1, batch.Rows);

        DomainRuleException repeat = Assert.ThrowsExactly<DomainRuleException>(() =>
            batch.RecordDecision(Guid.NewGuid(), Submitted.AddMinutes(6), false, "Δεύτερη απόφαση"));
        Assert.AreEqual("fiscal.staging.review.completed", repeat.Code);
    }

    [TestMethod]
    public void Batch_RejectsEmptyUserAndInvalidDates()
    {
        var missing = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalImportBatch.Create(Sample(), Guid.Empty, Submitted));
        Assert.AreEqual("fiscal.staging.submission.invalid", missing.Code);

        var future = Assert.ThrowsExactly<DomainRuleException>(() =>
            FiscalImportBatch.Create(Sample(), Guid.NewGuid(), Submitted.AddHours(-2)));
        Assert.AreEqual("fiscal.staging.submission.invalid", future.Code);
    }

    [TestMethod]
    public async Task DefaultPolicy_DeniesAllActors()
    {
        var policy = new DenyAllFiscalStagingAuthorizationPolicy();
        Assert.IsFalse(await policy.CanSubmitAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.IsFalse(await policy.CanReviewAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
