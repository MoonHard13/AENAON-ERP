using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Ίδιο transaction-scoped app lock για Catalog και MenuDraft mutations, ώστε
/// η αυτόματη ενημέρωση να μην χάνει εντολές σε παράλληλες συνεδρίες.
/// </summary>
internal static class MenuCatalogTenantLock
{
    public static async Task AcquireAsync(AenaonDbContext context, Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Απαιτείται ενεργή SQL transaction και έγκυρος Tenant.");
        }

        var connection = (SqlConnection)context.Database.GetDbConnection();
        await using SqlCommand command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)context.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            SELECT @result;
            """;
        command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value =
            "AenaonCatalog:" + tenantId.ToString("N", CultureInfo.InvariantCulture);

        int result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new TimeoutException("Το μενού ή ο κατάλογος τροποποιείται από άλλη συνεδρία.");
        }
    }
}
