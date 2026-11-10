using Aenaon.Infrastructure.Fiscal;
using Aenaon.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Διατηρεί ακριβώς μία τελική απόφαση ανά υποβολή με διαφορετικό ελεγκτή.</summary>
internal sealed class FiscalImportReviewConfiguration : IEntityTypeConfiguration<FiscalImportReviewRecord>
{
    public void Configure(EntityTypeBuilder<FiscalImportReviewRecord> builder)
    {
        builder.ToTable("FiscalImportReviews", "dbo", table =>
        {
            table.HasCheckConstraint("CK_FiscalImportReviews_Decision", "[Decision] IN (2, 3)");
            table.HasCheckConstraint("CK_FiscalImportReviews_Reviewer",
                "[ReviewerUserId] <> [SubmittedByUserId]");
            table.HasCheckConstraint("CK_FiscalImportReviews_Hash", "LEN([ContentSha256]) = 64");
            table.HasCheckConstraint("CK_FiscalImportReviews_Reason",
                "LEN(LTRIM(RTRIM([Reason]))) > 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Decision).HasConversion<int>().IsRequired();
        builder.Property(value => value.ContentSha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(value => value.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(value => new { value.BatchId, value.SubmittedByUserId }).IsUnique();
        builder.HasOne<FiscalImportBatch>().WithMany()
            .HasForeignKey(value => new { value.BatchId, value.SubmittedByUserId })
            .HasPrincipalKey(value => new { value.Id, value.SubmittedByUserId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AenaonUser>().WithMany().HasForeignKey(value => value.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
