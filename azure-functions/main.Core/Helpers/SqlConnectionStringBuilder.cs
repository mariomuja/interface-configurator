namespace ProcessCsvBlobTrigger.Core.Helpers;

/// <summary>
/// Helper class to build SQL Server connection strings from individual properties
/// </summary>
public static class SqlConnectionStringBuilder
{
    /// <summary>
    /// Builds a SQL Server connection string from individual properties
    /// </summary>
    /// <param name="serverName">SQL Server name or IP (e.g., "sql-server.database.windows.net" or "192.168.1.100")</param>
    /// <param name="databaseName">Database name</param>
    /// <param name="userName">SQL login username (required if integratedSecurity is false)</param>
    /// <param name="password">SQL password (required if integratedSecurity is false)</param>
    /// <param name="integratedSecurity">Use Windows Authentication (true) or SQL Authentication (false)</param>
    /// <param name="port">Port number (default: 1433 for SQL Server)</param>
    /// <returns>SQL Server connection string</returns>
    public static string BuildConnectionString(
        string? serverName,
        string? databaseName,
        string? userName = null,
        string? password = null,
        bool integratedSecurity = false,
        int port = 1433)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            throw new ArgumentException("Server name cannot be empty", nameof(serverName));
        
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        var parts = new List<string>();

        // Server
        if (serverName.Contains(","))
        {
            // Server already includes port
            parts.Add($"Server={serverName}");
        }
        else
        {
            // Add port if not Azure (Azure uses default port 1433)
            if (serverName.Contains(".database.windows.net"))
            {
                parts.Add($"Server=tcp:{serverName},{port}");
            }
            else
            {
                parts.Add($"Server={serverName},{port}");
            }
        }

        // Database
        parts.Add($"Initial Catalog={databaseName}");

        // Authentication
        if (integratedSecurity)
        {
            parts.Add("Integrated Security=True");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("User name is required when Integrated Security is false", nameof(userName));
            
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required when Integrated Security is false", nameof(password));

            parts.Add($"User ID={userName}");
            parts.Add($"Password={password}");
        }

        // Additional settings for Azure SQL
        if (serverName.Contains(".database.windows.net"))
        {
            parts.Add("Persist Security Info=False");
            parts.Add("MultipleActiveResultSets=False");
            parts.Add("Encrypt=True");
            parts.Add("TrustServerCertificate=False");
            parts.Add("Connection Timeout=30");
        }
        else
        {
            // Standard SQL Server settings
            parts.Add("Persist Security Info=False");
            parts.Add("MultipleActiveResultSets=True");
            parts.Add("Encrypt=False");
            parts.Add("Connection Timeout=30");
        }

        return string.Join(";", parts) + ";";
    }

    /// <summary>
    /// Builds a SQL Server connection string from InterfaceConfiguration properties
    /// </summary>
    public static string BuildConnectionStringFromConfig(ProcessCsvBlobTrigger.Core.Models.InterfaceConfiguration config)
    {
        return BuildConnectionString(
            config.SqlServerName,
            config.SqlDatabaseName,
            config.SqlUserName,
            config.SqlPassword,
            config.SqlIntegratedSecurity);
    }
}




