using Aenaon.Domain.Menus;
using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Η ταυτότητα του μενού διατηρεί καθολική ιδιοκτησία Tenant.</summary>
internal sealed class RestaurantMenuConfiguration : IEntityTypeConfiguration<RestaurantMenu>
{
    public void Configure(EntityTypeBuilder<RestaurantMenu> builder)
    {
        builder.ToTable("RestaurantMenus", "dbo", table =>
        {
            table.HasCheckConstraint("CK_RestaurantMenus_TenantId", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_RestaurantMenus_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_RestaurantMenus_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_RestaurantMenus_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(RestaurantMenu.MaxCodeLength)
            .UseCollation("Greek_CI_AS").IsRequired();
        builder.Property(x => x.Name).HasMaxLength(RestaurantMenu.MaxNameLength).IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
