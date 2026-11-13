using Aenaon.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Δεσμεύει κάθε τιμή στον ίδιο Tenant με τον τιμοκατάλογο και το είδος.</summary>
internal sealed class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        builder.ToTable("PriceListItems", "dbo", table =>
        {
            table.HasCheckConstraint("CK_PriceListItems_Id",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_PriceListItems_TenantId",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_PriceListItems_UnitPrice",
                "[UnitPrice] >= 0 AND [UnitPrice] <= 999999999999.9999");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PriceListId).IsRequired();
        builder.Property(x => x.CatalogItemId).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.PriceListId, x.CatalogItemId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CatalogItemId });
        builder.HasOne<PriceList>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.PriceListId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CatalogItemId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
