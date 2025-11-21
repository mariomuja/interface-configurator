using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// SQL Server Adapter for reading from and writing to SQL Server tables
/// Uses ApplicationDbContext to access the main application database (app-database)
/// TransportData table is created/written in the main application database, NOT in MessageBox database
/// </summary>
public class SqlServerAdapter : AdapterBase
{
    public override string AdapterName => "SqlServer";
    public override string AdapterAlias => "SQL Server";
    public override bool SupportsRead => true;
    public override bool SupportsWrite => true;

    /// <summary>
    /// ApplicationDbContext connects to the configured SQL Server database
    /// Can use either the default context (from DI) or a custom connection string
    /// </summary>
    private readonly ApplicationDbContext? _defaultContext;
    private readonly string? _connectionString;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IDataService _dataService;
    private readonly ILogger<SqlServerAdapter>? _logger;
    private readonly CsvColumnAnalyzer _columnAnalyzer;
    private readonly TypeValidator _typeValidator;
    
    // Source-specific properties
    private readonly string? _pollingStatement;
    private readonly int _pollingInterval;
    private readonly string? _tableName;
    
    // General SQL Server adapter properties
    private readonly bool _useTransaction;
    private readonly int _commandTimeout;
    private readonly bool _failOnBadStatement;
    private readonly IInterfaceConfigurationService? _configService;
    private readonly ProcessingStatisticsService? _statisticsService;

    public SqlServerAdapter(
        ApplicationDbContext? context,
        IDynamicTableService dynamicTableService,
        IDataService dataService,
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        string? connectionString = null,
        string? pollingStatement = null,
        int? pollingInterval = null,
        string? tableName = null,
        bool? useTransaction = null,
        int? batchSize = null,
        int? commandTimeout = null,
        bool? failOnBadStatement = null,
        IInterfaceConfigurationService? configService = null,
        string adapterRole = "Source",
        ILogger<SqlServerAdapter>? logger = null,
        ProcessingStatisticsService? statisticsService = null)
        : base(
            messageBoxService: messageBoxService,
            subscriptionService: subscriptionService,
            interfaceName: interfaceName ?? "FromCsvToSqlServerExample",
            adapterInstanceGuid: adapterInstanceGuid,
            batchSize: batchSize ?? 1000,
            adapterRole: adapterRole,
            logger: logger)
    {
        if (context == null && string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Either context or connectionString must be provided", nameof(context));
        
        _defaultContext = context;
        _connectionString = connectionString;
        _dynamicTableService = dynamicTableService ?? throw new ArgumentNullException(nameof(dynamicTableService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _pollingStatement = pollingStatement;
        _pollingInterval = pollingInterval ?? 60;
        _tableName = tableName;
        _useTransaction = useTransaction ?? false;
        _commandTimeout = commandTimeout ?? 30;
        _failOnBadStatement = failOnBadStatement ?? false;
        _configService = configService;
        _logger = logger;
        _statisticsService = statisticsService;
        _columnAnalyzer = new CsvColumnAnalyzer();
        _typeValidator = new TypeValidator();
    }

    /// <summary>
    /// Gets the ApplicationDbContext, creating a new one with custom connection string if needed
    /// </summary>
    private ApplicationDbContext GetContext()
    {
        if (_defaultContext != null)
            return _defaultContext;

        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("Connection string is required when default context is not available");

        // Create DbContextOptions with custom connection string and connection pooling
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(_connectionString, options =>
        {
            // Enable connection pooling and optimize for performance
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            options.CommandTimeout(300); // 5 minutes timeout for large operations
            options.MaxBatchSize(1000); // Batch size for EF Core operations
        });
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        try
        {
            string selectSql;
            List<string> headers = new List<string>();

            // Use polling statement if provided (for source adapters)
            // If not provided, use default: "SELECT * FROM Table" where Table is _tableName
            if (!string.IsNullOrWhiteSpace(_pollingStatement))
            {
                selectSql = _pollingStatement;
                _logger?.LogInformation("Reading data using polling statement: {PollingStatement}", _pollingStatement);
            }
            else
            {
                // Use default polling statement if table name is provided
                if (!string.IsNullOrWhiteSpace(_tableName))
                {
                    selectSql = $"SELECT * FROM [{_tableName}]";
                    _logger?.LogInformation("Reading data using default polling statement: SELECT * FROM [{TableName}]", _tableName);
                }
                else if (!string.IsNullOrWhiteSpace(source))
                {
                    // Fallback to source parameter if table name not provided
                    selectSql = $"SELECT * FROM [{source}]";
                    _logger?.LogInformation("Reading data using source parameter: SELECT * FROM [{Source}]", source);
                }
                else
                {
                    throw new ArgumentException("Source table name or PollingStatement must be provided", nameof(source));
                }
            }

            // Execute query with batch size support
            var records = new List<Dictionary<string, string>>();
            using var context = GetContext();
            var connection = context.Database.GetDbConnection();
            
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = selectSql;
                command.CommandTimeout = _commandTimeout;
                
                _logger?.LogDebug("Using command timeout: {CommandTimeout} seconds, batch size: {BatchSize} for reading data", 
                    _commandTimeout, _batchSize);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                
                // Get column names from reader if headers not already determined
                if (headers.Count == 0)
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        headers.Add(reader.GetName(i));
                    }
                }

                // Read records in batches
                var batchBuffer = new List<Dictionary<string, string>>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    var record = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Count; i++)
                    {
                        var header = headers[i];
                        var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                        record[header] = value;
                    }
                    batchBuffer.Add(record);
                    
                    // Process batch when buffer reaches batch size
                    if (batchBuffer.Count >= _batchSize)
                    {
                        records.AddRange(batchBuffer);
                        batchBuffer.Clear();
                        _logger?.LogDebug("Processed batch of {BatchSize} records", _batchSize);
                    }
                }
                
                // Add remaining records
                if (batchBuffer.Count > 0)
                {
                    records.AddRange(batchBuffer);
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            _logger?.LogInformation("Successfully read {RecordCount} records from SQL Server table: {Source}", records.Count, source);

            // Write to MessageBox if AdapterRole is "Source"
            await WriteRecordsToMessageBoxAsync(headers, records, cancellationToken);

            return (headers, records);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading from SQL Server table: {Source}", source);
            throw;
        }
    }

    public override async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination table name cannot be empty", nameof(destination));

        try
        {
            _logger?.LogInformation("Writing {RecordCount} records to SQL Server table: {Destination}, AdapterRole: {AdapterRole}", 
                records?.Count ?? 0, destination, AdapterRole);

            // Read messages from MessageBox if AdapterRole is "Destination"
            List<MessageBoxMessage>? processedMessages = null;
            var messageBoxResult = await ReadMessagesFromMessageBoxAsync(cancellationToken);
            if (messageBoxResult.HasValue)
            {
                var (messageHeaders, messageRecords, messages) = messageBoxResult.Value;
                headers = messageHeaders;
                records = messageRecords;
                processedMessages = messages;
            }
            else if (AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
            {
                // No messages found, but continue to fallback: use provided records if available
                _logger?.LogWarning("No messages found in MessageBox. Will use provided records if available.");
            }
            
            // If no messages were read from MessageBox, use provided records (fallback for direct calls)
            // This allows adapters to work both ways: via MessageBox (timer-based) or direct (blob trigger)
            if (records == null || records.Count == 0)
            {
                _logger?.LogInformation("No records from MessageBox and no records provided. Nothing to write.");
                return;
            }
            
            // Validate headers and records if not reading from MessageBox
            if (headers == null || headers.Count == 0)
                throw new ArgumentException("Headers cannot be empty", nameof(headers));

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

            // Insert rows using DataService with optional transaction support
            var startTime = DateTime.UtcNow;
            var rowsProcessed = records?.Count ?? 0;
            var rowsSucceeded = 0;
            var rowsFailed = 0;
            string? sourceFile = null;

            if (records != null && records.Count > 0)
            {
                try
                {
                    if (_useTransaction)
                    {
                        // Wrap operations in an explicit transaction
                        using var context = GetContext();
                        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                        try
                        {
                            _logger?.LogInformation("Starting transaction for writing {RecordCount} records to table {Destination}", records.Count, destination);
                            
                            await _dataService.InsertRowsAsync(records, columnTypes, cancellationToken);
                            
                            await transaction.CommitAsync(cancellationToken);
                            rowsSucceeded = records.Count;
                            _logger?.LogInformation("Transaction committed successfully. Inserted {RecordCount} records into table {Destination}", records.Count, destination);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            rowsFailed = records.Count;
                            _logger?.LogError(ex, "Transaction rolled back due to error while writing to table {Destination}", destination);
                            throw;
                        }
                    }
                    else
                    {
                        // No transaction - use default behavior
                        await _dataService.InsertRowsAsync(records, columnTypes, cancellationToken);
                        rowsSucceeded = records.Count;
                        _logger?.LogInformation("Successfully inserted {RecordCount} records into table {Destination}", records.Count, destination);
                    }
                }
                catch
                {
                    rowsFailed = records.Count;
                    rowsSucceeded = 0;
                    throw;
                }
                finally
                {
                    // Record processing statistics
                    try
                    {
                        var duration = DateTime.UtcNow - startTime;
                        if (_statisticsService != null && !string.IsNullOrWhiteSpace(_interfaceName))
                        {
                            await _statisticsService.RecordProcessingStatsAsync(
                                _interfaceName,
                                rowsProcessed,
                                rowsSucceeded,
                                rowsFailed,
                                duration,
                                sourceFile,
                                cancellationToken);
                        }
                    }
                    catch (Exception statsEx)
                    {
                        _logger?.LogWarning(statsEx, "Failed to record processing statistics");
                    }
                }
            }

            // Mark subscriptions as processed for all messages that were processed
            if (processedMessages != null && processedMessages.Count > 0)
            {
                await MarkMessagesAsProcessedAsync(processedMessages, $"Written to SQL Server table: {destination}", cancellationToken);
            }

            _logger?.LogInformation("Successfully wrote {RecordCount} records to SQL Server table: {Destination}", records?.Count ?? 0, destination);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing to SQL Server table: {Destination}", destination);
            throw;
        }
    }

    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default)
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

    public override async Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
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

