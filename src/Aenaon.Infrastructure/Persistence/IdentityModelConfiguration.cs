using Aenaon.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

internal static class IdentityModelConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AenaonUser>(builder =>
        {
            builder.ToTable("AspNetUsers", "dbo", table =>
            {
                table.HasCheckConstraint("CK_AspNetUsers_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.HasCheckConstraint("CK_AspNetUsers_UserName", "LEN(LTRIM(RTRIM([UserName]))) > 0");
                table.HasCheckConstraint("CK_AspNetUsers_NormalizedUserName", "LEN(LTRIM(RTRIM([NormalizedUserName]))) > 0");
            });
            builder.Property(user => user.Id).ValueGeneratedNever();
            builder.Property(user => user.UserName).IsRequired();
            builder.Property(user => user.NormalizedUserName).IsRequired();
            builder.Property(user => user.PhoneNumber).HasMaxLength(256);
            builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex")
                .IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        });
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims", "dbo");
        modelBuilder.Entity<IdentityUserLogin<Guid>>(builder =>
        {
            builder.ToTable("AspNetUserLogins", "dbo");
            builder.Property(login => login.LoginProvider).HasMaxLength(128);
            builder.Property(login => login.ProviderKey).HasMaxLength(128);
        });
        modelBuilder.Entity<IdentityUserToken<Guid>>(builder =>
        {
            builder.ToTable("AspNetUserTokens", "dbo");
            builder.Property(token => token.LoginProvider).HasMaxLength(128);
            builder.Property(token => token.Name).HasMaxLength(128);
        });
    }
}
