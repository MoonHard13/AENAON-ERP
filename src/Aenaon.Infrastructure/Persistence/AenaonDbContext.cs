using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

public sealed class AenaonDbContext(DbContextOptions<AenaonDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new StoreConfiguration());
    }
}
