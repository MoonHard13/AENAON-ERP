using Microsoft.Data.SqlClient;

namespace Aenaon.Infrastructure.Database;

public static class SqlConnectionConfiguration
{
    public static string ForDevelopment(string server, string database, string login, string password,
        bool trustServerCertificate)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = login,
            Password = password,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = trustServerCertificate
        };
        return SqlConnectionPolicy.CreateBuilder(builder.ConnectionString, allowUntrustedCertificate: true).ConnectionString;
    }
}
