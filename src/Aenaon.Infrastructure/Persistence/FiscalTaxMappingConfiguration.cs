using Aenaon.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Προστατεύει τον Tenant και αποτρέπει την αποθήκευση ανύπαρκτων κωδικών αναφοράς.</summary>
internal sealed class FiscalTaxMappingConfiguration : IEntityTypeConfiguration<FiscalTaxMappingRecord>
{
    public void Configure(EntityTypeBuilder<FiscalTaxMappingRecord> builder)
    {
        builder.ToTable("FiscalTaxMappings", "dbo", table =>
        {
            table.HasCheckConstraint("CK_FiscalMappings_Channel", "[CustomerChannel] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_FiscalMappings_Period",
                "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
            table.HasCheckConstraint("CK_FiscalMappings_Tenant",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
        });

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.CatalogItemId).IsRequired();
        builder.Property(value => value.CustomerChannel).HasConversion<int>().IsRequired();
        builder.Property(value => value.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(value => value.EffectiveUntil).HasColumnType("date");
        builder.Property(value => value.CreatedAtUtc).IsRequired();

        builder.HasIndex(value => new
        {
            value.TenantId, value.CatalogItemId, value.CustomerChannel, value.EffectiveFrom
        }).IsUnique();

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(value => new { value.TenantId, value.CatalogItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FiscalReferenceRecord>().WithMany()
            .HasForeignKey(value => value.VatCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FiscalReferenceRecord>().WithMany()
            .HasForeignKey(value => value.IncomeCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FiscalReferenceRecord>().WithMany()
            .HasForeignKey(value => value.IncomeTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FiscalReferenceRecord>().WithMany()
            .HasForeignKey(value => value.VatExemptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FiscalReferenceRecord>().WithMany()
            .HasForeignKey(value => value.MeasurementUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FiscalReferenceRecord>().WithMany()
            .HasForeignKey(value => value.MeasurementUnitEnId).OnDelete(DeleteBehavior.Restrict);
    }
}
