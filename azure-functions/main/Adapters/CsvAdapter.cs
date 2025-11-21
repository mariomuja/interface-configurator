using System.Text;
using System.Linq;
using System.IO;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// CSV Adapter for reading from and writing to CSV files in Azure Blob Storage
/// When used as Source: Reads CSV and writes to MessageBox
/// When used as Destination: Reads from MessageBox and writes CSV
/// </summary>
public class CsvAdapter : IAdapter
{
    public string AdapterName => "CSV";
    public string AdapterAlias => "CSV";
    public bool SupportsRead => true;
    public bool SupportsWrite => true;

    private readonly ICsvProcessingService _csvProcessingService;
    private readonly IAdapterConfigurationService _adapterConfig;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IMessageBoxService? _messageBoxService;
    private readonly IMessageSubscriptionService? _subscriptionService;
    private readonly string? _interfaceName;
    private readonly Guid? _adapterInstanceGuid;
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
    
    // SFTP properties
    private readonly string _adapterType;
    private readonly string? _sftpHost;
    private readonly int _sftpPort;
    private readonly string? _sftpUsername;
    private readonly string? _sftpPassword;
    private readonly string? _sftpSshKey;
    private readonly string? _sftpFolder;
    private readonly string _sftpFileMask;
    private readonly int _sftpMaxConnectionPoolSize;
    private readonly int _sftpFileBufferSize;
    private readonly SemaphoreSlim? _sftpConnectionSemaphore;
    private readonly Queue<SftpClient>? _sftpConnectionPool;

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
        string? sftpHost = null,
        int? sftpPort = null,
        string? sftpUsername = null,
        string? sftpPassword = null,
        string? sftpSshKey = null,
        string? sftpFolder = null,
        string? sftpFileMask = null,
        int? sftpMaxConnectionPoolSize = null,
        int? sftpFileBufferSize = null,
        ILogger<CsvAdapter>? logger = null)
    {
        _csvProcessingService = csvProcessingService ?? throw new ArgumentNullException(nameof(csvProcessingService));
        _adapterConfig = adapterConfig ?? throw new ArgumentNullException(nameof(adapterConfig));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _messageBoxService = messageBoxService;
        _subscriptionService = subscriptionService;
        _interfaceName = interfaceName ?? "FromCsvToSqlServerExample";
        _adapterInstanceGuid = adapterInstanceGuid;
        _receiveFolder = receiveFolder;
        _fileMask = fileMask ?? "*.txt";
        _batchSize = batchSize ?? 1000; // Increased default batch size from 100 to 1000 for better performance
        _fieldSeparator = fieldSeparator ?? "║"; // Default: Box Drawing Double Vertical Line (U+2551)
        _destinationReceiveFolder = destinationReceiveFolder;
        _destinationFileMask = destinationFileMask ?? "*.txt";
        _logger = logger;
        
        // SFTP properties
        _adapterType = adapterType ?? "FILE";
        _sftpHost = sftpHost;
        _sftpPort = sftpPort ?? 22;
        _sftpUsername = sftpUsername;
        _sftpPassword = sftpPassword;
        _sftpSshKey = sftpSshKey;
        _sftpFolder = sftpFolder;
        _sftpFileMask = sftpFileMask ?? "*.txt";
        _sftpMaxConnectionPoolSize = sftpMaxConnectionPoolSize ?? 5;
        _sftpFileBufferSize = sftpFileBufferSize ?? 8192;
        
        // Initialize SFTP connection pool if SFTP adapter type
        if (_adapterType.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
        {
            _sftpConnectionSemaphore = new SemaphoreSlim(_sftpMaxConnectionPoolSize, _sftpMaxConnectionPoolSize);
            _sftpConnectionPool = new Queue<SftpClient>();
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

            // For RAW adapter type: Create file from CsvData and upload to csv-incoming
            if (_adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
            {
                await UploadCsvDataToIncomingAsync(cancellationToken);
                return; // File upload will trigger blob trigger which processes the file
            }

            // For other adapter types, process directly to MessageBox (legacy behavior)
            if (_messageBoxService == null || string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
            {
                _logger?.LogWarning("Cannot process CsvData: MessageBoxService, InterfaceName, or AdapterInstanceGuid is missing");
                return;
            }

            // Parse CSV using CsvProcessingService with configured field separator
            var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(_csvData, _fieldSeparator, cancellationToken);

            _logger?.LogInformation("Successfully parsed CsvData: {HeaderCount} headers, {RecordCount} records",
                headers.Count, records.Count);

            // Process records in batches of _batchSize, then debatch each batch into single rows
            _logger?.LogInformation("Processing CsvData in batches of {BatchSize} rows: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, TotalRecords={RecordCount}",
                _batchSize, _interfaceName, _adapterInstanceGuid.Value, records.Count);

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

            _logger?.LogInformation("Completed processing CsvData: {TotalRecords} records debatched into {TotalMessages} messages",
                records.Count, records.Count);
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
            // Ensure CSV folders exist before uploading
            await EnsureCsvFoldersExistAsync(cancellationToken);

            // Generate unique filename: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
            var now = DateTime.UtcNow;
            var fileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
            var blobPath = $"csv-incoming/{fileName}";
            
            // Get container client (default: csv-files)
            var containerName = "csv-files";
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            // Upload CSV data to csv-incoming folder
            var blobClient = containerClient.GetBlobClient(blobPath);
            var content = Encoding.UTF8.GetBytes(_csvData);
            
            await blobClient.UploadAsync(
                new BinaryData(content),
                new Azure.Storage.Blobs.Models.BlobUploadOptions
                {
                    HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "text/csv"
                    }
                },
                cancellationToken);

            _logger?.LogInformation("Successfully uploaded CsvData to csv-incoming folder: {FileName}, DataLength={DataLength}",
                fileName, _csvData.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading CsvData to csv-incoming folder");
            throw;
        }
    }

    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) && !_adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Source path cannot be empty", nameof(source));

        try
        {
            _logger?.LogInformation("Reading CSV from source: {Source}, AdapterType: {AdapterType}", source ?? "RAW", _adapterType);

            // Check adapter type: RAW, SFTP, or FILE (Azure Blob Storage)
            if (_adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
            {
                // For RAW type, process CsvData and upload to csv-incoming
                // The blob trigger will then process the file
                await ProcessCsvDataAsync(cancellationToken);
                return (new List<string>(), new List<Dictionary<string, string>>()); // Empty result, file processing happens via blob trigger
            }
            else if (_adapterType.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
            {
                return await ReadFromSftpAsync(source, cancellationToken);
            }
            else // FILE type
            {
                return await ReadFromBlobStorageAsync(source, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading CSV from source: {Source}", source ?? "RAW");
            throw;
        }
    }

    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadFromBlobStorageAsync(string source, CancellationToken cancellationToken)
    {
        // Parse source path: "container-name/blob-path" or "container-name/folder/blob-name" or "container-name/folder/" (folder)
        var pathParts = source.Split('/', 2);
        if (pathParts.Length != 2)
            throw new ArgumentException($"Invalid source path format. Expected 'container/path', got: {source}", nameof(source));

        var containerName = pathParts[0];
        var blobPath = pathParts[1];

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        
        // Check if source is a folder (ends with '/' or doesn't contain a file extension)
        bool isFolder = blobPath.EndsWith("/") || (!blobPath.Contains(".") && !string.IsNullOrWhiteSpace(blobPath));
        
        var allHeaders = new List<string>();
        var allRecords = new List<Dictionary<string, string>>();

        if (isFolder || !string.IsNullOrWhiteSpace(_receiveFolder))
            {
            // Process all CSV files in the folder
            _logger?.LogInformation("Processing folder: {BlobPath} in container {ContainerName}", blobPath, containerName);
            
            // Ensure folder path ends with /
            if (!blobPath.EndsWith("/"))
                blobPath += "/";

            // List all blobs in the folder
            var blobs = containerClient.GetBlobsAsync(prefix: blobPath, cancellationToken: cancellationToken);
            var csvFiles = new List<string>();
            
            await foreach (var blobItem in blobs)
            {
                // Extract filename from blob path (e.g., "folder/subfolder/file.txt" -> "file.txt")
                var fileName = blobItem.Name.Contains('/') 
                    ? blobItem.Name.Substring(blobItem.Name.LastIndexOf('/') + 1)
                    : blobItem.Name;
                
                // Filter files by file mask pattern (e.g., "*.txt", "*.csv", "data_*.txt")
                if (MatchesFileMask(fileName, _fileMask))
                {
                    csvFiles.Add(blobItem.Name);
                }
            }

            _logger?.LogInformation("Found {FileCount} files matching mask '{FileMask}' in folder {BlobPath}", csvFiles.Count, _fileMask, blobPath);

            // Process each CSV file
            foreach (var csvFile in csvFiles)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(csvFile);
                    
                    if (!await blobClient.ExistsAsync(cancellationToken))
                    {
                        _logger?.LogWarning("CSV file not found: {CsvFile}", csvFile);
                        continue;
                    }

                    // Download blob content
                    var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
                    var csvContent = downloadResult.Value.Content.ToString();

                    // Parse CSV using CsvProcessingService with configured field separator
                    var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, _fieldSeparator, cancellationToken);

                    _logger?.LogInformation("Successfully read CSV from {CsvFile}: {HeaderCount} headers, {RecordCount} records", 
                        csvFile, headers.Count, records.Count);

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

                    // For FILE adapter type: Copy file to csv-incoming folder if reading from a different folder
                    // This will trigger the blob trigger which processes the file
                    // Only copy if source folder is not already csv-incoming
                    if (!csvFile.Contains("csv-incoming/"))
                    {
                        await CopyFileToIncomingAsync(csvContent, csvFile, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing CSV file {CsvFile} from folder {BlobPath}", csvFile, blobPath);
                    // Continue with next file
                }
            }
        }
        else
        {
            // Process single file
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
                throw new FileNotFoundException($"CSV file not found: {source}");

            // Download blob content
            var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
            var csvContent = downloadResult.Value.Content.ToString();

            // Parse CSV using CsvProcessingService
            var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, fieldSeparator: _fieldSeparator, cancellationToken);

            _logger?.LogInformation("Successfully read CSV from {Source}: {HeaderCount} headers, {RecordCount} records", 
                source, headers.Count, records.Count);

            allHeaders = headers;
            allRecords = records;

            // If MessageBoxService is available, debatch and write to MessageBox as Source adapter
            // Process records in batches of _batchSize, then debatch each batch into single rows
            if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName) && _adapterInstanceGuid.HasValue)
            {
                _logger?.LogInformation("Processing CSV data in batches of {BatchSize} rows: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, TotalRecords={RecordCount}", 
                    _batchSize, _interfaceName, _adapterInstanceGuid.Value, records.Count);
                
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
                
                _logger?.LogInformation("Completed processing all batches: {TotalRecords} records debatched into {TotalMessages} messages", 
                    records.Count, records.Count);
            }
        }

        if (_messageBoxService != null && !string.IsNullOrWhiteSpace(_interfaceName) && !_adapterInstanceGuid.HasValue)
        {
            _logger?.LogWarning("AdapterInstanceGuid is missing. Messages will not be written to MessageBox.");
        }

        return (allHeaders, allRecords);
    }

    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadFromSftpAsync(string source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_sftpHost) || string.IsNullOrWhiteSpace(_sftpUsername))
        {
            throw new InvalidOperationException("SFTP Host and Username must be configured for SFTP adapter type.");
        }

        var allHeaders = new List<string>();
        var allRecords = new List<Dictionary<string, string>>();

        // Use SFTP folder from configuration or source parameter
        var remoteFolder = !string.IsNullOrWhiteSpace(_sftpFolder) ? _sftpFolder : source;
        
        // Ensure folder path ends with /
        if (!remoteFolder.EndsWith("/"))
            remoteFolder += "/";

        _logger?.LogInformation("Reading CSV files from SFTP server {Host}:{Port}, folder: {Folder}, file mask: {FileMask}", 
            _sftpHost, _sftpPort, remoteFolder, _sftpFileMask);

        SftpClient? sftpClient = null;
        try
        {
            // Get SFTP client from pool or create new one
            sftpClient = await GetSftpClientAsync(cancellationToken);
            
            if (sftpClient == null || !sftpClient.IsConnected)
            {
                throw new InvalidOperationException("Failed to establish SFTP connection.");
            }

            // List files in remote folder
            var files = sftpClient.ListDirectory(remoteFolder);
            var csvFiles = files
                .Where(f => !f.IsDirectory && MatchesFileMask(f.Name, _sftpFileMask))
                .ToList();

            _logger?.LogInformation("Found {FileCount} files matching mask '{FileMask}' in SFTP folder {Folder}", 
                csvFiles.Count, _sftpFileMask, remoteFolder);

            // Process each CSV file
            foreach (var file in csvFiles)
            {
                try
                {
                    var remoteFilePath = remoteFolder + file.Name;
                    _logger?.LogInformation("Reading CSV file from SFTP: {RemoteFilePath}", remoteFilePath);

                    // Download file content using buffer
                    using var memoryStream = new MemoryStream();
                    sftpClient.DownloadFile(remoteFilePath, memoryStream);
                    memoryStream.Position = 0;
                    
                    var csvContent = Encoding.UTF8.GetString(memoryStream.ToArray());

                    // Parse CSV using CsvProcessingService
                    var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, _fieldSeparator, cancellationToken);

                    _logger?.LogInformation("Successfully read CSV from SFTP {RemoteFilePath}: {HeaderCount} headers, {RecordCount} records", 
                        remoteFilePath, headers.Count, records.Count);

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

                    // For SFTP adapter type: Upload file to csv-incoming folder
                    // This will trigger the blob trigger which processes the file
                    await UploadFileToIncomingAsync(csvContent, file.Name, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing CSV file {FileName} from SFTP folder {Folder}", file.Name, remoteFolder);
                    // Continue with next file
                }
            }
        }
        finally
        {
            // Return SFTP client to pool
            if (sftpClient != null)
            {
                await ReturnSftpClientAsync(sftpClient);
            }
        }

        return (allHeaders, allRecords);
    }

    private async Task<SftpClient> GetSftpClientAsync(CancellationToken cancellationToken)
    {
        if (_sftpConnectionSemaphore == null || _sftpConnectionPool == null)
        {
            return CreateSftpClient();
        }

        await _sftpConnectionSemaphore.WaitAsync(cancellationToken);
        
        lock (_sftpConnectionPool)
        {
            if (_sftpConnectionPool.Count > 0)
            {
                var client = _sftpConnectionPool.Dequeue();
                if (client.IsConnected)
                {
                    return client;
                }
                else
                {
                    // Client disconnected, dispose and create new one
                    try 
                    { 
                        client.Dispose(); 
                    } 
                    catch (Exception disposeEx)
                    {
                        _logger?.LogWarning(disposeEx, "Error disposing disconnected SFTP client: {ErrorMessage}", disposeEx.Message);
                    }
                }
            }
        }

        // Create new client if pool is empty
        return CreateSftpClient();
    }

    private async Task ReturnSftpClientAsync(SftpClient client)
    {
        if (_sftpConnectionSemaphore == null || _sftpConnectionPool == null)
        {
            client?.Dispose();
            return;
        }

        lock (_sftpConnectionPool)
        {
            if (client.IsConnected && _sftpConnectionPool.Count < _sftpMaxConnectionPoolSize)
            {
                _sftpConnectionPool.Enqueue(client);
                _sftpConnectionSemaphore.Release();
                return;
            }
        }

        // Pool is full or client disconnected, dispose it
        try 
        { 
            client?.Dispose(); 
        } 
        catch (Exception disposeEx)
        {
            _logger?.LogWarning(disposeEx, "Error disposing SFTP client: {ErrorMessage}", disposeEx.Message);
        }
        _sftpConnectionSemaphore.Release();
    }

    private SftpClient CreateSftpClient()
    {
        ConnectionInfo connectionInfo;
        
        if (!string.IsNullOrWhiteSpace(_sftpSshKey))
        {
            // Use SSH key authentication
            var keyBytes = Convert.FromBase64String(_sftpSshKey);
            using var keyStream = new MemoryStream(keyBytes);
            var privateKeyFile = new PrivateKeyFile(keyStream);
            connectionInfo = new ConnectionInfo(_sftpHost, _sftpPort, _sftpUsername, new PrivateKeyAuthenticationMethod(_sftpUsername, privateKeyFile));
        }
        else if (!string.IsNullOrWhiteSpace(_sftpPassword))
        {
            // Use password authentication
            connectionInfo = new ConnectionInfo(_sftpHost, _sftpPort, _sftpUsername, new PasswordAuthenticationMethod(_sftpUsername, _sftpPassword));
        }
        else
        {
            throw new InvalidOperationException("Either SFTP Password or SSH Key must be provided.");
        }

        var client = new SftpClient(connectionInfo);
        client.Connect();
        
        if (!client.IsConnected)
        {
            throw new InvalidOperationException($"Failed to connect to SFTP server {_sftpHost}:{_sftpPort}");
        }

        _logger?.LogInformation("Successfully connected to SFTP server {Host}:{Port}", _sftpHost, _sftpPort);
        return client;
    }

    public async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination path cannot be empty", nameof(destination));

        try
        {
            _logger?.LogInformation("Writing CSV to destination: {Destination}, {RecordCount} records", destination, records?.Count ?? 0);

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
                            message.MessageId, AdapterName, $"Written to CSV destination: {destination}", cancellationToken);
                        
                        // Mark message as processed (releases lock automatically)
                        await _messageBoxService.MarkMessageAsProcessedAsync(
                            message.MessageId, $"Written to CSV destination: {destination}", cancellationToken);
                        
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
                        _logger?.LogError(ex, "Error marking subscription as processed for message {MessageId}", message.MessageId);
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
        try
        {
            // Ensure CSV folders exist before uploading
            await EnsureCsvFoldersExistAsync(cancellationToken);

            // Generate unique filename: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
            var now = DateTime.UtcNow;
            var fileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
            var blobPath = $"csv-incoming/{fileName}";
            
            // Get container client (default: csv-files)
            var containerName = "csv-files";
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            // Upload CSV content to csv-incoming folder
            var blobClient = containerClient.GetBlobClient(blobPath);
            var content = Encoding.UTF8.GetBytes(csvContent);
            
            await blobClient.UploadAsync(
                new BinaryData(content),
                new Azure.Storage.Blobs.Models.BlobUploadOptions
                {
                    HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "text/csv"
                    }
                },
                cancellationToken);

            _logger?.LogInformation("Successfully uploaded file to csv-incoming folder: {FileName} (from {OriginalFileName}), DataLength={DataLength}",
                fileName, originalFileName, csvContent.Length);
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
        try
        {
            // Ensure CSV folders exist before copying
            await EnsureCsvFoldersExistAsync(cancellationToken);

            // Generate unique filename: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
            var now = DateTime.UtcNow;
            var uniqueFileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
            var blobPath = $"csv-incoming/{uniqueFileName}";
            
            // Get container client (default: csv-files)
            var containerName = "csv-files";
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            
            // Upload CSV content to csv-incoming folder
            var blobClient = containerClient.GetBlobClient(blobPath);
            var content = Encoding.UTF8.GetBytes(csvContent);
            
            await blobClient.UploadAsync(
                new BinaryData(content),
                new Azure.Storage.Blobs.Models.BlobUploadOptions
                {
                    HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "text/csv"
                    }
                },
                cancellationToken);

            _logger?.LogInformation("Successfully copied file to csv-incoming folder: {FileName} (from {SourcePath}), DataLength={DataLength}",
                uniqueFileName, sourceBlobPath, csvContent.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error copying file to csv-incoming folder: {SourcePath}", sourceBlobPath);
            throw;
        }
    }
}

