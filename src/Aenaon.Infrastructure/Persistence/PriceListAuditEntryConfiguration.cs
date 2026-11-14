using Aenaon.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Μόνιμο, απομονωμένο ανά Tenant εμπορικό ιστορικό με ασφαλή αναζήτηση.</summary>
internal sealed class PriceListAuditEntryConfiguration : IEntityTypeConfiguration<PriceListAuditEntry>
{
    public void Configure(EntityTypeBuilder<PriceListAuditEntry> builder)
    {
        builder.ToTable("PriceListAuditEntries", "dbo", table =>
        {
            // Το SQL trigger απαιτεί να μην χρησιμοποιείται το OUTPUT χωρίς INTO.
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_PriceListAuditEntries_Action", "[Action] BETWEEN 1 AND 7");
            table.HasCheckConstraint("CK_PriceListAuditEntries_Source",
                "[Source] IN ('Web', 'CLI', 'System')");
            table.HasCheckConstraint("CK_PriceListAuditEntries_Item",
                "([Action] BETWEEN 1 AND 4 AND [CatalogItemId] IS NULL) OR " +
                "([Action] BETWEEN 5 AND 7 AND [CatalogItemId] IS NOT NULL)");
            table.HasCheckConstraint("CK_PriceListAuditEntries_Prices",
                "([BeforePrice] IS NULL OR [BeforePrice] >= 0) AND " +
                "([AfterPrice] IS NULL OR [AfterPrice] >= 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PriceListId).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Action).HasConversion<int>().IsRequired();
        builder.Property(x => x.OccurredUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.BeforePrice).HasPrecision(18, 4);
        builder.Property(x => x.AfterPrice).HasPrecision(18, 4);
        builder.Property(x => x.BeforePolicy).HasMaxLength(1024);
        builder.Property(x => x.AfterPolicy).HasMaxLength(1024);
        builder.HasIndex(x => new { x.TenantId, x.PriceListId, x.OccurredUtc, x.Id });
        builder.HasOne<PriceList>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.PriceListId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
