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
    private readonly IMessageBoxService? _messageBoxService;
    private readonly IMessageSubscriptionService? _subscriptionService;
    private readonly string? _interfaceName;
    private readonly ILogger<SqlServerAdapter>? _logger;
    private readonly CsvColumnAnalyzer _columnAnalyzer;
    private readonly TypeValidator _typeValidator;

    public SqlServerAdapter(
        ApplicationDbContext context,
        IDynamicTableService dynamicTableService,
        IDataService dataService,
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        ILogger<SqlServerAdapter>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dynamicTableService = dynamicTableService ?? throw new ArgumentNullException(nameof(dynamicTableService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _messageBoxService = messageBoxService;
        _subscriptionService = subscriptionService;
        _interfaceName = interfaceName ?? "FromCsvToSqlServerExample";
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

            // If MessageBoxService is available, debatch and write to MessageBox as Source adapter
            // Each record is written as a separate message, triggering events
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName))
            {
                _logger?.LogInformation("Debatching SQL Server data and writing to MessageBox as Source adapter: Interface={InterfaceName}, Records={RecordCount}", 
                    _interfaceName, records.Count);
                var messageIds = await _messageBoxService.WriteMessagesAsync(
                    _interfaceName,
                    AdapterName,
                    "Source",
                    headers,
                    records,
                    cancellationToken);
                _logger?.LogInformation("Successfully debatched and wrote {MessageCount} messages to MessageBox", messageIds.Count);
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
            List<ProcessCsvBlobTrigger.Core.Models.MessageBoxMessage>? processedMessages = null;
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName))
            {
                _logger?.LogInformation("Subscribing to messages from MessageBox as Destination adapter: Interface={InterfaceName}", _interfaceName);
                
                // Read pending messages (event-driven: messages are queued when added)
                var messages = await _messageBoxService.ReadMessagesAsync(_interfaceName, "Pending", cancellationToken);
                processedMessages = new List<ProcessCsvBlobTrigger.Core.Models.MessageBoxMessage>();
                
                if (messages.Count > 0)
                {
                    // Process messages one by one (each message contains a single record)
                    var processedRecords = new List<Dictionary<string, string>>();
                    var processedHeaders = new List<string>();
                    
                    foreach (var message in messages)
                    {
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
                            _logger?.LogError(ex, "Error processing message {MessageId} from MessageBox", message.MessageId);
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
                    _logger?.LogWarning("No pending messages found in MessageBox for interface {InterfaceName}", _interfaceName);
                    return;
                }
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

            // Insert rows using DataService
            if (records != null && records.Count > 0)
            {
                await _dataService.InsertRowsAsync(records, columnTypes, cancellationToken);
            }

            // Mark subscriptions as processed for all messages that were processed
            if (_messageBoxService != null && _subscriptionService != null && processedMessages != null && processedMessages.Count > 0)
            {
                foreach (var message in processedMessages)
                {
                    try
                    {
                        await _subscriptionService.MarkSubscriptionAsProcessedAsync(
                            message.MessageId, AdapterName, $"Written to SQL Server table: {destination}", cancellationToken);
                        
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

