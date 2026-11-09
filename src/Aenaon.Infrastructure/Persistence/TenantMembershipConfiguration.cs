using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("TenantMemberships", "dbo", table =>
            table.HasCheckConstraint("CK_TenantMemberships_Role", "[Role] IN (1, 2)"));
        builder.HasKey(membership => new { membership.TenantId, membership.UserId });
        builder.HasIndex(membership => membership.UserId);
        builder.Property(membership => membership.Role).HasConversion<int>().IsRequired();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AenaonUser>().WithMany().HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
