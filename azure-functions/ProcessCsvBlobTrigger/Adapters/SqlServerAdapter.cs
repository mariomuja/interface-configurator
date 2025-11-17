using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Core.Services;
using ProcessCsvBlobTrigger.Data;

namespace ProcessCsvBlobTrigger.Adapters;

/// <summary>
/// SQL Server Adapter for reading from and writing to SQL Server tables
/// </summary>
public class SqlServerAdapter : IAdapter
{
    public string AdapterName => "SqlServer";

    private readonly ApplicationDbContext _context;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IDataService _dataService;
    private readonly ILogger<SqlServerAdapter>? _logger;
    private readonly CsvColumnAnalyzer _columnAnalyzer;
    private readonly TypeValidator _typeValidator;

    public SqlServerAdapter(
        ApplicationDbContext context,
        IDynamicTableService dynamicTableService,
        IDataService dataService,
        ILogger<SqlServerAdapter>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dynamicTableService = dynamicTableService ?? throw new ArgumentNullException(nameof(dynamicTableService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _logger = logger;
        _columnAnalyzer = new CsvColumnAnalyzer();
        _typeValidator = new TypeValidator();
    }

    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source table name cannot be empty", nameof(source));

        try
        {
            _logger?.LogInformation("Reading data from SQL Server table: {Source}", source);

            // Ensure database exists
            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);

            // Get current table structure
            var columnTypes = await _dynamicTableService.GetCurrentTableStructureAsync(cancellationToken);

            if (columnTypes.Count == 0)
            {
                _logger?.LogWarning("Table {Source} has no columns or does not exist", source);
                return (new List<string>(), new List<Dictionary<string, string>>());
            }

            var headers = columnTypes.Keys.ToList();

            // Build SELECT query
            var sanitizedColumns = headers.Select(SanitizeColumnName).ToList();
            var columnList = string.Join(", ", sanitizedColumns);
            var selectSql = $"SELECT {columnList} FROM [{source}] ORDER BY datetime_created";

            // Execute query
            var records = new List<Dictionary<string, string>>();
            var connection = _context.Database.GetDbConnection();
            
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = selectSql;

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var record = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Count; i++)
                    {
                        var header = headers[i];
                        var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                        record[header] = value;
                    }
                    records.Add(record);
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            _logger?.LogInformation("Successfully read {RecordCount} records from SQL Server table: {Source}", records.Count, source);

            return (headers, records);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading from SQL Server table: {Source}", source);
            throw;
        }
    }

    public async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination table name cannot be empty", nameof(destination));

        if (headers == null || headers.Count == 0)
            throw new ArgumentException("Headers cannot be empty", nameof(headers));

        try
        {
            _logger?.LogInformation("Writing {RecordCount} records to SQL Server table: {Destination}", records?.Count ?? 0, destination);

            // Ensure database exists
            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);

            // Analyze column types from records
            var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();
            foreach (var header in headers)
            {
                var values = records?
                    .Select(r => r.GetValueOrDefault(header, string.Empty))
                    .ToList() ?? new List<string>();

                var typeInfo = _columnAnalyzer.AnalyzeColumn(header, values);
                columnTypes[header] = typeInfo;
            }

            // Ensure table structure matches
            await EnsureDestinationStructureAsync(destination, columnTypes, cancellationToken);

            // Insert rows using DataService
            if (records != null && records.Count > 0)
            {
                await _dataService.InsertRowsAsync(records, columnTypes, cancellationToken);
            }

            _logger?.LogInformation("Successfully wrote {RecordCount} records to SQL Server table: {Destination}", records?.Count ?? 0, destination);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing to SQL Server table: {Destination}", destination);
            throw;
        }
    }

    public async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source table name cannot be empty", nameof(source));

        try
        {
            _logger?.LogInformation("Getting schema from SQL Server table: {Source}", source);

            // Ensure database exists
            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);

            // Get current table structure
            var columnTypes = await _dynamicTableService.GetCurrentTableStructureAsync(cancellationToken);

            _logger?.LogInformation("Retrieved schema from SQL Server table {Source}: {ColumnCount} columns", source, columnTypes.Count);

            return columnTypes;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting schema from SQL Server table: {Source}", source);
            throw;
        }
    }

    public async Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination table name cannot be empty", nameof(destination));

        try
        {
            _logger?.LogInformation("Ensuring destination structure for SQL Server table: {Destination}", destination);

            // Ensure database exists
            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);

            // Use DynamicTableService to ensure table structure
            await _dynamicTableService.EnsureTableStructureAsync(columnTypes, cancellationToken);

            _logger?.LogInformation("Destination structure ensured for SQL Server table: {Destination}", destination);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ensuring destination structure for SQL Server table: {Destination}", destination);
            throw;
        }
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
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }
}

