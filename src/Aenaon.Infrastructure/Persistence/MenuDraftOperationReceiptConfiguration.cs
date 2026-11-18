using Aenaon.Domain.Menus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Αδιάβλητη ταυτότητα αιτήματος και δεσμευτική σύνδεση μενού ανά Tenant.</summary>
internal sealed class MenuDraftOperationReceiptConfiguration : IEntityTypeConfiguration<MenuDraftOperationReceipt>
{
    public void Configure(EntityTypeBuilder<MenuDraftOperationReceipt> builder)
    {
        builder.ToTable("MenuDraftOperationReceipts", "dbo", table =>
        {
            table.HasCheckConstraint("CK_MenuDraftReceipts_Revision", "[Revision] >= 1");
        });
        builder.HasKey(x => new { x.TenantId, x.RequestId });
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired().IsUnicode(false);
        builder.Property(x => x.OccurredUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.MenuId, x.OccurredUtc });
        builder.HasOne<RestaurantMenu>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MenuId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
