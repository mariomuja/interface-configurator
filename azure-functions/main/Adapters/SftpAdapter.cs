using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using ServiceBusMessage = InterfaceConfigurator.Main.Core.Interfaces.ServiceBusMessage;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// SFTP Adapter for reading files from SFTP servers
/// Can be used standalone or by other adapters (e.g., CsvAdapter) to retrieve data
/// </summary>
public class SftpAdapter : AdapterBase
{
    public override string AdapterName => "SFTP";
    public override string AdapterAlias => "SFTP";
    public override bool SupportsRead => true;
    public override bool SupportsWrite => true; // SFTP adapter can be used as destination

    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string? _password;
    private readonly string? _sshKey;
    private readonly string? _folder;
    private readonly string _fileMask;
    private readonly int _maxConnectionPoolSize;
    private readonly int _fileBufferSize;
    
    // Connection pool for SFTP clients
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly Queue<SftpClient> _connectionPool;

    public SftpAdapter(
        string host,
        int port,
        string username,
        IServiceBusService? serviceBusService = null,
        string adapterRole = "Source",
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        string? password = null,
        string? sshKey = null,
        string? folder = null,
        string? fileMask = null,
        int? maxConnectionPoolSize = null,
        int? fileBufferSize = null,
        int? batchSize = null,
        ILogger<SftpAdapter>? logger = null)
        : base(
            serviceBusService: serviceBusService,
            interfaceName: interfaceName,
            adapterInstanceGuid: adapterInstanceGuid,
            batchSize: batchSize ?? 1000,
            adapterRole: adapterRole,
            logger: logger)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("SFTP Host cannot be empty", nameof(host));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("SFTP Username cannot be empty", nameof(username));
        if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(sshKey))
            throw new ArgumentException("Either SFTP Password or SSH Key must be provided");

        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _sshKey = sshKey;
        _folder = folder;
        _fileMask = fileMask ?? "*.txt";
        _maxConnectionPoolSize = maxConnectionPoolSize ?? 5;
        _fileBufferSize = fileBufferSize ?? 8192;

        // Initialize connection pool
        _connectionSemaphore = new SemaphoreSlim(_maxConnectionPoolSize, _maxConnectionPoolSize);
        _connectionPool = new Queue<SftpClient>();
    }

    /// <summary>
    /// Reads file content from SFTP server
    /// </summary>
    /// <param name="source">Path to the file or folder on SFTP server</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    public async Task<string> ReadFileAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source path cannot be empty", nameof(source));

        var sftpClient = await GetSftpClientAsync(cancellationToken);
        try
        {
            if (!sftpClient.IsConnected)
            {
                throw new InvalidOperationException("SFTP client is not connected");
            }

            _logger?.LogInformation("Reading file from SFTP: {Source}", source);

            using var memoryStream = new MemoryStream();
            sftpClient.DownloadFile(source, memoryStream);
            memoryStream.Position = 0;

            var content = Encoding.UTF8.GetString(memoryStream.ToArray());
            _logger?.LogInformation("Successfully read file from SFTP: {Source}, Size={Size} bytes", source, content.Length);

            return content;
        }
        finally
        {
            await ReturnSftpClientAsync(sftpClient);
        }
    }

    /// <summary>
    /// Lists files in the specified folder matching the file mask
    /// </summary>
    /// <param name="folder">Folder path on SFTP server (optional, uses configured folder if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of file names matching the file mask</returns>
    public async Task<List<string>> ListFilesAsync(string? folder = null, CancellationToken cancellationToken = default)
    {
        var targetFolder = !string.IsNullOrWhiteSpace(folder) ? folder : _folder;
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new InvalidOperationException("SFTP folder must be specified either in configuration or as parameter");

        // Ensure folder path ends with /
        if (!targetFolder.EndsWith("/"))
            targetFolder += "/";

        var sftpClient = await GetSftpClientAsync(cancellationToken);
        try
        {
            if (!sftpClient.IsConnected)
            {
                throw new InvalidOperationException("SFTP client is not connected");
            }

            _logger?.LogInformation("Listing files from SFTP server {Host}:{Port}, folder: {Folder}, file mask: {FileMask}",
                _host, _port, targetFolder, _fileMask);

            var files = sftpClient.ListDirectory(targetFolder);
            var matchingFiles = files
                .Where(f => !f.IsDirectory && MatchesFileMask(f.Name, _fileMask))
                .Select(f => f.Name)
                .ToList();

            _logger?.LogInformation("Found {FileCount} files matching mask '{FileMask}' in SFTP folder {Folder}",
                matchingFiles.Count, _fileMask, targetFolder);

            return matchingFiles;
        }
        finally
        {
            await ReturnSftpClientAsync(sftpClient);
        }
    }

    /// <summary>
    /// Reads all files from the specified folder and returns their content
    /// </summary>
    /// <param name="folder">Folder path on SFTP server (optional, uses configured folder if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping file names to their content</returns>
    public async Task<Dictionary<string, string>> ReadAllFilesAsync(string? folder = null, CancellationToken cancellationToken = default)
    {
        var fileNames = await ListFilesAsync(folder, cancellationToken);
        var result = new Dictionary<string, string>();

        var targetFolder = !string.IsNullOrWhiteSpace(folder) ? folder : _folder;
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new InvalidOperationException("SFTP folder must be specified");

        if (!targetFolder.EndsWith("/"))
            targetFolder += "/";

        foreach (var fileName in fileNames)
        {
            try
            {
                var filePath = targetFolder + fileName;
                var content = await ReadFileAsync(filePath, cancellationToken);
                result[fileName] = content;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading file {FileName} from SFTP folder {Folder}", fileName, targetFolder);
                // Continue with next file
            }
        }

        return result;
    }

    /// <summary>
    /// Reads CSV files from SFTP and returns headers and records
    /// This is a convenience method for CSV processing
    /// </summary>
    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadCsvFilesAsync(
        ICsvProcessingService csvProcessingService,
        string fieldSeparator,
        string? folder = null,
        int skipHeaderLines = 0,
        int skipFooterLines = 0,
        char quoteCharacter = '"',
        CancellationToken cancellationToken = default)
    {
        var files = await ReadAllFilesAsync(folder, cancellationToken);
        var allHeaders = new List<string>();
        var allRecords = new List<Dictionary<string, string>>();

        foreach (var (fileName, content) in files)
        {
            try
            {
                var (headers, records) = await csvProcessingService.ParseCsvWithHeadersAsync(content, fieldSeparator, skipHeaderLines, skipFooterLines, quoteCharacter, cancellationToken);

                _logger?.LogInformation("Successfully parsed CSV from SFTP file {FileName}: {HeaderCount} headers, {RecordCount} records",
                    fileName, headers.Count, records.Count);

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
                _logger?.LogError(ex, "Error parsing CSV file {FileName} from SFTP", fileName);
                // Continue with next file
            }
        }

        return (allHeaders, allRecords);
    }

    /// <summary>
    /// Reads CSV files from SFTP and returns headers, records, and successfully parsed files
    /// This method avoids reading files twice by returning the files that were successfully parsed
    /// </summary>
    public async Task<(List<string> headers, List<Dictionary<string, string>> records, List<(string fileName, string content)> successfullyParsedFiles)> ReadCsvFilesWithContentAsync(
        ICsvProcessingService csvProcessingService,
        string fieldSeparator,
        string? folder = null,
        int skipHeaderLines = 0,
        int skipFooterLines = 0,
        char quoteCharacter = '"',
        CancellationToken cancellationToken = default)
    {
        var files = await ReadAllFilesAsync(folder, cancellationToken);
        var allHeaders = new List<string>();
        var allRecords = new List<Dictionary<string, string>>();
        var successfullyParsedFiles = new List<(string fileName, string content)>();

        foreach (var (fileName, content) in files)
        {
            try
            {
                var (headers, records) = await csvProcessingService.ParseCsvWithHeadersAsync(content, fieldSeparator, skipHeaderLines, skipFooterLines, quoteCharacter, cancellationToken);

                _logger?.LogInformation("Successfully parsed CSV from SFTP file {FileName}: {HeaderCount} headers, {RecordCount} records",
                    fileName, headers.Count, records.Count);

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
                // Only include files that were successfully parsed
                successfullyParsedFiles.Add((fileName, content));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error parsing CSV file {FileName} from SFTP. File will not be uploaded to blob storage.", fileName);
                // Continue with next file - do not add to successfullyParsedFiles
            }
        }

        return (allHeaders, allRecords, successfullyParsedFiles);
    }

    /// <summary>
    /// IAdapter.ReadAsync implementation - reads files from SFTP server
    /// Returns headers and records for compatibility with other adapters
    /// </summary>
    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        // This method requires ICsvProcessingService which is not available in IAdapter
        // For now, throw an exception indicating that ReadCsvFilesAsync should be used instead
        throw new NotSupportedException("Use ReadFileAsync, ReadAllFilesAsync, or ReadCsvFilesAsync methods instead. ReadAsync requires ICsvProcessingService.");
    }

    /// <summary>
    /// Processes files from SFTP server directly within the container app
    /// Reads files, parses CSV, debatches to single records, sends to Service Bus
    /// This method ensures all work is done in the container app, not via blob triggers
    /// </summary>
    public async Task ProcessFilesFromSftpAsync(
        ICsvProcessingService csvProcessingService,
        string fieldSeparator,
        int skipHeaderLines = 0,
        int skipFooterLines = 0,
        char quoteCharacter = '"',
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        if (AdapterRole != "Source")
        {
            LogProcessingState("ProcessFilesFromSftp", "Skipped", $"AdapterRole is {AdapterRole}, not Source");
            return;
        }

        if (_serviceBusService == null || string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        {
            LogProcessingState("ProcessFilesFromSftp", "Error", 
                $"ServiceBusService={_serviceBusService != null}, InterfaceName={_interfaceName ?? "NULL"}, AdapterInstanceGuid={_adapterInstanceGuid.HasValue}");
            return;
        }

        try
        {
            var targetFolder = !string.IsNullOrWhiteSpace(folder) ? folder : _folder;
            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                LogProcessingState("ProcessFilesFromSftp", "Error", "SFTP folder must be specified");
                throw new InvalidOperationException("SFTP folder must be specified either in configuration or as parameter");
            }

            LogProcessingState("ProcessFilesFromSftp", "Starting", 
                $"Interface: {_interfaceName}, AdapterInstanceGuid: {_adapterInstanceGuid.Value}, SFTP Folder: {targetFolder}, FileMask: {_fileMask}");

            // List files from SFTP server
            LogProcessingState("ProcessFilesFromSftp", "ListingFiles", $"Listing files from SFTP folder: {targetFolder}");
            var fileNames = await ListFilesAsync(targetFolder, cancellationToken);

            if (fileNames == null || fileNames.Count == 0)
            {
                LogProcessingState("ProcessFilesFromSftp", "NoFiles", $"No files found in SFTP folder: {targetFolder}");
                return;
            }

            LogProcessingState("ProcessFilesFromSftp", "FilesFound", $"Found {fileNames.Count} file(s) to process");

            // Ensure folder path ends with /
            if (!targetFolder.EndsWith("/"))
                targetFolder += "/";

            // Process each file
            var allHeaders = new List<string>();
            var allRecords = new List<Dictionary<string, string>>();
            var processedFiles = 0;
            var failedFiles = 0;

            foreach (var fileName in fileNames)
            {
                try
                {
                    var filePath = targetFolder + fileName;
                    LogProcessingState("ProcessFilesFromSftp", "ReadingFile", $"Reading file: {fileName} from SFTP");

                    // Read file content
                    var fileContent = await ReadFileAsync(filePath, cancellationToken);
                    LogProcessingState("ProcessFilesFromSftp", "FileRead", $"Read {fileContent.Length} characters from {fileName}");

                    // Parse CSV
                    LogProcessingState("ProcessFilesFromSftp", "ParsingCSV", $"Parsing CSV content from {fileName}");
                    var (headers, records) = await csvProcessingService.ParseCsvWithHeadersAsync(
                        fileContent, 
                        fieldSeparator, 
                        skipHeaderLines, 
                        skipFooterLines, 
                        quoteCharacter, 
                        cancellationToken);

                    LogProcessingState("ProcessFilesFromSftp", "CSVParsed", 
                        $"Parsed {headers.Count} headers and {records.Count} records from {fileName}");

                    // Use headers from first file, or merge if different
                    if (allHeaders.Count == 0)
                    {
                        allHeaders = headers;
                    }
                    else if (!headers.SequenceEqual(allHeaders))
                    {
                        LogProcessingState("ProcessFilesFromSftp", "HeaderMismatch", 
                            $"Headers differ between files. Using headers from first file. File: {fileName}");
                    }

                    allRecords.AddRange(records);
                    processedFiles++;

                    LogProcessingState("ProcessFilesFromSftp", "FileProcessed", 
                        $"Successfully processed {fileName}: {records.Count} records");
                }
                catch (Exception ex)
                {
                    failedFiles++;
                    LogProcessingState("ProcessFilesFromSftp", "FileError", 
                        $"Failed to process file {fileName} from SFTP", ex);
                    // Continue with next file
                }
            }

            if (allRecords.Count == 0)
            {
                LogProcessingState("ProcessFilesFromSftp", "NoRecords", 
                    $"No records found in {processedFiles} processed file(s), {failedFiles} failed file(s)");
                return;
            }

            // Debatch and send to Service Bus (one message per record)
            LogProcessingState("ProcessFilesFromSftp", "Debatching", 
                $"Debatching {allRecords.Count} records from {processedFiles} file(s) to individual Service Bus messages");
            
            var messagesSent = await WriteRecordsToServiceBusWithDebatchingAsync(allHeaders, allRecords, cancellationToken);
            
            LogProcessingState("ProcessFilesFromSftp", "Completed", 
                $"Successfully processed {processedFiles} file(s) from SFTP, {failedFiles} failed, " +
                $"sent {messagesSent} messages to Service Bus for {allRecords.Count} records");
        }
        catch (Exception ex)
        {
            LogProcessingState("ProcessFilesFromSftp", "Error", 
                "Failed to process files from SFTP server", ex);
            throw;
        }
    }

    /// <summary>
    /// IAdapter.WriteAsync implementation - writes data to SFTP server
    /// When AdapterRole is "Destination", reads messages from Service Bus first
    /// </summary>
    public override async Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination path cannot be empty", nameof(destination));

        _logger?.LogInformation("Writing to SFTP destination: {Destination}, {RecordCount} records, AdapterRole: {AdapterRole}", 
            destination, records?.Count ?? 0, AdapterRole);

        // Read messages from Service Bus if AdapterRole is "Destination"
        List<ServiceBusMessage>? processedMessages = null;
        var serviceBusResult = await ReadMessagesFromServiceBusAsync(cancellationToken);
        if (serviceBusResult.HasValue)
        {
            var (messageHeaders, messageRecords, messages) = serviceBusResult.Value;
            headers = messageHeaders;
            records = messageRecords;
            processedMessages = messages;
        }
        else if (AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
        {
            // No messages found, return early
            return;
        }

        // Validate headers and records
        if (headers == null || headers.Count == 0)
            throw new ArgumentException("Headers cannot be empty", nameof(headers));

        // Write CSV content to SFTP server
        var csvContent = new StringBuilder();
        csvContent.AppendLine(string.Join(",", headers.Select(h => EscapeCsvValue(h, ","))));
        
        if (records != null)
        {
            foreach (var record in records)
            {
                var values = headers.Select(header => record.GetValueOrDefault(header, string.Empty));
                csvContent.AppendLine(string.Join(",", values.Select(v => EscapeCsvValue(v, ","))));
            }
        }

        // Determine destination path on SFTP server
        var remotePath = !string.IsNullOrWhiteSpace(_folder) ? _folder : destination;
        if (!remotePath.EndsWith("/"))
            remotePath += "/";
        
        var fileName = $"output_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var fullPath = remotePath + fileName;

        var sftpClient = await GetSftpClientAsync(cancellationToken);
        try
        {
            var contentBytes = Encoding.UTF8.GetBytes(csvContent.ToString());
            using var memoryStream = new MemoryStream(contentBytes);
            sftpClient.UploadFile(memoryStream, fullPath);
            
            _logger?.LogInformation("Successfully wrote {RecordCount} records to SFTP: {FullPath}", records?.Count ?? 0, fullPath);
            
            // Mark messages as processed if they came from Service Bus
            if (processedMessages != null && processedMessages.Count > 0)
            {
                LogProcessingState("SftpAdapter.WriteAsync", "MarkingProcessed", 
                    $"Marking {processedMessages.Count} messages as processed after writing to SFTP");
                await MarkMessagesAsProcessedAsync(processedMessages, $"Written to SFTP destination: {fullPath}", cancellationToken);
            }
        }
        finally
        {
            await ReturnSftpClientAsync(sftpClient);
        }
    }
    
    private string EscapeCsvValue(string value, string separator)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(separator) || value.Contains("\n") || value.Contains("\r") || value.Contains("\""))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <summary>
    /// IAdapter.GetSchemaAsync implementation - not supported for SFTP adapter
    /// Schema must be determined from CSV content, not from SFTP itself
    /// </summary>
    public override Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("SFTP adapter does not support schema detection. Use ICsvProcessingService to analyze CSV content.");
    }

    /// <summary>
    /// IAdapter.EnsureDestinationStructureAsync implementation - not supported for SFTP adapter
    /// </summary>
    public override Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("SFTP adapter does not support writing. It is read-only.");
    }

    private async Task<SftpClient> GetSftpClientAsync(CancellationToken cancellationToken)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken);

        lock (_connectionPool)
        {
            if (_connectionPool.Count > 0)
            {
                var client = _connectionPool.Dequeue();
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
        lock (_connectionPool)
        {
            if (client.IsConnected && _connectionPool.Count < _maxConnectionPoolSize)
            {
                _connectionPool.Enqueue(client);
                _connectionSemaphore.Release();
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
        _connectionSemaphore.Release();
    }

    private SftpClient CreateSftpClient()
    {
        ConnectionInfo connectionInfo;

        if (!string.IsNullOrWhiteSpace(_sshKey))
        {
            // Use SSH key authentication
            var keyBytes = Convert.FromBase64String(_sshKey);
            using var keyStream = new MemoryStream(keyBytes);
            var privateKeyFile = new PrivateKeyFile(keyStream);
            connectionInfo = new ConnectionInfo(_host, _port, _username, new PrivateKeyAuthenticationMethod(_username, privateKeyFile));
        }
        else if (!string.IsNullOrWhiteSpace(_password))
        {
            // Use password authentication
            connectionInfo = new ConnectionInfo(_host, _port, _username, new PasswordAuthenticationMethod(_username, _password));
        }
        else
        {
            throw new InvalidOperationException("Either SFTP Password or SSH Key must be provided.");
        }

        var client = new SftpClient(connectionInfo);
        client.Connect();

        if (!client.IsConnected)
        {
            throw new InvalidOperationException($"Failed to connect to SFTP server {_host}:{_port}");
        }

        _logger?.LogInformation("Successfully connected to SFTP server {Host}:{Port}", _host, _port);
        return client;
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

    /// <summary>
    /// Disposes all SFTP connections in the pool
    /// </summary>
    public void Dispose()
    {
        lock (_connectionPool)
        {
            while (_connectionPool.Count > 0)
            {
                try
                {
                    var client = _connectionPool.Dequeue();
                    client?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing SFTP client during cleanup");
                }
            }
        }
        _connectionSemaphore?.Dispose();
    }
}

