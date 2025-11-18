using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for dynamically creating and managing SQL tables based on CSV structure
/// </summary>
public class DynamicTableService : IDynamicTableService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DynamicTableService>? _logger;

    public DynamicTableService(ApplicationDbContext context, ILogger<DynamicTableService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public async Task EnsureTableStructureAsync(Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure database exists
            await _context.Database.EnsureCreatedAsync(cancellationToken);

            // Check if TransportData table exists
            var tableExists = await TableExistsAsync("TransportData", cancellationToken);

            if (!tableExists)
            {
                await CreateTableAsync(columnTypes, cancellationToken);
            }
            else
            {
                // Check if table has old structure (CsvDataJson or Id instead of PrimaryKey)
                var hasOldStructure = await HasOldTableStructureAsync(cancellationToken);
                
                if (hasOldStructure)
                {
                    _logger?.LogWarning("TransportData table has old structure (CsvDataJson or Id). Dropping and recreating with new structure.");
                    await DropTableAsync("TransportData", cancellationToken);
                    await CreateTableAsync(columnTypes, cancellationToken);
                }
                else
                {
                    // Add missing columns
                    await AddMissingColumnsAsync(columnTypes, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ensuring table structure");
            throw;
        }
    }

    private async Task<bool> HasOldTableStructureAsync(CancellationToken cancellationToken)
    {
        try
        {
            var query = @"
                SELECT c.name AS ColumnName
                FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                WHERE t.name = 'TransportData'
                AND (c.name = 'CsvDataJson' OR c.name = 'Id')
            ";
            
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            
            using var command = connection.CreateCommand();
            command.CommandText = query;
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var hasOldColumn = await reader.ReadAsync(cancellationToken);
            
            await connection.CloseAsync();
            
            return hasOldColumn;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking for old table structure");
            // If we can't check, assume it's old structure to be safe
            return true;
        }
    }

    private async Task DropTableAsync(string tableName, CancellationToken cancellationToken)
    {
        try
        {
            var dropSql = $"DROP TABLE [dbo].[{tableName}]";
            await _context.Database.ExecuteSqlRawAsync(dropSql, cancellationToken);
            _logger?.LogInformation("Dropped table {TableName}", tableName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error dropping table {TableName}", tableName);
            throw;
        }
    }

    public async Task AddMissingColumnsAsync(Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        var existingColumns = await GetCurrentTableStructureAsync(cancellationToken);

        foreach (var column in columnTypes)
        {
            var columnName = SanitizeColumnName(column.Key);
            
            // Skip reserved columns (case-insensitive):
            // - 'id' or 'Id' column: PrimaryKey is handled separately
            // - 'CsvDataJson': Old JSON column approach, not used anymore
            var reservedColumns = new[] { "id", "Id", "CsvDataJson", "PrimaryKey", "datetime_created" };
            if (reservedColumns.Any(rc => string.Equals(columnName, rc, StringComparison.OrdinalIgnoreCase)))
                continue;
            
            // Check if column already exists (case-insensitive comparison)
            var columnExists = existingColumns.Keys.Any(ec => string.Equals(ec, columnName, StringComparison.OrdinalIgnoreCase));
            if (columnExists)
            {
                _logger?.LogDebug("Column {ColumnName} already exists, skipping", columnName);
                continue;
            }
            
            await AddColumnAsync(columnName, column.Value, cancellationToken);
        }
    }

    public async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetCurrentTableStructureAsync(CancellationToken cancellationToken = default)
    {
        var columns = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        try
        {
            // Query SQL Server system tables to get column information
            // Exclude PrimaryKey, datetime_created, and old Id column (case-insensitive check needed)
            var query = @"
                SELECT 
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.max_length AS MaxLength,
                    c.precision AS Precision,
                    c.scale AS Scale,
                    c.is_nullable AS IsNullable
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID('dbo.TransportData')
                AND c.name NOT IN ('PrimaryKey', 'datetime_created', 'Id', 'CsvDataJson')
                ORDER BY c.column_id";

            // Use raw SQL to query system tables
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            
            using var command = connection.CreateCommand();
            command.CommandText = query;
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var maxLength = reader.IsDBNull(2) ? -1 : reader.GetInt16(2);
                var precision = reader.IsDBNull(3) ? (byte)0 : reader.GetByte(3);
                var scale = reader.IsDBNull(4) ? (byte)0 : reader.GetByte(4);
                var isNullable = reader.GetBoolean(5);

                // Convert SQL Server data type to our ColumnTypeInfo
                var columnType = MapSqlTypeToColumnTypeInfo(dataType, maxLength, precision, scale);
                columns[columnName] = columnType;
            }

            await connection.CloseAsync();
            _logger?.LogInformation("Retrieved {ColumnCount} columns from TransportData table", columns.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting current table structure");
            // Return empty dictionary if table doesn't exist or query fails
        }

        return columns;
    }

    private CsvColumnAnalyzer.ColumnTypeInfo MapSqlTypeToColumnTypeInfo(string sqlType, int maxLength, byte precision, byte scale)
    {
        // Map SQL Server types to our ColumnTypeInfo
        var sqlTypeUpper = sqlType.ToUpperInvariant();
        
        return sqlTypeUpper switch
        {
            "INT" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.INT, 
                SqlTypeDefinition = "INT" 
            },
            "BIGINT" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.INT, // Map BIGINT to INT for compatibility
                SqlTypeDefinition = "BIGINT" 
            },
            "DECIMAL" or "NUMERIC" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.DECIMAL,
                Precision = precision,
                Scale = scale,
                SqlTypeDefinition = $"DECIMAL({precision},{scale})" 
            },
            "FLOAT" or "REAL" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.DECIMAL, // Map FLOAT to DECIMAL
                SqlTypeDefinition = "FLOAT" 
            },
            "BIT" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.BIT, 
                SqlTypeDefinition = "BIT" 
            },
            "DATETIME2" or "DATETIME" or "DATE" or "TIME" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.DATETIME2, 
                SqlTypeDefinition = "DATETIME2" 
            },
            "UNIQUEIDENTIFIER" => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.UNIQUEIDENTIFIER, 
                SqlTypeDefinition = "UNIQUEIDENTIFIER" 
            },
            _ => new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = maxLength == -1 ? null : maxLength,
                SqlTypeDefinition = maxLength == -1 || maxLength > 4000 
                    ? "NVARCHAR(MAX)" 
                    : $"NVARCHAR({maxLength})" 
            }
        };
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        try
        {
            // Sanitize table name to prevent SQL injection
            var sanitizedTableName = SanitizeTableName(tableName);
            // Use square brackets to safely escape the table name
            // The table name is sanitized to only contain alphanumeric characters and underscores, so it's safe
#pragma warning disable EF1002 // SQL injection warning - table name is sanitized
            var result = await _context.Database.ExecuteSqlRawAsync(
                $"SELECT TOP 1 1 FROM [{sanitizedTableName}]", cancellationToken);
#pragma warning restore EF1002
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private string SanitizeTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        
        // Remove any characters that are not alphanumeric or underscore
        var sanitized = new string(tableName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Table name must contain at least one alphanumeric character", nameof(tableName));
        
        // Ensure it doesn't start with a number
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        
        return sanitized;
    }

    private async Task CreateTableAsync(Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken)
    {
        var columnDefinitions = new List<string>
        {
            "[PrimaryKey] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID()",
            "[datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE()"
        };

        foreach (var column in columnTypes)
        {
            var columnName = SanitizeColumnName(column.Key);
            // Skip reserved columns (case-insensitive):
            // - 'id' column: PrimaryKey is handled separately
            // - 'CsvDataJson': Old JSON column approach, not used anymore
            var reservedColumns = new[] { "id", "CsvDataJson", "PrimaryKey", "datetime_created" };
            if (reservedColumns.Any(rc => string.Equals(columnName, rc, StringComparison.OrdinalIgnoreCase)))
                continue;
            
            var sqlType = column.Value.SqlTypeDefinition;
            var nullable = "NULL"; // All CSV columns are nullable to handle missing values

            columnDefinitions.Add($"[{columnName}] {sqlType} {nullable}");
        }

        var createTableSql = $@"
            CREATE TABLE [dbo].[TransportData] (
                {string.Join(",\n                ", columnDefinitions)}
            );
            
            CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);";

        await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
        _logger?.LogInformation("Created TransportData table with {ColumnCount} columns", columnTypes.Count);
    }

    private async Task AddColumnAsync(string columnName, CsvColumnAnalyzer.ColumnTypeInfo columnType, CancellationToken cancellationToken)
    {
        // Double-check: Skip reserved columns
        var reservedColumns = new[] { "id", "CsvDataJson", "PrimaryKey", "datetime_created" };
        if (reservedColumns.Any(rc => string.Equals(columnName, rc, StringComparison.OrdinalIgnoreCase)))
        {
            _logger?.LogWarning("Skipping reserved column {ColumnName}", columnName);
            return;
        }
        
        var sqlType = columnType.SqlTypeDefinition;
        var addColumnSql = $@"
            ALTER TABLE [dbo].[TransportData]
            ADD [{columnName}] {sqlType} NULL;";

        await _context.Database.ExecuteSqlRawAsync(addColumnSql, cancellationToken);
        _logger?.LogInformation("Added column {ColumnName} ({SqlType}) to TransportData table", columnName, sqlType);
    }

    private string SanitizeColumnName(string columnName)
    {
        // Remove special characters and ensure valid SQL identifier
        var sanitized = columnName
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace(".", "_")
            .Replace("/", "_")
            .Replace("\\", "_");

        // Ensure it starts with a letter
        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }
}

