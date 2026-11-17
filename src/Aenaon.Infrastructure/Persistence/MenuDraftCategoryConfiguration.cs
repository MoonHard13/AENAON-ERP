using Aenaon.Domain.Catalog;
using Aenaon.Domain.Menus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Σύνθετο FK προς τον ίδιο Tenant και MenuDraft, χωρίς σύνδεση με CatalogCategory.</summary>
internal sealed class MenuDraftCategoryConfiguration : IEntityTypeConfiguration<MenuDraftCategory>
{
    public void Configure(EntityTypeBuilder<MenuDraftCategory> builder)
    {
        builder.ToTable("MenuDraftCategories", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MenuDraftCategories_Position", "[Position] BETWEEN 1 AND 10000");
            table.HasCheckConstraint("CK_MenuDraftCategories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.MenuId, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(MenuDraftCategory.MaxNameLength).IsRequired();
        builder.Property(x => x.IsVisible).IsRequired();
        builder.Property(x => x.CatalogCategoryId).IsRequired(false);
        builder.Property(x => x.Position).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.MenuId, x.Position, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.MenuId, x.CatalogCategoryId })
            .IsUnique().HasFilter("[CatalogCategoryId] IS NOT NULL");
        builder.HasOne<CatalogCategory>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CatalogCategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MenuDraft>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MenuId })
            .HasPrincipalKey(x => new { x.TenantId, x.MenuId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
