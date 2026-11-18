using Aenaon.Domain.Menus;
using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Ένας προεπιλεγμένος στόχος με σύνθετα foreign keys στο ίδιο Tenant.</summary>
internal sealed class StoreDefaultMenuConfiguration : IEntityTypeConfiguration<StoreDefaultMenu>
{
    public void Configure(EntityTypeBuilder<StoreDefaultMenu> builder)
    {
        builder.ToTable("StoreDefaultMenus", "dbo");
        builder.HasKey(x => new { x.TenantId, x.StoreId });
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.StoreId).IsRequired();
        builder.Property(x => x.MenuId).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.MenuId });
        builder.HasOne<Store>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.StoreId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RestaurantMenu>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MenuId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
