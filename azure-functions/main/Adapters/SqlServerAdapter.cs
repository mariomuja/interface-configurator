using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// SQL Server Adapter for reading from and writing to SQL Server tables
/// Uses ApplicationDbContext to access the main application database (app-database)
/// TransportData table is created/written in the main application database, NOT in MessageBox database
/// </summary>
public class SqlServerAdapter : IAdapter
{
    public string AdapterName => "SqlServer";
    public string AdapterAlias => "SQL Server";
    public bool SupportsRead => true;
    public bool SupportsWrite => true;

    /// <summary>
    /// ApplicationDbContext connects to the configured SQL Server database
    /// Can use either the default context (from DI) or a custom connection string
    /// </summary>
    private readonly ApplicationDbContext? _defaultContext;
    private readonly string? _connectionString;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IDataService _dataService;
    private readonly IMessageBoxService? _messageBoxService;
    private readonly IMessageSubscriptionService? _subscriptionService;
    private readonly string? _interfaceName;
    private readonly Guid? _adapterInstanceGuid;
    private readonly ILogger<SqlServerAdapter>? _logger;
    private readonly CsvColumnAnalyzer _columnAnalyzer;
    private readonly TypeValidator _typeValidator;
    
    // Source-specific properties
    private readonly string? _pollingStatement;
    private readonly int _pollingInterval;
    
    // General SQL Server adapter properties
    private readonly bool _useTransaction;
    private readonly int _batchSize;
    private readonly int _commandTimeout;
    private readonly bool _failOnBadStatement;
    private readonly IInterfaceConfigurationService? _configService;

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
        bool? useTransaction = null,
        int? batchSize = null,
        int? commandTimeout = null,
        bool? failOnBadStatement = null,
        IInterfaceConfigurationService? configService = null,
        ILogger<SqlServerAdapter>? logger = null)
    {
        if (context == null && string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Either context or connectionString must be provided", nameof(context));
        
        _defaultContext = context;
        _connectionString = connectionString;
        _dynamicTableService = dynamicTableService ?? throw new ArgumentNullException(nameof(dynamicTableService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _messageBoxService = messageBoxService;
        _subscriptionService = subscriptionService;
        _interfaceName = interfaceName ?? "FromCsvToSqlServerExample";
        _adapterInstanceGuid = adapterInstanceGuid;
        _pollingStatement = pollingStatement;
        _pollingInterval = pollingInterval ?? 60;
        _useTransaction = useTransaction ?? false;
        _batchSize = batchSize ?? 1000;
        _commandTimeout = commandTimeout ?? 30;
        _failOnBadStatement = failOnBadStatement ?? false;
        _configService = configService;
        _logger = logger;
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

        // Create DbContextOptions with custom connection string
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(_connectionString);
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        try
        {
            string selectSql;
            List<string> headers = new List<string>();

            // Use polling statement if provided (for source adapters)
            if (!string.IsNullOrWhiteSpace(_pollingStatement))
            {
                selectSql = _pollingStatement;
                _logger?.LogInformation("Reading data using polling statement: {PollingStatement}", _pollingStatement);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(source))
                    throw new ArgumentException("Source table name cannot be empty when polling statement is not provided", nameof(source));

                _logger?.LogInformation("Reading data from SQL Server table: {Source}", source);

                // Ensure database exists
                using var contextForInit = GetContext();
                await contextForInit.Database.EnsureCreatedAsync(cancellationToken);

                // Get current table structure
                var columnTypes = await _dynamicTableService.GetCurrentTableStructureAsync(cancellationToken);

                if (columnTypes.Count == 0)
                {
                    _logger?.LogWarning("Table {Source} has no columns or does not exist", source);
                    return (new List<string>(), new List<Dictionary<string, string>>());
                }

                headers = columnTypes.Keys.ToList();

                // Build SELECT query
                var sanitizedColumns = headers.Select(SanitizeColumnName).ToList();
                var columnList = string.Join(", ", sanitizedColumns);
                selectSql = $"SELECT {columnList} FROM [{source}] ORDER BY datetime_created";
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

            // If MessageBoxService is available, debatch and write to MessageBox as Source adapter
            // Each record is written as a separate message, triggering events
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName) && _adapterInstanceGuid.HasValue)
            {
                _logger?.LogInformation("Debatching SQL Server data and writing to MessageBox as Source adapter: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, Records={RecordCount}", 
                    _interfaceName, _adapterInstanceGuid.Value, records.Count);
                var messageIds = await _messageBoxService.WriteMessagesAsync(
                    _interfaceName,
                    AdapterName,
                    "Source",
                    _adapterInstanceGuid.Value,
                    headers,
                    records,
                    cancellationToken);
                _logger?.LogInformation("Successfully debatched and wrote {MessageCount} messages to MessageBox", messageIds.Count);
            }
            else if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName))
            {
                _logger?.LogWarning("AdapterInstanceGuid is missing. Messages will not be written to MessageBox.");
            }

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

        try
        {
            _logger?.LogInformation("Writing {RecordCount} records to SQL Server table: {Destination}", records?.Count ?? 0, destination);

            // If MessageBoxService is available, subscribe and process messages from event queue (as Destination adapter)
            List<InterfaceConfigurator.Main.Core.Models.MessageBoxMessage>? processedMessages = null;
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName))
            {
                _logger?.LogInformation("Subscribing to messages from MessageBox as Destination adapter: Interface={InterfaceName}", _interfaceName);
                
                // Read pending messages (event-driven: messages are queued when added)
                var messages = await _messageBoxService.ReadMessagesAsync(_interfaceName, "Pending", cancellationToken);
                processedMessages = new List<InterfaceConfigurator.Main.Core.Models.MessageBoxMessage>();
                
                if (messages.Count > 0)
                {
                    // Process messages one by one (each message contains a single record)
                    var processedRecords = new List<Dictionary<string, string>>();
                    var processedHeaders = new List<string>();
                    
                    foreach (var message in messages)
                    {
                        // Try to acquire lock on message (prevent concurrent processing)
                        var lockAcquired = await _messageBoxService.MarkMessageAsInProgressAsync(
                            message.MessageId, lockTimeoutMinutes: 5, cancellationToken);
                        
                        if (!lockAcquired)
                        {
                            _logger?.LogWarning("Could not acquire lock on message {MessageId}, skipping (may be processed by another instance)", message.MessageId);
                            continue; // Skip this message, another instance is processing it
                        }

                        try
                        {
                            // Create subscription for this message (if subscription service is available)
                            if (_subscriptionService != null)
                            {
                                await _subscriptionService.CreateSubscriptionAsync(
                                    message.MessageId, _interfaceName, AdapterName, cancellationToken);
                            }
                            
                            // Extract single record from message
                            (var messageHeaders, var singleRecord) = _messageBoxService.ExtractDataFromMessage(message);
                            
                            // Use headers from first message
                            if (processedHeaders.Count == 0)
                            {
                                processedHeaders = messageHeaders;
                            }
                            
                            processedRecords.Add(singleRecord);
                            processedMessages.Add(message); // Track processed messages for subscription marking
                            
                            _logger?.LogInformation("Processed message {MessageId} from MessageBox", message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error processing message {MessageId} from MessageBox: {ErrorMessage}", message.MessageId, ex.Message);
                            
                            // Release lock and mark as error (will handle retry logic)
                            try
                            {
                                await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Error", cancellationToken);
                                await _messageBoxService.MarkMessageAsErrorAsync(message.MessageId, ex.Message, cancellationToken);
                            }
                            catch (Exception lockEx)
                            {
                                _logger?.LogError(lockEx, "Error releasing lock or marking error for message {MessageId}", message.MessageId);
                            }
                            
                            // Mark subscription as error if subscription service is available
                            if (_subscriptionService != null)
                            {
                                try
                                {
                                    await _subscriptionService.MarkSubscriptionAsErrorAsync(
                                        message.MessageId, AdapterName, ex.Message, cancellationToken);
                                }
                                catch (Exception subEx)
                                {
                                    _logger?.LogError(subEx, "Error marking subscription as error for message {MessageId}", message.MessageId);
                                }
                            }
                            // Continue with next message
                        }
                    }
                    
                    if (processedRecords.Count > 0)
                    {
                        headers = processedHeaders;
                        records = processedRecords;
                        
                        _logger?.LogInformation("Read {RecordCount} records from {MessageCount} MessageBox messages", 
                            processedRecords.Count, messages.Count);
                    }
                    else
                    {
                        // No messages processed, return early
                        _logger?.LogInformation("No messages were successfully processed from MessageBox for interface {InterfaceName}", _interfaceName);
                        return;
                    }
                }
                else
                {
                    _logger?.LogWarning("No pending messages found in MessageBox for interface {InterfaceName}. Will use provided records if available.", _interfaceName);
                    // Continue to fallback: use provided records if available
                }
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
            if (records != null && records.Count > 0)
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
                        _logger?.LogInformation("Transaction committed successfully. Inserted {RecordCount} records into table {Destination}", records.Count, destination);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _logger?.LogError(ex, "Transaction rolled back due to error while writing to table {Destination}", destination);
                        throw;
                    }
                }
                else
                {
                    // No transaction - use default behavior
                    await _dataService.InsertRowsAsync(records, columnTypes, cancellationToken);
                    _logger?.LogInformation("Successfully inserted {RecordCount} records into table {Destination}", records.Count, destination);
                }
            }

            // Mark subscriptions as processed for all messages that were processed
            if (_messageBoxService != null && _subscriptionService != null && processedMessages != null && processedMessages.Count > 0)
            {
                foreach (var message in processedMessages)
                {
                    try
                    {
                        // Release lock before marking as processed
                        await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Processed", cancellationToken);
                        
                        await _subscriptionService.MarkSubscriptionAsProcessedAsync(
                            message.MessageId, AdapterName, $"Written to SQL Server table: {destination}", cancellationToken);
                        
                        // Mark message as processed (releases lock automatically)
                        await _messageBoxService.MarkMessageAsProcessedAsync(
                            message.MessageId, $"Written to SQL Server table: {destination}", cancellationToken);
                        
                        // Check if all subscriptions are processed, then remove message
                        var allProcessed = await _subscriptionService.AreAllSubscriptionsProcessedAsync(message.MessageId, cancellationToken);
                        if (allProcessed)
                        {
                            await _messageBoxService.RemoveMessageAsync(message.MessageId, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error marking subscription as processed for message {MessageId}", message.MessageId);
                        // Release lock on error
                        try
                        {
                            await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Error", cancellationToken);
                        }
                        catch { }
                    }
                }
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

