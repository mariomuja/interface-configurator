using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using System.Text.Json;
using Azure.Storage.Blobs;
using ServiceBusMessage = InterfaceConfigurator.Main.Core.Interfaces.ServiceBusMessage;

namespace InterfaceConfigurator.Adapters;

/// <summary>
/// Base class for all adapters providing common Service Bus functionality
/// Handles reading from and writing to Service Bus for Source and Destination roles
/// </summary>
public abstract class AdapterBase : IAdapter
{
    // IAdapter interface properties - must be implemented by derived classes
    public abstract string AdapterName { get; }
    public abstract string AdapterAlias { get; }
    public abstract bool SupportsRead { get; }
    public abstract bool SupportsWrite { get; }
    public string AdapterRole { get; protected set; }

    // Common Service Bus-related fields
    protected readonly IServiceBusService? _serviceBusService;
    protected readonly string? _interfaceName;
    protected readonly Guid? _adapterInstanceGuid;
    protected readonly int _batchSize;
    protected readonly ILogger? _logger;
    
    // JQ Transformation support
    protected readonly JQTransformationService? _jqService;
    protected readonly string? _jqScriptFile;
    
    // Processing Statistics support
    protected readonly ProcessingStatisticsService? _statisticsService;

    protected AdapterBase(
        IServiceBusService? serviceBusService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        int batchSize = 1000,
        string adapterRole = "Source",
        ILogger? logger = null,
        JQTransformationService? jqService = null,
        string? jqScriptFile = null,
        ProcessingStatisticsService? statisticsService = null)
    {
        _serviceBusService = serviceBusService;
        _interfaceName = interfaceName;
        _adapterInstanceGuid = adapterInstanceGuid;
        _batchSize = batchSize;
        AdapterRole = adapterRole ?? "Source";
        _logger = logger;
        _jqService = jqService;
        _jqScriptFile = jqScriptFile;
        _statisticsService = statisticsService;
    }

    // IAdapter interface methods - must be implemented by derived classes
    public abstract Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default);
    public abstract Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default);
    public abstract Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default);
    public abstract Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes records to Service Bus in batches (for Source adapters)
    /// All adapters MUST use Service Bus - no fallback to MessageBox
    /// </summary>
    protected async Task WriteRecordsToServiceBusAsync(
        List<string> headers,
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Source", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("AdapterRole is '{AdapterRole}', skipping Service Bus write", AdapterRole);
            return;
        }

        if (_serviceBusService == null)
        {
            _logger?.LogError("ServiceBusService is required but not available. Cannot write messages. InterfaceName={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}",
                _interfaceName ?? "NULL", _adapterInstanceGuid.HasValue);
            throw new InvalidOperationException("ServiceBusService is required for Source adapters");
        }

        if (string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        {
            _logger?.LogWarning("Skipping Service Bus write: InterfaceName={InterfaceName}, AdapterInstanceGuid={HasAdapterInstanceGuid}",
                _interfaceName ?? "NULL", _adapterInstanceGuid.HasValue);
            return;
        }

        if (records == null || records.Count == 0)
        {
            _logger?.LogDebug("No records to write to Service Bus");
            return;
        }

        _logger?.LogInformation("Writing records to Service Bus as Source adapter: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, TotalRecords={RecordCount}",
            _interfaceName, _adapterInstanceGuid.Value, records.Count);

        // Send all records to Service Bus (batched internally)
        var messageIds = await _serviceBusService.SendMessagesAsync(
            _interfaceName,
            AdapterName,
            "Source",
            _adapterInstanceGuid.Value,
            headers,
            records,
            cancellationToken);

        _logger?.LogInformation("Successfully sent {MessageCount} messages to Service Bus: Interface={InterfaceName}",
            messageIds.Count, _interfaceName);
    }


    /// <summary>
    /// Reads messages from Service Bus and extracts records (for Destination adapters)
    /// Returns the processed records, headers, and list of processed Service Bus messages
    /// All adapters MUST use Service Bus - no fallback to MessageBox
    /// </summary>
    protected async Task<(List<string> headers, List<Dictionary<string, string>> records, List<ServiceBusMessage> processedMessages)?> ReadMessagesFromServiceBusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("AdapterRole is '{AdapterRole}', skipping Service Bus read", AdapterRole);
            return null;
        }

        if (_serviceBusService == null)
        {
            _logger?.LogError("ServiceBusService is required but not available. Cannot read messages. InterfaceName={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}",
                _interfaceName ?? "NULL", _adapterInstanceGuid.HasValue);
            throw new InvalidOperationException("ServiceBusService is required for Destination adapters");
        }

        if (string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        {
            _logger?.LogDebug("ServiceBusService or InterfaceName/AdapterInstanceGuid not available, skipping Service Bus read");
            return null;
        }

        _logger?.LogInformation("Reading messages from Service Bus as Destination adapter: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}", 
            _interfaceName, _adapterInstanceGuid.Value);

        var messages = await _serviceBusService.ReceiveMessagesAsync(
            _interfaceName, 
            _adapterInstanceGuid.Value, 
            _batchSize, 
            cancellationToken);

        if (messages == null || messages.Count == 0)
        {
            _logger?.LogDebug("No messages found in Service Bus for interface {InterfaceName}, subscription {SubscriptionName}", 
                _interfaceName, $"destination-{_adapterInstanceGuid.Value.ToString().ToLowerInvariant()}");
            return null;
        }

        var processedMessages = new List<ServiceBusMessage>();

        if (messages.Count == 0)
        {
            _logger?.LogWarning("No pending messages found in MessageBox for interface {InterfaceName}", _interfaceName);
            return null;
        }

        var processedRecords = new List<Dictionary<string, string>>();
        var processedHeaders = new List<string>();

        foreach (var message in messages)
        {
            try
            {
                // Extract headers and record from Service Bus message
                var messageHeaders = message.Headers ?? new List<string>();
                var singleRecord = message.Record ?? new Dictionary<string, string>();

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
                            enqueuedTime = message.EnqueuedTime
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
                                        prop.Name != "adapterType" && prop.Name != "enqueuedTime")
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
                processedMessages.Add(message); // Track processed messages for completion

                _logger?.LogInformation("Processed message {MessageId} from Service Bus", message.MessageId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing message {MessageId} from Service Bus: {ErrorMessage}", message.MessageId, ex.Message);

                // Dead letter the message on error
                try
                {
                    await _serviceBusService.DeadLetterMessageAsync(
                        message.MessageId, 
                        message.LockToken, 
                        $"Processing error: {ex.Message}", 
                        cancellationToken);
                }
                catch (Exception dlEx)
                {
                    _logger?.LogError(dlEx, "Error dead lettering message {MessageId}", message.MessageId);
                }

                // Continue with next message
            }
        }

        if (processedRecords.Count == 0)
        {
            _logger?.LogInformation("No messages were successfully processed from Service Bus for interface {InterfaceName}", _interfaceName);
            return null;
        }

        _logger?.LogInformation("Read {RecordCount} records from {MessageCount} Service Bus messages",
            processedRecords.Count, messages.Count);

        return (processedHeaders, processedRecords, processedMessages);
    }


    /// <summary>
    /// Marks messages as processed after successful write operation (for Destination adapters)
    /// Uses Service Bus to complete messages
    /// </summary>
    protected async Task MarkMessagesAsProcessedAsync(
        List<ServiceBusMessage> processedMessages,
        string successMessage,
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_serviceBusService == null || processedMessages == null || processedMessages.Count == 0)
        {
            return;
        }

        foreach (var message in processedMessages)
        {
            try
            {
                // Complete the message in Service Bus (acknowledges successful processing)
                await _serviceBusService.CompleteMessageAsync(
                    message.MessageId, 
                    message.LockToken, 
                    cancellationToken);

                _logger?.LogInformation("Successfully completed message {MessageId} in Service Bus: {SuccessMessage}", 
                    message.MessageId, successMessage);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error completing message {MessageId} in Service Bus: {ErrorMessage}", 
                    message.MessageId, ex.Message);
                
                // Try to abandon the message so it can be retried
                try
                {
                    await _serviceBusService.AbandonMessageAsync(
                        message.MessageId, 
                        message.LockToken, 
                        null, 
                        cancellationToken);
                    _logger?.LogInformation("Abandoned message {MessageId} for retry", message.MessageId);
                }
                catch (Exception abandonEx)
                {
                    _logger?.LogError(abandonEx, "Error abandoning message {MessageId}: {ErrorMessage}", 
                        message.MessageId, abandonEx.Message);
                }
            }
        }
    }

    /// <summary>
    /// Logs detailed processing state with full exception information
    /// </summary>
    protected void LogProcessingState(
        string step,
        string state,
        string? details = null,
        Exception? exception = null)
    {
        if (_logger == null) return;

        var logMessage = $"[{step}] State: {state}";
        if (!string.IsNullOrWhiteSpace(details))
        {
            logMessage += $", Details: {details}";
        }

        if (exception != null)
        {
            logMessage += $", Exception: {exception.GetType().Name}: {exception.Message}";
            
            if (exception.InnerException != null)
            {
                logMessage += $", InnerException: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
            }

            logMessage += $", StackTrace: {exception.StackTrace}";
            
            _logger.LogError(exception, logMessage);
        }
        else
        {
            _logger.LogInformation(logMessage);
        }
    }

    /// <summary>
    /// Moves a blob file from one folder to another within the same container
    /// Used for moving files between incoming, processed, and error folders
    /// </summary>
    protected async Task MoveBlobFileAsync(
        BlobServiceClient blobServiceClient,
        string containerName,
        string sourcePath,
        string destinationFolder,
        CancellationToken cancellationToken = default)
    {
        if (blobServiceClient == null)
        {
            LogProcessingState("MoveBlobFile", "Error", "BlobServiceClient is null");
            throw new ArgumentNullException(nameof(blobServiceClient));
        }

        try
        {
            LogProcessingState("MoveBlobFile", "Starting", $"Container: {containerName}, Source: {sourcePath}, Destination: {destinationFolder}");

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure destination folder exists (create placeholder if needed)
            var destinationPlaceholder = $"{destinationFolder.TrimEnd('/')}/.folder-initialized";
            var placeholderBlob = containerClient.GetBlobClient(destinationPlaceholder);
            if (!await placeholderBlob.ExistsAsync(cancellationToken))
            {
                await placeholderBlob.UploadAsync(
                    new BinaryData($"Folder initialized at {DateTime.UtcNow:O}"),
                    cancellationToken);
                LogProcessingState("MoveBlobFile", "FolderCreated", $"Destination folder: {destinationFolder}");
            }

            var sourceBlob = containerClient.GetBlobClient(sourcePath);
            if (!await sourceBlob.ExistsAsync(cancellationToken))
            {
                LogProcessingState("MoveBlobFile", "Error", $"Source file does not exist: {sourcePath}");
                throw new FileNotFoundException($"Source file does not exist: {sourcePath}");
            }

            // Extract filename from source path
            var fileName = sourcePath.Contains('/') ? sourcePath.Substring(sourcePath.LastIndexOf('/') + 1) : sourcePath;
            var destinationPath = $"{destinationFolder.TrimEnd('/')}/{fileName}";

            // Copy blob to destination
            var destinationBlob = containerClient.GetBlobClient(destinationPath);
            var copyOperation = await destinationBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);
            
            // Wait for copy to complete
            var properties = await destinationBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            while (properties.Value.CopyStatus == Azure.Storage.Blobs.Models.CopyStatus.Pending)
            {
                await Task.Delay(100, cancellationToken);
                properties = await destinationBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            }

            if (properties.Value.CopyStatus != Azure.Storage.Blobs.Models.CopyStatus.Success)
            {
                LogProcessingState("MoveBlobFile", "Error", $"Copy failed with status: {properties.Value.CopyStatus}");
                throw new InvalidOperationException($"Failed to copy blob: {properties.Value.CopyStatus}");
            }

            // Delete source blob after successful copy
            await sourceBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            LogProcessingState("MoveBlobFile", "Completed", $"File moved from {sourcePath} to {destinationPath}");
        }
        catch (Exception ex)
        {
            LogProcessingState("MoveBlobFile", "Error", $"Failed to move file from {sourcePath} to {destinationFolder}", ex);
            throw;
        }
    }

    /// <summary>
    /// Writes records to Service Bus with debatching (one message per record)
    /// This is the preferred method for Source adapters that process files
    /// </summary>
    protected async Task<int> WriteRecordsToServiceBusWithDebatchingAsync(
        List<string> headers,
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken = default)
    {
        if (!AdapterRole.Equals("Source", StringComparison.OrdinalIgnoreCase))
        {
            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Skipped", $"AdapterRole is {AdapterRole}, not Source");
            return 0;
        }

        if (_serviceBusService == null)
        {
            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Error", "ServiceBusService is not available");
            throw new InvalidOperationException("ServiceBusService is required for Source adapters");
        }

        if (string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        {
            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Skipped", 
                $"InterfaceName={_interfaceName ?? "NULL"}, AdapterInstanceGuid={_adapterInstanceGuid.HasValue}");
            return 0;
        }

        if (records == null || records.Count == 0)
        {
            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Skipped", "No records to send");
            return 0;
        }

        try
        {
            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Starting", 
                $"Interface={_interfaceName}, AdapterInstanceGuid={_adapterInstanceGuid.Value}, TotalRecords={records.Count}");

            // Send all records to Service Bus (batched internally, but each record becomes a separate message)
            var messageIds = await _serviceBusService.SendMessagesAsync(
                _interfaceName!,
                AdapterName,
                "Source",
                _adapterInstanceGuid.Value,
                headers,
                records,
                cancellationToken);

            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Completed", 
                $"Successfully sent {messageIds.Count} messages to Service Bus");

            return messageIds.Count;
        }
        catch (Exception ex)
        {
            LogProcessingState("WriteRecordsToServiceBusWithDebatching", "Error", 
                $"Failed to send {records.Count} records to Service Bus", ex);
            throw;
        }
    }

}


