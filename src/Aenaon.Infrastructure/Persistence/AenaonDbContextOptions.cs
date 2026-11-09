using Aenaon.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

public static class AenaonDbContextOptions
{
    public static DbContextOptions<AenaonDbContext> Create(string connectionString, bool allowUntrustedCertificate)
    {
        string validated = SqlConnectionPolicy.CreateBuilder(connectionString, allowUntrustedCertificate).ConnectionString;
        return new DbContextOptionsBuilder<AenaonDbContext>()
            .UseSqlServer(validated, options => options.UseCompatibilityLevel(160).CommandTimeout(30)
                .MigrationsHistoryTable("__EFMigrationsHistory", "dbo"))
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false)
            .Options;
    }
}
