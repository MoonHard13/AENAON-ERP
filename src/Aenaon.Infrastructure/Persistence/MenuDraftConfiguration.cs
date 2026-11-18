using Aenaon.Domain.Menus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Μοναδική αναθεώρηση ανά Menu, με server-side optimistic concurrency.</summary>
internal sealed class MenuDraftConfiguration : IEntityTypeConfiguration<MenuDraft>
{
    public void Configure(EntityTypeBuilder<MenuDraft> builder)
    {
        builder.ToTable("MenuDrafts", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MenuDrafts_Revision", "[Revision] >= 1");
        });
        builder.HasKey(x => new { x.TenantId, x.MenuId });
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.MenuId).IsRequired();
        builder.Property(x => x.Revision).IsConcurrencyToken().IsRequired();
        builder.Property(x => x.EditedUtc).HasColumnType("datetime2").IsRequired();
        builder.HasOne<RestaurantMenu>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MenuId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
