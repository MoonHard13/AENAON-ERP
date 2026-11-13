using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Δεσμεύει κάθε είδος στον Tenant και σε κατηγορία του ίδιου Tenant.</summary>
internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("CatalogItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_CatalogItems_Id",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_CatalogItems_TenantId",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_CatalogItems_CategoryId",
                "[CategoryId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_CatalogItems_Code",
                "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_CatalogItems_Name",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_CatalogItems_Kind", "[Kind] IN (1, 2)");
            table.HasCheckConstraint("CK_CatalogItems_ServiceStock",
                "[Kind] <> 2 OR [TracksStock] = 0");
            table.HasCheckConstraint("CK_CatalogItems_UnitOfMeasure",
                "[UnitOfMeasure] BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_CatalogItems_AccountingType",
                "[AccountingType] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_CatalogItems_CommercialProfile",
                "([Kind] = 2 AND [AccountingType] = 5 AND [UnitOfMeasure] IN (7, 8)) " +
                "OR ([Kind] = 1 AND [AccountingType] <> 5 AND [UnitOfMeasure] <> 8)");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.Id });
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.CategoryId).IsRequired();
        builder.Property(item => item.Code).IsRequired()
            .HasMaxLength(CatalogItem.MaxCodeLength).UseCollation("Greek_CI_AS");
        builder.Property(item => item.Name).IsRequired().HasMaxLength(CatalogItem.MaxNameLength);
        builder.Property(item => item.Kind).HasConversion<int>().IsRequired();
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.TracksStock).IsRequired();
        builder.Property(item => item.UnitOfMeasure).HasConversion<int>().IsRequired();
        builder.Property(item => item.AccountingType).HasConversion<int>().IsRequired();

        builder.HasIndex(item => new { item.TenantId, item.Code }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CategoryId });

        // Το σύνθετο foreign key απαγορεύει αναφορά σε κατηγορία άλλης επιχείρησης.
        builder.HasOne<CatalogCategory>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.CategoryId })
            .HasPrincipalKey(category => new { category.TenantId, category.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
