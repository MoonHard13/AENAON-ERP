using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aenaon.Infrastructure.Persistence;

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Stores_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Stores_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_Stores_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
        });
        builder.HasKey(store => store.Id);
        builder.Property(store => store.Id).ValueGeneratedNever();
        builder.Property(store => store.TenantId).IsRequired();
        builder.Property(store => store.Name).HasMaxLength(Store.MaxNameLength).IsRequired();
        // Η μοναδικότητα παραμένει ανεξάρτητη από πεζά/κεφαλαία και σε απευθείας εγγραφές SQL.
        builder.Property(store => store.Code).HasMaxLength(Store.MaxCodeLength).IsRequired()
            .UseCollation("Greek_CI_AS");
        builder.HasIndex(store => new { store.TenantId, store.Code }).IsUnique();
    }
}
