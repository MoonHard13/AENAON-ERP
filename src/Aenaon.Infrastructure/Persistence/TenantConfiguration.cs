using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Tenants_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Tenants_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();
        builder.Property(tenant => tenant.Name).HasMaxLength(Tenant.MaxNameLength).IsRequired();
        builder.HasMany(tenant => tenant.Stores).WithOne().HasForeignKey(store => store.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(tenant => tenant.Stores).HasField("_stores").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
