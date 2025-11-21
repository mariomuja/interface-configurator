using System.Text;
using System.Linq;
using System.IO;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// CSV Adapter for reading from and writing to CSV files in Azure Blob Storage
/// When used as Source: Reads CSV and writes to MessageBox
/// When used as Destination: Reads from MessageBox and writes CSV
/// </summary>
public class CsvAdapter : AdapterBase
{
    public override string AdapterName => "CSV";
    public override string AdapterAlias => "CSV";
    public override bool SupportsRead => true;
    public override bool SupportsWrite => true;

    private readonly ICsvProcessingService _csvProcessingService;
    private readonly IAdapterConfigurationService _adapterConfig;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string? _receiveFolder;
    private readonly string _fileMask;
    private readonly int _batchSize;
    private readonly string _fieldSeparator;
    private readonly string? _destinationReceiveFolder;
    private readonly string _destinationFileMask;
    private readonly ILogger<CsvAdapter>? _logger;
    
    // CSV Data property - can be set directly to trigger debatching
    private string? _csvData;
    
    // Static flag to track if CSV folders have been initialized
    private static bool _csvFoldersInitialized = false;
    private static readonly object _csvFoldersLock = new object();
    
    // Adapter type (FILE, RAW, SFTP)
    private readonly string _adapterType;
    
    // SFTP adapter instance (used when adapterType is SFTP)
    private readonly SftpAdapter? _sftpAdapter;
    
    // File adapter instance (used when adapterType is FILE)
    private readonly FileAdapter? _fileAdapter;

    public CsvAdapter(
        ICsvProcessingService csvProcessingService,
        IAdapterConfigurationService adapterConfig,
        BlobServiceClient blobServiceClient,
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        string? receiveFolder = null,
        string? fileMask = null,
        int? batchSize = null,
        string? fieldSeparator = null,
        string? destinationReceiveFolder = null,
        string? destinationFileMask = null,
        string? adapterType = null,
        SftpAdapter? sftpAdapter = null,
        FileAdapter? fileAdapter = null,
        string adapterRole = "Source",
        ILogger<CsvAdapter>? logger = null)
        : base(
            messageBoxService: messageBoxService,
            subscriptionService: subscriptionService,
            interfaceName: interfaceName ?? "FromCsvToSqlServerExample",
            adapterInstanceGuid: adapterInstanceGuid,
            batchSize: batchSize ?? 1000,
            adapterRole: adapterRole,
            logger: logger)
    {
        _csvProcessingService = csvProcessingService ?? throw new ArgumentNullException(nameof(csvProcessingService));
        _adapterConfig = adapterConfig ?? throw new ArgumentNullException(nameof(adapterConfig));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        
        _logger?.LogInformation("DEBUG CsvAdapter constructor: MessageBoxService={HasMessageBoxService}, InterfaceName={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}",
            _messageBoxService != null, _interfaceName, _adapterInstanceGuid.HasValue ? _adapterInstanceGuid.Value.ToString() : "NULL");
        _receiveFolder = receiveFolder;
        _fileMask = fileMask ?? "*.txt";
        _fieldSeparator = fieldSeparator ?? "║"; // Default: Box Drawing Double Vertical Line (U+2551)
        _destinationReceiveFolder = destinationReceiveFolder;
        _destinationFileMask = destinationFileMask ?? "*.txt";
        _logger = logger;
        
        // Adapter type and adapters
        _adapterType = adapterType ?? "FILE";
        _sftpAdapter = sftpAdapter;
        _fileAdapter = fileAdapter;
        
        // Validate adapters are provided when needed
        if (_adapterType.Equals("SFTP", StringComparison.OrdinalIgnoreCase) && _sftpAdapter == null)
        {
            throw new ArgumentException("SftpAdapter instance is required when adapterType is SFTP", nameof(sftpAdapter));
        }
        
        if (_adapterType.Equals("FILE", StringComparison.OrdinalIgnoreCase) && _fileAdapter == null)
        {
            throw new ArgumentException("FileAdapter instance is required when adapterType is FILE", nameof(fileAdapter));
        }
    }

    /// <summary>
    /// Gets or sets the CSV data directly. When set, processes and debatches the data to MessageBox.
    /// </summary>
    public string? CsvData
    {
        get => _csvData;
        set
        {
            _csvData = value;
            if (!string.IsNullOrWhiteSpace(_csvData))
            {
                // Process CSV data asynchronously when set
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Always process directly to MessageBox first, regardless of adapter type
                        // For RAW adapter type, also upload to csv-incoming for blob trigger processing
                        await ProcessCsvDataAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing CsvData property");
                    }
                });
            }
        }
    }

    /// <summary>
    /// Processes the CsvData property by parsing it and debatching to MessageBox
    /// For RAW adapter type: Creates a file from CsvData and uploads it to csv-incoming folder
    /// </summary>
    public async Task ProcessCsvDataAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_csvData))
        {
            _logger?.LogWarning("CsvData is empty, nothing to process");
            return;
        }

        try
        {
            _logger?.LogInformation("Processing CsvData property: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, AdapterType={AdapterType}, DataLength={DataLength}",
                _interfaceName, _adapterInstanceGuid, _adapterType, _csvData.Length);

            // Check MessageBox conditions first
            if (_messageBoxService == null || string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
            {
                _logger?.LogWarning("Cannot process CsvData: MessageBoxService={HasMessageBoxService}, InterfaceName={InterfaceName}, AdapterInstanceGuid={HasAdapterInstanceGuid}",
                    _messageBoxService != null, _interfaceName ?? "NULL", _adapterInstanceGuid.HasValue);
                return;
            }

            // For RAW adapter type: Also upload to csv-incoming for blob trigger processing
            // But still process directly to MessageBox first (if AdapterRole is Source)
            if (_adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("RAW adapter type detected. Uploading to csv-incoming AND processing directly to MessageBox.");
                // Upload to csv-incoming (will trigger blob trigger as backup)
                _ = Task.Run(async () => await UploadCsvDataToIncomingAsync(cancellationToken));
                // Continue with direct MessageBox processing below (if Source role)
            }

            // Parse CSV using CsvProcessingService with configured field separator
            var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(_csvData, _fieldSeparator, cancellationToken);

            _logger?.LogInformation("Successfully parsed CsvData: {HeaderCount} headers, {RecordCount} records",
                headers.Count, records.Count);

            // Write to MessageBox if AdapterRole is "Source"
            await WriteRecordsToMessageBoxAsync(headers, records, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing CsvData property");
            throw;
        }
    }

    /// <summary>
    /// Ensures the CSV blob container folders exist (csv-incoming, csv-processed, csv-error)
    /// Creates placeholder files to initialize the folders if they don't exist
    /// </summary>
    private async Task EnsureCsvFoldersExistAsync(CancellationToken cancellationToken)
    {
        // Use double-check locking pattern to ensure folders are initialized only once
        if (_csvFoldersInitialized)
        {
            return;
        }

        lock (_csvFoldersLock)
        {
            if (_csvFoldersInitialized)
            {
                return;
            }
        }

        try
        {
            var containerName = "csv-files";
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var folders = new[] { "csv-incoming", "csv-processed", "csv-error" };

            foreach (var folder in folders)
            {
                try
                {
                    // Create a placeholder file to ensure the folder exists
                    // In Azure Blob Storage, folders are virtual - they exist when blobs with that prefix exist
                    // We create a hidden placeholder file that marks the folder as initialized
                    var placeholderPath = $"{folder}/.folder-initialized";
                    var placeholderBlob = containerClient.GetBlobClient(placeholderPath);

                    if (!await placeholderBlob.ExistsAsync(cancellationToken))
                    {
                        var placeholderContent = Encoding.UTF8.GetBytes($"Folder initialized at {DateTime.UtcNow:O}");
                        await placeholderBlob.UploadAsync(
                            new BinaryData(placeholderContent),
                            new Azure.Storage.Blobs.Models.BlobUploadOptions
                            {
                                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                                {
                                    ContentType = "text/plain"
                                }
                            },
                            cancellationToken);

                        _logger?.LogInformation("Created CSV folder: {Folder} in container {Container}", folder, containerName);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error creating placeholder for folder {Folder}", folder);
                    // Continue with other folders even if one fails
                }
            }

            lock (_csvFoldersLock)
            {
                _csvFoldersInitialized = true;
            }

            _logger?.LogInformation("CSV folders initialization completed: csv-incoming, csv-processed, csv-error");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ensuring CSV folders exist");
            // Don't throw - allow adapter to continue even if folder creation fails
        }
    }

    /// <summary>
    /// Uploads CsvData to csv-incoming folder in blob storage
    /// This will trigger the blob trigger which processes the file
    /// </summary>
    private async Task UploadCsvDataToIncomingAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_csvData))
        {
            _logger?.LogWarning("CsvData is empty, cannot upload to csv-incoming");
            return;
        }

        try
        {
            if (_fileAdapter == null)
            {
                throw new InvalidOperationException("FileAdapter is required for uploading CsvData to csv-incoming");
            }

            // Ensure CSV folders exist before uploading
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-incoming", cancellationToken);
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-processed", cancellationToken);
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-error", cancellationToken);

            // Generate unique filename: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
            var now = DateTime.UtcNow;
            var fileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
            var blobPath = $"csv-files/csv-incoming/{fileName}";
            
            // Upload CSV data to csv-incoming folder
            await _fileAdapter.WriteFileAsync(blobPath, _csvData, cancellationToken);

            _logger?.LogInformation("Successfully uploaded CsvData to csv-incoming folder: {FileName}, DataLength={DataLength}",
                fileName, _csvData.Length);
            
            // Clean up old files in csv-incoming folder (keep only 10 most recent)
            await _fileAdapter.CleanupOldFilesAsync("csv-files", "csv-incoming", maxFiles: 10, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading CsvData to csv-incoming folder");
            throw;
        }
    }

    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        var hasExplicitSource = !string.IsNullOrWhiteSpace(source);
        var isRawAdapter = _adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase);

        if (!hasExplicitSource && !isRawAdapter)
            throw new ArgumentException("Source path cannot be empty", nameof(source));

        try
        {
            _logger?.LogInformation("Reading CSV from source: {Source}, AdapterType: {AdapterType}", source ?? "RAW", _adapterType);

            // RAW adapters without an explicit source rely on CsvData and blob upload
            if (isRawAdapter && !hasExplicitSource)
            {
                await ProcessCsvDataAsync(cancellationToken);
                return (new List<string>(), new List<Dictionary<string, string>>()); // Blob trigger will process uploaded file
            }

            // Treat RAW adapters with an explicit source path like FILE adapters (blob content already exists)
            if (isRawAdapter && hasExplicitSource)
            {
                _logger?.LogInformation("RAW adapter invoked with explicit source path {Source}. Processing as FILE adapter.", source);
            }

            if (_adapterType.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
            {
                // Use SftpAdapter to read CSV files from SFTP server
                if (_sftpAdapter == null)
                {
                    throw new InvalidOperationException("SFTP adapter is not configured. SftpAdapter instance is required for SFTP adapter type.");
                }
                
                // Use source parameter as folder, or let SftpAdapter use its configured folder
                var folder = !string.IsNullOrWhiteSpace(source) ? source : null;
                
                // Read CSV files and get successfully parsed files in one call to avoid reading files twice
                var (headers, records, successfullyParsedFiles) = await _sftpAdapter.ReadCsvFilesWithContentAsync(
                    _csvProcessingService, _fieldSeparator, folder, cancellationToken);
                
                // Upload only successfully parsed files to csv-incoming folder for blob trigger processing
                // This ensures consistency: only files that were successfully parsed are uploaded
                foreach (var (fileName, content) in successfullyParsedFiles)
                {
                    await UploadFileToIncomingAsync(content, fileName, cancellationToken);
                }
                
                return (headers, records);
            }

            // Use FileAdapter to read CSV files from blob storage
            if (_fileAdapter == null)
            {
                throw new InvalidOperationException("FILE adapter is not configured. FileAdapter instance is required for FILE adapter type.");
            }
            
            var effectiveSource = hasExplicitSource
                ? source
                : _fileAdapter.BuildDefaultBlobSourcePath(_receiveFolder);
            
            var (csvHeaders, csvRecords) = await _fileAdapter.ReadCsvFilesAsync(_csvProcessingService, _fieldSeparator, effectiveSource, cancellationToken);
            
            // For FILE adapter type: Copy files to csv-incoming folder if reading from a different folder
            // This will trigger the blob trigger which processes the file
            var pathParts = effectiveSource.Split('/', 2);
            if (pathParts.Length == 2)
            {
                var containerName = pathParts[0];
                var blobPath = pathParts[1];
                var isFolder = blobPath.EndsWith("/") || (!blobPath.Contains(".") && !string.IsNullOrWhiteSpace(blobPath));
                
                if (isFolder || !string.IsNullOrWhiteSpace(_receiveFolder))
                {
                    var filePaths = await _fileAdapter.ListFilesAsync(effectiveSource, cancellationToken);
                    foreach (var filePath in filePaths)
                    {
                        if (!filePath.Contains("csv-incoming/"))
                        {
                            var fullSourcePath = $"{containerName}/{filePath}";
                            var content = await _fileAdapter.ReadFileAsync(fullSourcePath, cancellationToken);
                            await UploadFileToIncomingAsync(content, filePath, cancellationToken);
                        }
                    }
                }
            }
            
            // Write to MessageBox if conditions are met
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName) && _adapterInstanceGuid.HasValue && csvRecords.Count > 0)
            {
                await WriteRecordsToMessageBoxAsync(csvHeaders, csvRecords, cancellationToken);
            }
            
            return (csvHeaders, csvRecords);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading CSV from source: {Source}", source ?? "RAW");
            throw;
        }
    }

    // WriteRecordsToMessageBoxAsync moved to AdapterBase.WriteRecordsToMessageBoxAsync

    // BuildDefaultBlobSourcePath removed - now handled by FileAdapter.BuildDefaultBlobSourcePath
    // ReadFromBlobStorageAsync removed - now handled by FileAdapter.ReadCsvFilesAsync


    public override async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination path cannot be empty", nameof(destination));

        try
        {
            _logger?.LogInformation("Writing CSV to destination: {Destination}, {RecordCount} records, AdapterRole: {AdapterRole}", 
                destination, records?.Count ?? 0, AdapterRole);

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
                // No messages found, return early
                return;
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

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobPath);

            // Use configured field separator
            var separator = _fieldSeparator;

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

            // Mark messages as processed if they came from MessageBox
            if (processedMessages != null && processedMessages.Count > 0)
            {
                await MarkMessagesAsProcessedAsync(processedMessages, $"Written to CSV destination: {destination}", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing CSV to destination: {Destination}", destination);
            throw;
        }
    }

    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default)
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

    public override Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        // CSV files don't require structure validation - they are schema-less
        // The structure is determined by the headers in the file
        _logger?.LogDebug("CSV adapter: No structure validation needed for destination: {Destination}", destination);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a filename matches a wildcard pattern (e.g., "*.txt", "data_*.csv")
    /// Supports * (matches any sequence) and ? (matches single character)
    /// </summary>
    private static bool MatchesFileMask(string fileName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true; // No pattern means match all
        
        // Normalize pattern: remove leading/trailing whitespace
        pattern = pattern.Trim();
        
        // If pattern doesn't contain wildcards, do exact match (case-insensitive)
        if (!pattern.Contains('*') && !pattern.Contains('?'))
        {
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
        
        // Convert wildcard pattern to regex
        // Escape special regex characters except * and ?
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            // If regex is invalid, fall back to simple string matching
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
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

    /// <summary>
    /// Expands filename variables in file mask pattern
    /// Supports: $datetime (replaced with current date/time with milliseconds: yyyyMMddHHmmss.fff)
    /// Example: "text_" + $datetime + ".txt" becomes "text_20240101120000.123.txt"
    /// </summary>
    private string ExpandFileNameVariables(string fileMask)
    {
        if (string.IsNullOrWhiteSpace(fileMask))
            return "output.txt";

        var result = fileMask;
        
        // Replace $datetime with current date/time (yyyyMMddHHmmss.fff)
        if (result.Contains("$datetime"))
        {
            var now = DateTime.UtcNow;
            var datetimeString = now.ToString("yyyyMMddHHmmss") + "." + now.Millisecond.ToString("D3");
            result = result.Replace("$datetime", datetimeString);
        }
        
        // If no variables were found and it's a wildcard pattern, generate a default filename
        if (result.Contains("*") || result.Contains("?"))
        {
            var now = DateTime.UtcNow;
            var datetimeString = now.ToString("yyyyMMddHHmmss") + "." + now.Millisecond.ToString("D3");
            
            // Replace wildcards with datetime
            result = result.Replace("*", datetimeString);
            result = result.Replace("?", datetimeString.Substring(0, Math.Min(1, datetimeString.Length)));
        }
        
        return result;
    }

    /// <summary>
    /// Uploads a file (from SFTP or other source) to csv-incoming folder in blob storage
    /// This will trigger the blob trigger which processes the file
    /// </summary>
    private async Task UploadFileToIncomingAsync(string csvContent, string originalFileName, CancellationToken cancellationToken)
    {
        if (_fileAdapter == null)
        {
            throw new InvalidOperationException("FileAdapter is required for uploading files to csv-incoming");
        }

        try
        {
            // Ensure CSV folders exist before uploading
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-incoming", cancellationToken);
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-processed", cancellationToken);
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-error", cancellationToken);

            // Generate unique filename: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
            var now = DateTime.UtcNow;
            var fileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
            var blobPath = $"csv-files/csv-incoming/{fileName}";
            
            // Upload CSV content to csv-incoming folder
            await _fileAdapter.WriteFileAsync(blobPath, csvContent, cancellationToken);

            _logger?.LogInformation("Successfully uploaded file to csv-incoming folder: {FileName} (from {OriginalFileName}), DataLength={DataLength}",
                fileName, originalFileName, csvContent.Length);
            
            // Clean up old files in csv-incoming folder (keep only 10 most recent)
            await _fileAdapter.CleanupOldFilesAsync("csv-files", "csv-incoming", maxFiles: 10, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading file to csv-incoming folder: {OriginalFileName}", originalFileName);
            throw;
        }
    }

    /// <summary>
    /// Copies a file from blob storage to csv-incoming folder
    /// This will trigger the blob trigger which processes the file
    /// </summary>
    private async Task CopyFileToIncomingAsync(string csvContent, string sourceBlobPath, CancellationToken cancellationToken)
    {
        if (_fileAdapter == null)
        {
            throw new InvalidOperationException("FileAdapter is required for copying files to csv-incoming");
        }

        try
        {
            // Ensure CSV folders exist before copying
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-incoming", cancellationToken);
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-processed", cancellationToken);
            await _fileAdapter.EnsureFolderExistsAsync("csv-files", "csv-error", cancellationToken);

            // Generate unique filename: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
            var now = DateTime.UtcNow;
            var uniqueFileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
            var blobPath = $"csv-files/csv-incoming/{uniqueFileName}";
            
            // Upload CSV content to csv-incoming folder
            await _fileAdapter.WriteFileAsync(blobPath, csvContent, cancellationToken);

            _logger?.LogInformation("Successfully copied file to csv-incoming folder: {FileName} (from {SourcePath}), DataLength={DataLength}",
                uniqueFileName, sourceBlobPath, csvContent.Length);
            
            // Clean up old files in csv-incoming folder (keep only 10 most recent)
            await _fileAdapter.CleanupOldFilesAsync("csv-files", "csv-incoming", maxFiles: 10, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error copying file to csv-incoming folder: {SourcePath}", sourceBlobPath);
            throw;
        }
    }

    // CleanupOldFilesAsync removed - now handled by FileAdapter.CleanupOldFilesAsync
}

