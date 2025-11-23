using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Services;
using System.Text.Json;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// Base class for all adapters providing common MessageBox functionality
/// Handles reading from and writing to MessageBox for Source and Destination roles
/// </summary>
public abstract class AdapterBase : IAdapter
{
    // IAdapter interface properties - must be implemented by derived classes
    public abstract string AdapterName { get; }
    public abstract string AdapterAlias { get; }
    public abstract bool SupportsRead { get; }
    public abstract bool SupportsWrite { get; }
    public string AdapterRole { get; protected set; }

    // Common MessageBox-related fields
    protected readonly IMessageBoxService? _messageBoxService;
    protected readonly IMessageSubscriptionService? _subscriptionService;
    protected readonly string? _interfaceName;
    protected readonly Guid? _adapterInstanceGuid;
    protected readonly int _batchSize;
    protected readonly ILogger? _logger;
    
    // JQ Transformation support
    protected readonly JQTransformationService? _jqService;
    protected readonly string? _jqScriptFile;

    protected AdapterBase(
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        int batchSize = 1000,
        string adapterRole = "Source",
        ILogger? logger = null,
        JQTransformationService? jqService = null,
        string? jqScriptFile = null)
    {
        _messageBoxService = messageBoxService;
        _subscriptionService = subscriptionService;
        _interfaceName = interfaceName;
        _adapterInstanceGuid = adapterInstanceGuid;
        _batchSize = batchSize;
        AdapterRole = adapterRole ?? "Source";
        _logger = logger;
        _jqService = jqService;
        _jqScriptFile = jqScriptFile;
    }

    // IAdapter interface methods - must be implemented by derived classes
    public abstract Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default);
    public abstract Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default);
    public abstract Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default);
    public abstract Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes records to MessageBox in batches (for Source adapters)
    /// </summary>
    protected async Task WriteRecordsToMessageBoxAsync(
        List<string> headers,
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Source", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("AdapterRole is '{AdapterRole}', skipping MessageBox write", AdapterRole);
            return;
        }

        if (_messageBoxService == null || string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        {
            _logger?.LogWarning("Skipping MessageBox write: MessageBoxService={HasMessageBoxService}, InterfaceName={InterfaceName}, AdapterInstanceGuid={HasAdapterInstanceGuid}",
                _messageBoxService != null, _interfaceName ?? "NULL", _adapterInstanceGuid.HasValue);
            return;
        }

        if (records == null || records.Count == 0)
        {
            _logger?.LogDebug("No records to write to MessageBox");
            return;
        }

        _logger?.LogInformation("Writing records to MessageBox as Source adapter: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, TotalRecords={RecordCount}",
            _interfaceName, _adapterInstanceGuid.Value, records.Count);

        // Process records in batches
        for (int i = 0; i < records.Count; i += _batchSize)
        {
            var batch = records.Skip(i).Take(_batchSize).ToList();
            var batchNumber = (i / _batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)records.Count / _batchSize);

            _logger?.LogInformation("Processing batch {BatchNumber}/{TotalBatches}: {BatchRecordCount} records",
                batchNumber, totalBatches, batch.Count);

            // Debatch batch into single rows and write to MessageBox
            var messageIds = await _messageBoxService.WriteMessagesAsync(
                _interfaceName,
                AdapterName,
                "Source",
                _adapterInstanceGuid.Value,
                headers,
                batch,
                cancellationToken);

            _logger?.LogInformation("Successfully debatched and wrote {MessageCount} messages to MessageBox from batch {BatchNumber}/{TotalBatches}",
                messageIds.Count, batchNumber, totalBatches);
        }

        _logger?.LogInformation("Completed writing all batches: {TotalRecords} records debatched into {TotalMessages} messages",
            records.Count, records.Count);
    }

    /// <summary>
    /// Reads messages from MessageBox and extracts records (for Destination adapters)
    /// Returns the processed records, headers, and list of processed messages
    /// </summary>
    protected async Task<(List<string> headers, List<Dictionary<string, string>> records, List<MessageBoxMessage> processedMessages)?> ReadMessagesFromMessageBoxAsync(
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("AdapterRole is '{AdapterRole}', skipping MessageBox read", AdapterRole);
            return null;
        }

        if (_messageBoxService == null || string.IsNullOrWhiteSpace(_interfaceName))
        {
            _logger?.LogDebug("MessageBoxService or InterfaceName not available, skipping MessageBox read");
            return null;
        }

        _logger?.LogInformation("Reading messages from MessageBox as Destination adapter: Interface={InterfaceName}", _interfaceName);

        var messages = await _messageBoxService.ReadMessagesAsync(_interfaceName, "Pending", cancellationToken);
        var processedMessages = new List<MessageBoxMessage>();

        if (messages.Count == 0)
        {
            _logger?.LogWarning("No pending messages found in MessageBox for interface {InterfaceName}", _interfaceName);
            return null;
        }

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

                // Apply jq transformation if configured
                Dictionary<string, string> transformedRecord = singleRecord;
                List<string> transformedHeaders = messageHeaders;
                
                if (!string.IsNullOrWhiteSpace(_jqScriptFile) && _jqService != null)
                {
                    try
                    {
                        // Convert message to JSON for jq transformation
                        var messageJson = JsonSerializer.Serialize(new
                        {
                            headers = messageHeaders,
                            record = singleRecord,
                            messageId = message.MessageId,
                            interfaceName = message.InterfaceName,
                            adapterName = message.AdapterName,
                            adapterType = message.AdapterType,
                            datetimeCreated = message.datetime_created
                        });

                        // Transform JSON using jq
                        var transformedJson = await _jqService.TransformJsonAsync(
                            messageJson,
                            _jqScriptFile,
                            cancellationToken);

                        // Parse transformed JSON back to record
                        var transformedData = JsonSerializer.Deserialize<JsonElement>(transformedJson);
                        
                        if (transformedData.ValueKind == JsonValueKind.Object)
                        {
                            // Extract headers and record from transformed JSON
                            if (transformedData.TryGetProperty("headers", out var headersElement) && 
                                headersElement.ValueKind == JsonValueKind.Array)
                            {
                                transformedHeaders = headersElement.EnumerateArray()
                                    .Select(h => h.GetString() ?? string.Empty)
                                    .ToList();
                            }

                            if (transformedData.TryGetProperty("record", out var recordElement) && 
                                recordElement.ValueKind == JsonValueKind.Object)
                            {
                                transformedRecord = new Dictionary<string, string>();
                                foreach (var prop in recordElement.EnumerateObject())
                                {
                                    transformedRecord[prop.Name] = prop.Value.GetString() ?? string.Empty;
                                }
                            }
                            else if (transformedData.ValueKind == JsonValueKind.Object)
                            {
                                // If no "record" property, use the entire object as record
                                transformedRecord = new Dictionary<string, string>();
                                foreach (var prop in transformedData.EnumerateObject())
                                {
                                    if (prop.Name != "headers" && prop.Name != "messageId" && 
                                        prop.Name != "interfaceName" && prop.Name != "adapterName" && 
                                        prop.Name != "adapterType" && prop.Name != "datetimeCreated")
                                    {
                                        transformedRecord[prop.Name] = prop.Value.GetString() ?? string.Empty;
                                    }
                                }
                            }
                        }

                        _logger?.LogInformation("Applied jq transformation to message {MessageId} using script {JQScriptFile}", 
                            message.MessageId, _jqScriptFile);
                    }
                    catch (Exception jqEx)
                    {
                        _logger?.LogError(jqEx, "Error applying jq transformation to message {MessageId}: {ErrorMessage}", 
                            message.MessageId, jqEx.Message);
                        throw; // Re-throw to mark message as error
                    }
                }

                // Use headers from first message
                if (processedHeaders.Count == 0)
                {
                    processedHeaders = transformedHeaders;
                }

                processedRecords.Add(transformedRecord);
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

        if (processedRecords.Count == 0)
        {
            _logger?.LogInformation("No messages were successfully processed from MessageBox for interface {InterfaceName}", _interfaceName);
            return null;
        }

        _logger?.LogInformation("Read {RecordCount} records from {MessageCount} MessageBox messages",
            processedRecords.Count, messages.Count);

        return (processedHeaders, processedRecords, processedMessages);
    }

    /// <summary>
    /// Marks messages as processed after successful write operation (for Destination adapters)
    /// </summary>
    protected async Task MarkMessagesAsProcessedAsync(
        List<MessageBoxMessage> processedMessages,
        string successMessage,
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_messageBoxService == null || _subscriptionService == null || processedMessages == null || processedMessages.Count == 0)
        {
            return;
        }

        foreach (var message in processedMessages)
        {
            try
            {
                // Release lock before marking as processed
                await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Processed", cancellationToken);

                await _subscriptionService.MarkSubscriptionAsProcessedAsync(
                    message.MessageId, AdapterName, successMessage, cancellationToken);

                // Mark message as processed (releases lock automatically)
                await _messageBoxService.MarkMessageAsProcessedAsync(
                    message.MessageId, successMessage, cancellationToken);

                // Check if all subscriptions are processed, then remove message
                var allProcessed = await _subscriptionService.AreAllSubscriptionsProcessedAsync(message.MessageId, cancellationToken);
                if (allProcessed)
                {
                    _logger?.LogInformation("All subscriptions processed for message {MessageId}. Retaining record for auditing.", message.MessageId);
                }
                else
                {
                    _logger?.LogDebug("Message {MessageId} still has pending subscribers.", message.MessageId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error marking message {MessageId} as processed", message.MessageId);
                // Release lock on error
                try
                {
                    await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Error", cancellationToken);
                }
                catch (Exception releaseEx)
                {
                    _logger?.LogWarning(releaseEx, "Error releasing message lock for message {MessageId}: {ErrorMessage}", message.MessageId, releaseEx.Message);
                }
            }
        }
    }
}


