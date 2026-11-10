using Aenaon.Domain.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Κλειδώνει τις εκδόσεις και τα namespaces των φορολογικών κωδικών.</summary>
internal sealed class FiscalReferenceConfiguration : IEntityTypeConfiguration<FiscalReferenceRecord>
{
    public void Configure(EntityTypeBuilder<FiscalReferenceRecord> builder)
    {
        builder.ToTable("FiscalReferenceCodes", "dbo", table =>
        {
            table.HasCheckConstraint("CK_FiscalReferences_Source", "[Source] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_FiscalReferences_CodeSystem", "[CodeSystem] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_FiscalReferences_Period",
                "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
            table.HasCheckConstraint("CK_FiscalReferences_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_FiscalReferences_Version", "LEN(LTRIM(RTRIM([SourceVersion]))) > 0");
        });

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Source).HasConversion<int>().IsRequired();
        builder.Property(value => value.CodeSystem).HasConversion<int>().IsRequired();
        builder.Property(value => value.Code).HasMaxLength(FiscalReferenceCode.MaxCodeLength)
            .UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(value => value.SourceVersion).HasMaxLength(FiscalReferenceCode.MaxSourceVersionLength)
            .UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(value => value.Description).HasMaxLength(250).IsRequired();
        builder.Property(value => value.SourceUrl).HasMaxLength(500).IsRequired();
        builder.Property(value => value.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(value => value.EffectiveUntil).HasColumnType("date");
        builder.Property(value => value.VerifiedAtUtc).IsRequired();

        builder.HasIndex(value => new { value.Source, value.CodeSystem, value.Code,
            value.SourceVersion, value.EffectiveFrom }).IsUnique();
    }
}
