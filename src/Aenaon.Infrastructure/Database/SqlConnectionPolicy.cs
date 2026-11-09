using Microsoft.Data.SqlClient;

namespace Aenaon.Infrastructure.Database;

internal static class SqlConnectionPolicy
{
    public static SqlConnectionStringBuilder CreateBuilder(string connectionString, bool allowUntrustedCertificate)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(builder.InitialCatalog.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Απαιτούνται server και ξεχωριστή βάση εφαρμογής.");
        }

        if (builder.Encrypt.Equals(SqlConnectionEncryptOption.Optional) ||
            (builder.TrustServerCertificate && !allowUntrustedCertificate))
        {
            throw new ArgumentException("Απαιτείται κρυπτογράφηση και έγκυρος έλεγχος πιστοποιητικού εκτός Development.");
        }

        if ((!builder.IntegratedSecurity &&
             (string.IsNullOrWhiteSpace(builder.UserID) || string.IsNullOrEmpty(builder.Password))) ||
            builder.Authentication is not SqlAuthenticationMethod.NotSpecified and not SqlAuthenticationMethod.SqlPassword ||
            (builder.IntegratedSecurity && builder.Authentication != SqlAuthenticationMethod.NotSpecified))
        {
            throw new ArgumentException("Υποστηρίζονται Windows Authentication και SQL Authentication με διαπιστευτήρια.");
        }

        builder.PersistSecurityInfo = false;
        builder.ConnectTimeout = 5;
        builder.ConnectRetryCount = 0;
        builder.ApplicationName = "Aenaon ERP";
        builder.Enlist = false;
        return builder;
    }
}
