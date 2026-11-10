using Aenaon.Infrastructure.Fiscal;
using Aenaon.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Επιβάλλει απομόνωση staging από το ενεργό φορολογικό μητρώο.</summary>
internal sealed class FiscalImportBatchConfiguration : IEntityTypeConfiguration<FiscalImportBatch>
{
    public void Configure(EntityTypeBuilder<FiscalImportBatch> builder)
    {
        builder.ToTable("FiscalImportBatches", "dbo", table =>
        {
            table.HasCheckConstraint("CK_FiscalImportBatches_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_FiscalImportBatches_Source", "[Source] IN (1, 2)");
            table.HasCheckConstraint("CK_FiscalImportBatches_CodeSystem", "[CodeSystem] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_FiscalImportBatches_Evidence",
                "LEN([ArtifactSha256]) = 64 AND LEN([ContentSha256]) = 64 AND [VerifiedAtUtc] <= [SubmittedAtUtc]");
            table.HasCheckConstraint("CK_FiscalImportBatches_Review", """
                ([Status] = 1 AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL AND [ReviewReason] IS NULL)
                OR ([Status] IN (2, 3) AND [ReviewedByUserId] IS NOT NULL
                    AND [ReviewedByUserId] <> [SubmittedByUserId]
                    AND [ReviewedAtUtc] >= [SubmittedAtUtc] AND [ReviewReason] IS NOT NULL)
                """);
        });
        builder.HasKey(value => value.Id);
        builder.HasAlternateKey(value => new { value.Id, value.SubmittedByUserId });
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Source).HasConversion<int>().IsRequired();
        builder.Property(value => value.CodeSystem).HasConversion<int>().IsRequired();
        builder.Property(value => value.Status).HasConversion<int>().IsRequired();
        builder.Property(value => value.SourceVersion).HasMaxLength(80).IsRequired();
        builder.Property(value => value.SourceUrl).HasMaxLength(500).IsRequired();
        builder.Property(value => value.ArtifactSha256).HasMaxLength(64).IsRequired()
            .IsUnicode(false);
        builder.Property(value => value.ContentSha256).HasMaxLength(64).IsRequired()
            .IsUnicode(false);
        builder.Property(value => value.ReviewReason).HasMaxLength(500);
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => new { value.Source, value.CodeSystem, value.SourceVersion });
        builder.HasOne<AenaonUser>().WithMany().HasForeignKey(value => value.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AenaonUser>().WithMany().HasForeignKey(value => value.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(value => value.Rows).WithOne().HasForeignKey(value => value.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(value => value.Rows).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
