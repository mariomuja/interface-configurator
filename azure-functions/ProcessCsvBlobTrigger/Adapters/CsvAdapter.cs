using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Adapters;

/// <summary>
/// CSV Adapter for reading from and writing to CSV files in Azure Blob Storage
/// When used as Source: Reads CSV and writes to MessageBox
/// When used as Destination: Reads from MessageBox and writes CSV
/// </summary>
public class CsvAdapter : IAdapter
{
    public string AdapterName => "CSV";

    private readonly ICsvProcessingService _csvProcessingService;
    private readonly IAdapterConfigurationService _adapterConfig;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IMessageBoxService? _messageBoxService;
    private readonly IMessageSubscriptionService? _subscriptionService;
    private readonly string? _interfaceName;
    private readonly ILogger<CsvAdapter>? _logger;
    private string? _cachedSeparator;

    public CsvAdapter(
        ICsvProcessingService csvProcessingService,
        IAdapterConfigurationService adapterConfig,
        BlobServiceClient blobServiceClient,
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        ILogger<CsvAdapter>? logger = null)
    {
        _csvProcessingService = csvProcessingService ?? throw new ArgumentNullException(nameof(csvProcessingService));
        _adapterConfig = adapterConfig ?? throw new ArgumentNullException(nameof(adapterConfig));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _messageBoxService = messageBoxService;
        _subscriptionService = subscriptionService;
        _interfaceName = interfaceName ?? "FromCsvToSqlServerExample";
        _logger = logger;
    }

    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source path cannot be empty", nameof(source));

        try
        {
            _logger?.LogInformation("Reading CSV from source: {Source}", source);

            // Parse source path: "container-name/blob-path" or "container-name/folder/blob-name"
            var pathParts = source.Split('/', 2);
            if (pathParts.Length != 2)
                throw new ArgumentException($"Invalid source path format. Expected 'container/path', got: {source}", nameof(source));

            var containerName = pathParts[0];
            var blobPath = pathParts[1];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
                throw new FileNotFoundException($"CSV file not found: {source}");

            // Download blob content
            var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
            var csvContent = downloadResult.Value.Content.ToString();

            // Parse CSV using CsvProcessingService
            var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, cancellationToken);

            _logger?.LogInformation("Successfully read CSV from {Source}: {HeaderCount} headers, {RecordCount} records", 
                source, headers.Count, records.Count);

            // If MessageBoxService is available, debatch and write to MessageBox as Source adapter
            // Each record is written as a separate message, triggering events
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName))
            {
                _logger?.LogInformation("Debatching CSV data and writing to MessageBox as Source adapter: Interface={InterfaceName}, Records={RecordCount}", 
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
            _logger?.LogError(ex, "Error reading CSV from source: {Source}", source);
            throw;
        }
    }

    public async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination path cannot be empty", nameof(destination));

        try
        {
            _logger?.LogInformation("Writing CSV to destination: {Destination}, {RecordCount} records", destination, records?.Count ?? 0);

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

            // Parse destination path: "container-name/blob-path"
            var pathParts = destination.Split('/', 2);
            if (pathParts.Length != 2)
                throw new ArgumentException($"Invalid destination path format. Expected 'container/path', got: {destination}", nameof(destination));

            var containerName = pathParts[0];
            var blobPath = pathParts[1];

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobPath);

            // Get field separator from configuration
            if (_cachedSeparator == null)
            {
                _cachedSeparator = await _adapterConfig.GetCsvFieldSeparatorAsync(cancellationToken);
            }
            var separator = _cachedSeparator;

            // Build CSV content
            var csvContent = new StringBuilder();
            
            // Write headers
            csvContent.AppendLine(string.Join(separator, headers.Select(h => EscapeCsvValue(h, separator))));

            // Write records
            if (records != null)
            {
                foreach (var record in records)
                {
                    var values = headers.Select(header => record.GetValueOrDefault(header, string.Empty));
                    csvContent.AppendLine(string.Join(separator, values.Select(v => EscapeCsvValue(v, separator))));
                }
            }

            // Upload to blob storage
            var content = Encoding.UTF8.GetBytes(csvContent.ToString());
            await blobClient.UploadAsync(new BinaryData(content), overwrite: true, cancellationToken);

            _logger?.LogInformation("Successfully wrote CSV to {Destination}: {RecordCount} records", destination, records?.Count ?? 0);

            // Mark subscriptions as processed for all messages that were processed
            if (_messageBoxService != null && _subscriptionService != null && processedMessages != null && processedMessages.Count > 0)
            {
                foreach (var message in processedMessages)
                {
                    try
                    {
                        await _subscriptionService.MarkSubscriptionAsProcessedAsync(
                            message.MessageId, AdapterName, $"Written to CSV destination: {destination}", cancellationToken);
                        
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
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing CSV to destination: {Destination}", destination);
            throw;
        }
    }

    public async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default)
    {
        var (headers, records) = await ReadAsync(source, cancellationToken);

        if (headers.Count == 0 || records.Count == 0)
            return new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        // Analyze column types
        var columnAnalyzer = new CsvColumnAnalyzer();
        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        foreach (var header in headers)
        {
            var values = records
                .Select(r => r.GetValueOrDefault(header, string.Empty))
                .ToList();

            var typeInfo = columnAnalyzer.AnalyzeColumn(header, values);
            columnTypes[header] = typeInfo;
        }

        return columnTypes;
    }

    public Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        // CSV files don't require structure validation - they are schema-less
        // The structure is determined by the headers in the file
        _logger?.LogDebug("CSV adapter: No structure validation needed for destination: {Destination}", destination);
        return Task.CompletedTask;
    }

    private string EscapeCsvValue(string value, string separator)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // If value contains separator, quotes, or newlines, wrap in quotes and escape quotes
        if (value.Contains(separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}

