using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Απομόνωση τιμοκαταλόγων και προαιρετική αντιστοίχιση στο σωστό κατάστημα.</summary>
internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("PriceLists", "dbo", table =>
        {
            table.HasCheckConstraint("CK_PriceLists_Id",
                "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_PriceLists_TenantId",
                "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_PriceLists_StoreId",
                "[StoreId] IS NULL OR [StoreId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_PriceLists_Code",
                "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_PriceLists_Name",
                "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_PriceLists_Currency",
                "[Currency] = 'EUR'");
            table.HasCheckConstraint("CK_PriceLists_Channel",
                "[Channel] BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_PriceLists_Period",
                "[ValidUntil] IS NULL OR [ValidUntil] >= [ValidFrom]");
        });

        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.StoreId).IsRequired(false);
        builder.Property(x => x.Code).IsRequired()
            .HasMaxLength(PriceList.MaxCodeLength).UseCollation("Greek_CI_AS");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(PriceList.MaxNameLength);
        builder.Property(x => x.Channel).HasConversion<int>().IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.PricesIncludeVat).IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnType("date").IsRequired();
        builder.Property(x => x.ValidUntil).HasColumnType("date").IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.StoreId, x.Channel, x.IsActive });

        // Το σύνθετο FK δεν επιτρέπει κατάστημα άλλης επιχείρησης.
        builder.HasOne<Store>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.StoreId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
