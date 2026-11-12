using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Διαμορφώνει τη μόνιμη αποθήκευση κατηγοριών με αυστηρή απομόνωση επιχειρήσεων.</summary>
internal sealed class CatalogCategoryConfiguration : IEntityTypeConfiguration<CatalogCategory>
{
    public void Configure(EntityTypeBuilder<CatalogCategory> builder)
    {
        builder.ToTable("CatalogCategories", "dbo", table =>
        {
            table.HasCheckConstraint("CK_CatalogCategories_Id",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_CatalogCategories_TenantId",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_CatalogCategories_Code",
                "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_CatalogCategories_Parent_NotSelf",
                "[ParentCategoryId] IS NULL OR [ParentCategoryId] <> [Id]");
            table.HasCheckConstraint("CK_CatalogCategories_Name",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
        });

        builder.HasKey(category => category.Id);
        builder.HasAlternateKey(category => new { category.TenantId, category.Id });
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.TenantId).IsRequired();
        builder.Property(category => category.Code).IsRequired()
            .HasMaxLength(CatalogCategory.MaxCodeLength).UseCollation("Greek_CI_AS");
        builder.Property(category => category.Name).IsRequired().HasMaxLength(CatalogCategory.MaxNameLength);
        builder.Property(category => category.IsActive).IsRequired();
        builder.Property(category => category.ParentCategoryId).IsRequired(false);
        builder.HasIndex(category => new { category.TenantId, category.ParentCategoryId });

        // Το σύνθετο κλειδί αποκλείει γονέα διαφορετικής επιχείρησης.
        builder.HasOne<CatalogCategory>().WithMany()
            .HasForeignKey(category => new { category.TenantId, category.ParentCategoryId })
            .HasPrincipalKey(category => new { category.TenantId, category.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => new { category.TenantId, category.Code }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(category => category.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
