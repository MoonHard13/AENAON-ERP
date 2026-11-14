using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Fiscal;
using Aenaon.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

public sealed class AenaonDbContext(DbContextOptions<AenaonDbContext> options) : IdentityUserContext<AenaonUser, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<CatalogCategory> CatalogCategories => Set<CatalogCategory>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<PriceListAuditEntry> PriceListAuditEntries => Set<PriceListAuditEntry>();
    public DbSet<PriceListAuditEntry> PriceListAuditEntries => Set<PriceListAuditEntry>();
    public DbSet<FiscalReferenceRecord> FiscalReferenceCodes => Set<FiscalReferenceRecord>();
    public DbSet<FiscalTaxMappingRecord> FiscalTaxMappings => Set<FiscalTaxMappingRecord>();
    public DbSet<FiscalImportBatch> FiscalImportBatches => Set<FiscalImportBatch>();
    public DbSet<FiscalImportBatchRow> FiscalImportBatchRows => Set<FiscalImportBatchRow>();
    public DbSet<FiscalImportReviewRecord> FiscalImportReviews => Set<FiscalImportReviewRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IdentityModelConfiguration.Apply(modelBuilder);
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new StoreConfiguration());
        modelBuilder.ApplyConfiguration(new TenantMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new CatalogCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new CatalogItemConfiguration());
        modelBuilder.ApplyConfiguration(new PriceListConfiguration());
        modelBuilder.ApplyConfiguration(new PriceListItemConfiguration());
        modelBuilder.ApplyConfiguration(new PriceListAuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new PriceListAuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new FiscalReferenceConfiguration());
        modelBuilder.ApplyConfiguration(new FiscalTaxMappingConfiguration());
        modelBuilder.ApplyConfiguration(new FiscalImportBatchConfiguration());
        modelBuilder.ApplyConfiguration(new FiscalImportBatchRowConfiguration());
        modelBuilder.ApplyConfiguration(new FiscalImportReviewConfiguration());
    }
}
