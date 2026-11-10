using Aenaon.Infrastructure.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Αποθηκεύει τις επιθεωρημένες εγγραφές αυστηρά μέσα σε μία υποβολή.</summary>
internal sealed class FiscalImportBatchRowConfiguration : IEntityTypeConfiguration<FiscalImportBatchRow>
{
    public void Configure(EntityTypeBuilder<FiscalImportBatchRow> builder)
    {
        builder.ToTable("FiscalImportBatchRows", "dbo", table =>
        {
            table.HasCheckConstraint("CK_FiscalImportBatchRows_Period",
                "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
            table.HasCheckConstraint("CK_FiscalImportBatchRows_Code",
                "LEN(LTRIM(RTRIM([Code]))) > 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Code).HasMaxLength(64).IsRequired()
            .UseCollation("Latin1_General_100_BIN2");
        builder.Property(value => value.Description).HasMaxLength(250).IsRequired();
        builder.Property(value => value.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(value => value.EffectiveUntil).HasColumnType("date");
        builder.HasIndex(value => new { value.BatchId, value.Code, value.EffectiveFrom }).IsUnique();
    }
}
