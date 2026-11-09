using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Aenaon.Api.Configuration;

/// <summary>Παράγει migrations και SQL scripts χωρίς ανάγνωση προσωπικών secrets ή σύνδεση στη βάση.</summary>
public sealed class AenaonDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AenaonDbContext>
{
    public AenaonDbContext CreateDbContext(string[] args)
    {
        // Η μη προσβάσιμη διεύθυνση προορίζεται μόνο για παραγωγή αρχείων, ποτέ για database update.
        return new AenaonDbContext(AenaonDbContextOptions.Create(
            "Server=design-only.invalid;Database=AenaonERP_DesignOnly;Integrated Security=True;Encrypt=True;",
            allowUntrustedCertificate: false));
    }
}
