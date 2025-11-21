using System.Text;
using System.Linq;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// File Adapter for reading from and writing to files in Azure Blob Storage
/// Can be used standalone or by other adapters (e.g., CsvAdapter) to access blob storage
/// Supports both reading and writing operations
/// </summary>
public class FileAdapter : IAdapter
{
    public string AdapterName => "FILE";
    public string AdapterAlias => "File";
    public bool SupportsRead => true;
    public bool SupportsWrite => true;
    public string AdapterRole { get; }

    private readonly BlobServiceClient _blobServiceClient;
    private readonly string? _receiveFolder;
    private readonly string _fileMask;
    private readonly string? _destinationReceiveFolder;
    private readonly string _destinationFileMask;
    private readonly ILogger<FileAdapter>? _logger;
    private readonly IMessageBoxService? _messageBoxService;
    private readonly IMessageSubscriptionService? _subscriptionService;
    private readonly string? _interfaceName;
    private readonly Guid? _adapterInstanceGuid;
    private readonly int _batchSize;

    public FileAdapter(
        BlobServiceClient blobServiceClient,
        string adapterRole = "Source",
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        string? receiveFolder = null,
        string? fileMask = null,
        string? destinationReceiveFolder = null,
        string? destinationFileMask = null,
        int? batchSize = null,
        ILogger<FileAdapter>? logger = null)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        AdapterRole = adapterRole ?? "Source";
        _messageBoxService = messageBoxService;
        _subscriptionService = subscriptionService;
        _interfaceName = interfaceName;
        _adapterInstanceGuid = adapterInstanceGuid;
        _receiveFolder = receiveFolder;
        _fileMask = fileMask ?? "*.txt";
        _destinationReceiveFolder = destinationReceiveFolder;
        _destinationFileMask = destinationFileMask ?? "*.txt";
        _batchSize = batchSize ?? 1000;
        _logger = logger;
    }

    /// <summary>
    /// Reads file content from blob storage
    /// </summary>
    /// <param name="source">Path in format "container-name/blob-path"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    public async Task<string> ReadFileAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source path cannot be empty", nameof(source));

        var (containerName, blobPath) = ParseBlobPath(source);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
            throw new FileNotFoundException($"File not found: {source}");

        var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
        var content = downloadResult.Value.Content.ToString();

        _logger?.LogInformation("Successfully read file from {Source}, Size={Size} bytes", source, content.Length);
        return content;
    }

    /// <summary>
    /// Lists files in the specified folder matching the file mask
    /// </summary>
    /// <param name="source">Path in format "container-name/folder-path/" (folder must end with /)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of blob paths matching the file mask</returns>
    public async Task<List<string>> ListFilesAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source path cannot be empty", nameof(source));

        var (containerName, blobPath) = ParseBlobPath(source);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure folder path ends with /
        if (!blobPath.EndsWith("/"))
            blobPath += "/";

        _logger?.LogInformation("Listing files in folder: {BlobPath} in container {ContainerName}, file mask: {FileMask}",
            blobPath, containerName, _fileMask);

        var blobs = containerClient.GetBlobsAsync(prefix: blobPath, cancellationToken: cancellationToken);
        var matchingFiles = new List<string>();

        await foreach (var blobItem in blobs)
        {
            // Extract filename from blob path (e.g., "folder/subfolder/file.txt" -> "file.txt")
            var fileName = blobItem.Name.Contains('/')
                ? blobItem.Name.Substring(blobItem.Name.LastIndexOf('/') + 1)
                : blobItem.Name;

            // Filter files by file mask pattern (e.g., "*.txt", "*.csv", "data_*.txt")
            if (MatchesFileMask(fileName, _fileMask))
            {
                matchingFiles.Add(blobItem.Name);
            }
        }

        _logger?.LogInformation("Found {FileCount} files matching mask '{FileMask}' in folder {BlobPath}",
            matchingFiles.Count, _fileMask, blobPath);

        return matchingFiles;
    }

    /// <summary>
    /// Reads all files from the specified folder and returns their content
    /// </summary>
    /// <param name="source">Path in format "container-name/folder-path/" (folder must end with /)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping blob paths to their content</returns>
    public async Task<Dictionary<string, string>> ReadAllFilesAsync(string source, CancellationToken cancellationToken = default)
    {
        var filePaths = await ListFilesAsync(source, cancellationToken);
        var result = new Dictionary<string, string>();

        var (containerName, _) = ParseBlobPath(source);

        foreach (var filePath in filePaths)
        {
            try
            {
                var fullPath = $"{containerName}/{filePath}";
                var content = await ReadFileAsync(fullPath, cancellationToken);
                result[filePath] = content;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading file {FilePath} from folder {Source}", filePath, source);
                // Continue with next file
            }
        }

        return result;
    }

    /// <summary>
    /// Writes content to a file in blob storage
    /// </summary>
    /// <param name="destination">Path in format "container-name/blob-path"</param>
    /// <param name="content">File content to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteFileAsync(string destination, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination path cannot be empty", nameof(destination));

        var (containerName, blobPath) = ParseBlobPath(destination);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobPath);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        await blobClient.UploadAsync(new BinaryData(contentBytes), overwrite: true, cancellationToken);

        _logger?.LogInformation("Successfully wrote file to {Destination}, Size={Size} bytes", destination, content.Length);
    }

    /// <summary>
    /// Copies a file from source to destination
    /// </summary>
    /// <param name="source">Source path in format "container-name/blob-path"</param>
    /// <param name="destination">Destination path in format "container-name/blob-path"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        var content = await ReadFileAsync(source, cancellationToken);
        await WriteFileAsync(destination, content, cancellationToken);
        _logger?.LogInformation("Successfully copied file from {Source} to {Destination}", source, destination);
    }

    /// <summary>
    /// Moves a file from source to destination (copies then deletes source)
    /// </summary>
    /// <param name="source">Source path in format "container-name/blob-path"</param>
    /// <param name="destination">Destination path in format "container-name/blob-path"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task MoveFileAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        await CopyFileAsync(source, destination, cancellationToken);

        // Delete source file
        var (containerName, blobPath) = ParseBlobPath(source);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        _logger?.LogInformation("Successfully moved file from {Source} to {Destination}", source, destination);
    }

    /// <summary>
    /// Deletes old files in a folder, keeping only the most recent ones
    /// </summary>
    /// <param name="containerName">Container name</param>
    /// <param name="folderPath">Folder path within container (e.g., "csv-incoming")</param>
    /// <param name="maxFiles">Maximum number of files to keep</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task CleanupOldFilesAsync(string containerName, string folderPath, int maxFiles, CancellationToken cancellationToken = default)
    {
        if (maxFiles <= 0)
            return;

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        if (!await containerClient.ExistsAsync(cancellationToken))
            return;

        // Ensure folder path ends with /
        if (!folderPath.EndsWith("/"))
            folderPath += "/";

        var blobs = new List<(string Name, DateTimeOffset LastModified)>();

        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: folderPath, cancellationToken: cancellationToken))
        {
            // Skip placeholder files
            if (blobItem.Name.EndsWith(".folder-initialized", StringComparison.OrdinalIgnoreCase))
                continue;

            blobs.Add((blobItem.Name, blobItem.Properties.LastModified ?? DateTimeOffset.MinValue));
        }

        if (blobs.Count <= maxFiles)
        {
            _logger?.LogDebug("No cleanup needed: {FileCount} files in {FolderPath} (max: {MaxFiles})", blobs.Count, folderPath, maxFiles);
            return;
        }

        // Sort by LastModified descending (newest first) and take the files to keep
        var filesToKeep = blobs.OrderByDescending(b => b.LastModified).Take(maxFiles).Select(b => b.Name).ToHashSet();
        var filesToDelete = blobs.Where(b => !filesToKeep.Contains(b.Name)).ToList();

        _logger?.LogInformation("Cleaning up {DeleteCount} old files from {FolderPath} (keeping {KeepCount} most recent)", 
            filesToDelete.Count, folderPath, filesToKeep.Count);

        foreach (var (fileName, _) in filesToDelete)
        {
            try
            {
                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                _logger?.LogDebug("Deleted old file: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error deleting old file {FileName}: {ErrorMessage}", fileName, ex.Message);
            }
        }
    }

    /// <summary>
    /// Ensures that a folder exists in blob storage (creates placeholder file if needed)
    /// </summary>
    /// <param name="containerName">Container name</param>
    /// <param name="folderPath">Folder path within container (e.g., "csv-incoming")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task EnsureFolderExistsAsync(string containerName, string folderPath, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Ensure folder path ends with /
        if (!folderPath.EndsWith("/"))
            folderPath += "/";

        // Create a placeholder file to ensure the folder exists
        // In Azure Blob Storage, folders are virtual - they exist when blobs with that prefix exist
        var placeholderPath = $"{folderPath}.folder-initialized";
        var placeholderBlob = containerClient.GetBlobClient(placeholderPath);

        if (!await placeholderBlob.ExistsAsync(cancellationToken))
        {
            await placeholderBlob.UploadAsync(new BinaryData(""), overwrite: true, cancellationToken);
            _logger?.LogDebug("Created placeholder file for folder: {FolderPath}", folderPath);
        }
    }

    /// <summary>
    /// Builds a default blob source path from receive folder configuration
    /// </summary>
    /// <param name="receiveFolder">Receive folder configuration (optional)</param>
    /// <returns>Blob path in format "container-name/folder-path/"</returns>
    public string BuildDefaultBlobSourcePath(string? receiveFolder = null)
    {
        var folder = receiveFolder ?? _receiveFolder;
        folder = folder?.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = "csv-files/csv-incoming";
        }
        else
        {
            folder = folder.Replace("\\", "/").Trim('/');

            // If folder already includes a container name (e.g., csv-files/csv-incoming), keep it.
            // Otherwise, assume csv-files as the default container.
            if (!folder.Contains("/"))
            {
                folder = $"csv-files/{folder}";
            }
            else if (!folder.Contains(":") && !folder.StartsWith("csv-files/", StringComparison.OrdinalIgnoreCase))
            {
                folder = $"csv-files/{folder}";
            }
        }

        if (!folder.EndsWith("/"))
        {
            folder += "/";
        }

        return folder;
    }

    /// <summary>
    /// IAdapter.ReadAsync implementation - reads files from blob storage
    /// Returns headers and records for compatibility with other adapters
    /// Note: This method requires ICsvProcessingService which is not available in IAdapter
    /// Use ReadFileAsync, ReadAllFilesAsync, or ReadCsvFilesAsync methods instead
    /// </summary>
    public Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        // This method requires ICsvProcessingService which is not available in IAdapter interface
        // FileAdapter is primarily designed to be used by other adapters (like CsvAdapter)
        // For standalone use, use ReadFileAsync, ReadAllFilesAsync, or ReadCsvFilesAsync methods
        throw new NotSupportedException("FileAdapter.ReadAsync requires ICsvProcessingService. Use ReadFileAsync(ICsvProcessingService, fieldSeparator, source, cancellationToken) instead, or use FileAdapter through CsvAdapter.");
    }

    /// <summary>
    /// Reads CSV files from blob storage and returns headers and records
    /// This is a convenience method for CSV processing
    /// </summary>
    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadCsvFilesAsync(
        ICsvProcessingService csvProcessingService,
        string fieldSeparator,
        string source,
        CancellationToken cancellationToken = default)
    {
        var (containerName, blobPath) = ParseBlobPath(source);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Check if source is a folder (ends with '/' or doesn't contain a file extension)
        bool isFolder = blobPath.EndsWith("/") || (!blobPath.Contains(".") && !string.IsNullOrWhiteSpace(blobPath));

        var allHeaders = new List<string>();
        var allRecords = new List<Dictionary<string, string>>();

        if (isFolder || !string.IsNullOrWhiteSpace(_receiveFolder))
        {
            // Process all CSV files in the folder
            var filePaths = await ListFilesAsync(source, cancellationToken);

            foreach (var filePath in filePaths)
            {
                try
                {
                    var fullPath = $"{containerName}/{filePath}";
                    var content = await ReadFileAsync(fullPath, cancellationToken);

                    var (headers, records) = await csvProcessingService.ParseCsvWithHeadersAsync(content, fieldSeparator, cancellationToken);

                    _logger?.LogInformation("Successfully parsed CSV from file {FilePath}: {HeaderCount} headers, {RecordCount} records",
                        filePath, headers.Count, records.Count);

                    // Use headers from first file, or merge if different
                    if (allHeaders.Count == 0)
                    {
                        allHeaders = headers;
                    }
                    else if (!headers.SequenceEqual(allHeaders))
                    {
                        _logger?.LogWarning("Headers differ between files. Using headers from first file.");
                    }

                    allRecords.AddRange(records);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing CSV file {FilePath} from folder {Source}", filePath, source);
                    // Continue with next file
                }
            }
        }
        else
        {
            // Process single file
            var content = await ReadFileAsync(source, cancellationToken);
            var (headers, records) = await csvProcessingService.ParseCsvWithHeadersAsync(content, fieldSeparator, cancellationToken);

            _logger?.LogInformation("Successfully parsed CSV from {Source}: {HeaderCount} headers, {RecordCount} records",
                source, headers.Count, records.Count);

            allHeaders = headers;
            allRecords = records;
        }

        // Write to MessageBox if AdapterRole is "Source"
        if (AdapterRole.Equals("Source", StringComparison.OrdinalIgnoreCase) && 
            _messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName) && _adapterInstanceGuid.HasValue && allRecords.Count > 0)
        {
            _logger?.LogInformation("Writing records to MessageBox as Source adapter: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, Records={RecordCount}",
                _interfaceName, _adapterInstanceGuid.Value, allRecords.Count);
            
            // Process records in batches
            var batchSize = _batchSize;
            for (int i = 0; i < allRecords.Count; i += batchSize)
            {
                var batch = allRecords.Skip(i).Take(batchSize).ToList();
                var messageIds = await _messageBoxService.WriteMessagesAsync(
                    _interfaceName,
                    AdapterName,
                    "Source",
                    _adapterInstanceGuid.Value,
                    allHeaders,
                    batch,
                    cancellationToken);
                
                _logger?.LogInformation("Successfully wrote {MessageCount} messages to MessageBox from batch", messageIds.Count);
            }
        }
        else if (!AdapterRole.Equals("Source", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("AdapterRole is '{AdapterRole}', skipping MessageBox write in ReadCsvFilesAsync", AdapterRole);
        }

        return (allHeaders, allRecords);
    }

    /// <summary>
    /// IAdapter.WriteAsync implementation - writes data to blob storage as CSV
    /// When AdapterRole is "Destination", reads messages from MessageBox first
    /// </summary>
    public async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination path cannot be empty", nameof(destination));

        _logger?.LogInformation("Writing to file destination: {Destination}, {RecordCount} records, AdapterRole: {AdapterRole}", 
            destination, records?.Count ?? 0, AdapterRole);

        // If AdapterRole is "Destination" and MessageBoxService is available, read messages from MessageBox
        List<InterfaceConfigurator.Main.Core.Models.MessageBoxMessage>? processedMessages = null;
        if (AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase) && 
            _messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName))
        {
            _logger?.LogInformation("Reading messages from MessageBox as Destination adapter: Interface={InterfaceName}", _interfaceName);
            
            var messages = await _messageBoxService.ReadMessagesAsync(_interfaceName, "Pending", cancellationToken);
            processedMessages = new List<InterfaceConfigurator.Main.Core.Models.MessageBoxMessage>();
            
            if (messages.Count > 0)
            {
                var processedRecords = new List<Dictionary<string, string>>();
                var processedHeaders = new List<string>();
                
                foreach (var message in messages)
                {
                    var lockAcquired = await _messageBoxService.MarkMessageAsInProgressAsync(
                        message.MessageId, lockTimeoutMinutes: 5, cancellationToken);
                    
                    if (!lockAcquired)
                    {
                        _logger?.LogWarning("Could not acquire lock on message {MessageId}, skipping", message.MessageId);
                        continue;
                    }

                    try
                    {
                        if (_subscriptionService != null)
                        {
                            await _subscriptionService.CreateSubscriptionAsync(
                                message.MessageId, _interfaceName, AdapterName, cancellationToken);
                        }
                        
                        var (messageHeaders, singleRecord) = _messageBoxService.ExtractDataFromMessage(message);
                        
                        if (processedHeaders.Count == 0)
                        {
                            processedHeaders = messageHeaders;
                        }
                        
                        processedRecords.Add(singleRecord);
                        processedMessages.Add(message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing message {MessageId} from MessageBox", message.MessageId);
                        await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Error", cancellationToken);
                        await _messageBoxService.MarkMessageAsErrorAsync(message.MessageId, ex.Message, cancellationToken);
                        
                        if (_subscriptionService != null)
                        {
                            await _subscriptionService.MarkSubscriptionAsErrorAsync(
                                message.MessageId, AdapterName, ex.Message, cancellationToken);
                        }
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
                    _logger?.LogInformation("No messages were successfully processed from MessageBox");
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

        // Determine destination path
        string containerName;
        string blobPath;

        // If DestinationReceiveFolder is configured, use it and construct filename from DestinationFileMask
        if (!string.IsNullOrWhiteSpace(_destinationReceiveFolder))
        {
            var pathParts = _destinationReceiveFolder.Split('/', 2);
            if (pathParts.Length != 2)
                throw new ArgumentException($"Invalid destination receive folder format. Expected 'container/path', got: {_destinationReceiveFolder}", nameof(_destinationReceiveFolder));

            containerName = pathParts[0];
            var folderPath = pathParts[1].TrimEnd('/');

            // Construct filename from DestinationFileMask with variable substitution
            var fileName = ExpandFileNameVariables(_destinationFileMask);
            blobPath = $"{folderPath}/{fileName}";

            _logger?.LogInformation("Using DestinationReceiveFolder '{ReceiveFolder}' with filename '{FileName}' constructed from mask '{FileMask}'",
                _destinationReceiveFolder, fileName, _destinationFileMask);
        }
        else
        {
            // Parse destination path: "container-name/blob-path"
            var pathParts = destination.Split('/', 2);
            if (pathParts.Length != 2)
                throw new ArgumentException($"Invalid destination path format. Expected 'container/path', got: {destination}", nameof(destination));

            containerName = pathParts[0];
            blobPath = pathParts[1];
        }

        // Build CSV content (using default separator - caller should handle field separator)
        var csvContent = new StringBuilder();

        // Write headers
        if (headers != null && headers.Count > 0)
        {
            csvContent.AppendLine(string.Join(",", headers.Select(h => EscapeCsvValue(h, ","))));
        }

        // Write records
        if (records != null)
        {
            foreach (var record in records)
            {
                var values = headers.Select(header => record.GetValueOrDefault(header, string.Empty));
                csvContent.AppendLine(string.Join(",", values.Select(v => EscapeCsvValue(v, ","))));
            }
        }

        // Write to blob storage
        await WriteFileAsync($"{containerName}/{blobPath}", csvContent.ToString(), cancellationToken);

        _logger?.LogInformation("Successfully wrote CSV to {Destination}: {RecordCount} records", destination, records?.Count ?? 0);
    }

    /// <summary>
    /// IAdapter.GetSchemaAsync implementation - not supported for FileAdapter
    /// Schema must be determined from CSV content, not from file system
    /// </summary>
    public Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("FileAdapter does not support schema detection. Use ICsvProcessingService to analyze CSV content.");
    }

    /// <summary>
    /// IAdapter.EnsureDestinationStructureAsync implementation - not supported for FileAdapter
    /// File structure is determined by content, not by adapter
    /// </summary>
    public Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("FileAdapter does not support destination structure management. File structure is determined by content.");
    }

    private (string containerName, string blobPath) ParseBlobPath(string path)
    {
        var pathParts = path.Split('/', 2);
        if (pathParts.Length != 2)
            throw new ArgumentException($"Invalid blob path format. Expected 'container/path', got: {path}", nameof(path));

        return (pathParts[0], pathParts[1]);
    }

    private bool MatchesFileMask(string fileName, string fileMask)
    {
        if (string.IsNullOrWhiteSpace(fileMask) || fileMask == "*" || fileMask == "*.*")
            return true;

        if (!fileMask.Contains("*") && !fileMask.Contains("?"))
            return fileName.Equals(fileMask, StringComparison.OrdinalIgnoreCase);

        // Simple wildcard matching
        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(fileMask)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string ExpandFileNameVariables(string fileMask)
    {
        var now = DateTime.UtcNow;
        return fileMask
            .Replace("{date}", now.ToString("yyyy-MM-dd"))
            .Replace("{time}", now.ToString("HH-mm-ss"))
            .Replace("{datetime}", now.ToString("yyyy-MM-dd_HH-mm-ss"))
            .Replace("{year}", now.ToString("yyyy"))
            .Replace("{month}", now.ToString("MM"))
            .Replace("{day}", now.ToString("dd"))
            .Replace("{hour}", now.ToString("HH"))
            .Replace("{minute}", now.ToString("mm"))
            .Replace("{second}", now.ToString("ss"));
    }

    private string EscapeCsvValue(string value, string separator)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // If value contains separator, newline, or quote, wrap in quotes and escape quotes
        if (value.Contains(separator) || value.Contains("\n") || value.Contains("\r") || value.Contains("\""))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

