using Aenaon.Domain.Catalog;
using Aenaon.Domain.Menus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Το είδος ανήκει στο Tenant/Menu, με προαιρετική κατηγορία του ίδιου πεδίου.</summary>
internal sealed class MenuDraftItemConfiguration : IEntityTypeConfiguration<MenuDraftItem>
{
    public void Configure(EntityTypeBuilder<MenuDraftItem> builder)
    {
        builder.ToTable("MenuDraftItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MenuDraftItems_Position", "[Position] BETWEEN 1 AND 10000");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.NameOverride).HasMaxLength(MenuDraftItem.MaxNameOverrideLength)
            .IsRequired(false);
        builder.Property(x => x.Position).IsRequired();
        builder.Property(x => x.MenuCategoryId).IsRequired(false);
        builder.HasIndex(x => new { x.TenantId, x.MenuId, x.MenuCategoryId, x.CatalogItemId })
            .IsUnique().HasFilter("[MenuCategoryId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.MenuId, x.CatalogItemId })
            .IsUnique().HasFilter("[MenuCategoryId] IS NULL");
        builder.HasIndex(x => new { x.TenantId, x.MenuId, x.MenuCategoryId, x.Position, x.Id });
        builder.HasOne<MenuDraftCategory>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MenuId, x.MenuCategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.MenuId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        // Τα είδη ρίζας δεν έχουν MenuCategoryId, επομένως χρειάζονται αυτοτελές FK προς το μενού.
        builder.HasOne<MenuDraft>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MenuId })
            .HasPrincipalKey(x => new { x.TenantId, x.MenuId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CatalogItemId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
