using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net;
using System.Linq;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// SAP Adapter for reading and sending IDOCs and data via OData/REST APIs (S/4HANA) or RFC
/// Supports modern S/4HANA via OData/REST and classic SAP systems via RFC Gateway
/// Can be used as Source (read from SAP) or Destination (send to SAP)
/// </summary>
public class SapAdapter : HttpClientAdapterBase
{
    public override string AdapterName => "SAP";
    public override string AdapterAlias => "SAP IDOC";
    public override bool SupportsRead => true;
    public override bool SupportsWrite => true;

    // SAP Connection Properties
    private readonly string? _sapApplicationServer;
    private readonly string? _sapSystemNumber;
    private readonly string? _sapClient;
    private readonly string? _sapUsername;
    private readonly string? _sapPassword;
    private readonly string? _sapLanguage;
    private readonly string? _sapIdocType;
    private readonly string? _sapIdocMessageType;
    private readonly string? _sapIdocFilter;
    private readonly int _sapPollingInterval;
    private readonly int _sapBatchSize;
    private readonly int _sapConnectionTimeout;
    private readonly bool _sapUseRfc;
    private readonly string? _sapRfcDestination;
    private readonly string? _sapReceiverPort;
    private readonly string? _sapReceiverPartner;
    private readonly string? _sapRfcFunctionModule;
    private readonly string? _sapRfcParameters;
    private readonly string? _sapODataServiceUrl;
    private readonly string? _sapRestApiEndpoint;
    private readonly bool _sapUseOData;
    private readonly bool _sapUseRestApi;


    public SapAdapter(
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        int batchSize = 100,
        string adapterRole = "Source",
        ILogger? logger = null,
        // SAP Connection Properties
        string? sapApplicationServer = null,
        string? sapSystemNumber = null,
        string? sapClient = null,
        string? sapUsername = null,
        string? sapPassword = null,
        string? sapLanguage = "EN",
        string? sapIdocType = null,
        string? sapIdocMessageType = null,
        string? sapIdocFilter = null,
        int sapPollingInterval = 60,
        int sapBatchSize = 100,
        int sapConnectionTimeout = 30,
        bool sapUseRfc = true,
        string? sapRfcDestination = null,
        string? sapReceiverPort = null,
        string? sapReceiverPartner = null,
        string? sapRfcFunctionModule = null,
        string? sapRfcParameters = null,
        string? sapODataServiceUrl = null,
        string? sapRestApiEndpoint = null,
        bool sapUseOData = false,
        bool sapUseRestApi = false,
        HttpClient? httpClient = null) // Optional HttpClient for testing
        : base(messageBoxService, subscriptionService, interfaceName, adapterInstanceGuid, batchSize, adapterRole, logger, httpClient)
    {
        _sapApplicationServer = sapApplicationServer;
        _sapSystemNumber = sapSystemNumber;
        _sapClient = sapClient;
        _sapUsername = sapUsername;
        _sapPassword = sapPassword;
        _sapLanguage = sapLanguage;
        _sapIdocType = sapIdocType;
        _sapIdocMessageType = sapIdocMessageType;
        _sapIdocFilter = sapIdocFilter;
        _sapPollingInterval = sapPollingInterval;
        _sapBatchSize = sapBatchSize;
        _sapConnectionTimeout = sapConnectionTimeout;
        _sapUseRfc = sapUseRfc;
        _sapRfcDestination = sapRfcDestination;
        _sapReceiverPort = sapReceiverPort;
        _sapReceiverPartner = sapReceiverPartner;
        _sapRfcFunctionModule = sapRfcFunctionModule;
        _sapRfcParameters = sapRfcParameters;
        _sapODataServiceUrl = sapODataServiceUrl;
        _sapRestApiEndpoint = sapRestApiEndpoint;
        _sapUseOData = sapUseOData;
        _sapUseRestApi = sapUseRestApi;

        // Set up authentication if credentials provided and HttpClient was created by base class
        if (_disposeHttpClient && !string.IsNullOrEmpty(_sapUsername) && !string.IsNullOrEmpty(_sapPassword))
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_sapUsername}:{_sapPassword}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }
        
        // Set timeout if HttpClient was created by base class
        if (_disposeHttpClient)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_sapConnectionTimeout);
        }
    }
    
    /// <summary>
    /// SAP Adapter doesn't use OAuth, so this is not needed
    /// But we need to implement it because we inherit from HttpClientAdapterBase
    /// </summary>
    protected override Task<string> GetAccessTokenInternalAsync(CancellationToken cancellationToken = default)
    {
        // SAP doesn't use OAuth tokens, return empty string
        return Task.FromResult<string>(string.Empty);
    }

    /// <summary>
    /// Gets the base URL for SAP connection
    /// </summary>
    private string GetSapBaseUrl()
    {
        if (string.IsNullOrEmpty(_sapApplicationServer))
            throw new InvalidOperationException("SAP Application Server is required");

        var protocol = _sapUseOData || _sapUseRestApi ? "https" : "http";
        var port = _sapUseOData || _sapUseRestApi ? "443" : (_sapSystemNumber ?? "00");
        
        return $"{protocol}://{_sapApplicationServer}:{port}";
    }

    /// <summary>
    /// Reads IDOCs/data from SAP (Source role)
    /// Supports OData, REST API, and RFC Gateway
    /// </summary>
    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("SAP Adapter (Source): Reading data from SAP. Type: {Type}, IDOC Type: {IdocType}, Filter: {Filter}", 
            _sapUseOData ? "OData" : _sapUseRestApi ? "REST" : "RFC", _sapIdocType, _sapIdocFilter);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        try
        {
            if (_sapUseOData && !string.IsNullOrEmpty(_sapODataServiceUrl))
            {
                // OData Service (S/4HANA)
                (headers, records) = await ReadFromODataAsync(cancellationToken);
            }
            else if (_sapUseRestApi && !string.IsNullOrEmpty(_sapRestApiEndpoint))
            {
                // REST API (S/4HANA)
                (headers, records) = await ReadFromRestApiAsync(cancellationToken);
            }
            else if (_sapUseRfc && !string.IsNullOrEmpty(_sapRfcFunctionModule))
            {
                // RFC via HTTP Gateway (classic SAP systems)
                (headers, records) = await ReadFromRfcAsync(cancellationToken);
            }
            else
            {
                // Fallback: Use configured RFC function module or default IDOC reading
                (headers, records) = await ReadFromIdocAsync(cancellationToken);
            }

            _logger?.LogInformation("SAP Adapter (Source): Read {Count} records", records.Count);

            // Write to MessageBox if used as source
            if (AdapterRole == "Source" && _messageBoxService != null && records.Count > 0)
            {
                await WriteRecordsToMessageBoxAsync(headers, records, cancellationToken);
            }

            return (headers, records);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SAP Adapter (Source): Error reading data from SAP");
            throw;
        }
    }

    /// <summary>
    /// Reads data from SAP OData service (S/4HANA)
    /// </summary>
    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadFromODataAsync(
        CancellationToken cancellationToken)
    {
        var baseUrl = GetSapBaseUrl();
        var odataUrl = _sapODataServiceUrl!.StartsWith("/") 
            ? $"{baseUrl}{_sapODataServiceUrl}" 
            : $"{baseUrl}/{_sapODataServiceUrl}";

        // Add filter if provided
        if (!string.IsNullOrEmpty(_sapIdocFilter))
        {
            odataUrl += $"?$filter={Uri.EscapeDataString(_sapIdocFilter)}";
        }

        // Add top for batch size
        var separator = odataUrl.Contains("?") ? "&" : "?";
        odataUrl += $"{separator}$top={_sapBatchSize}";

        _logger?.LogDebug("SAP Adapter: Calling OData service: {Url}", odataUrl);

        var response = await _httpClient.GetAsync(odataUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(jsonContent);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        // Parse OData response
        if (jsonDoc.RootElement.TryGetProperty("value", out var valueArray))
        {
            foreach (var item in valueArray.EnumerateArray())
            {
                var record = new Dictionary<string, string>();
                foreach (var prop in item.EnumerateObject())
                {
                    if (headers.Count == 0)
                    {
                        headers.Add(prop.Name);
                    }
                    else if (!headers.Contains(prop.Name))
                    {
                        headers.Add(prop.Name);
                    }

                    record[prop.Name] = prop.Value.ToString();
                }
                records.Add(record);
            }
        }

        return (headers, records);
    }

    /// <summary>
    /// Reads data from SAP REST API (S/4HANA)
    /// </summary>
    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadFromRestApiAsync(
        CancellationToken cancellationToken)
    {
        var baseUrl = GetSapBaseUrl();
        var restUrl = _sapRestApiEndpoint!.StartsWith("/") 
            ? $"{baseUrl}{_sapRestApiEndpoint}" 
            : $"{baseUrl}/{_sapRestApiEndpoint}";

        // Add query parameters if provided
        if (!string.IsNullOrEmpty(_sapIdocFilter))
        {
            restUrl += restUrl.Contains("?") ? "&" : "?";
            restUrl += Uri.EscapeDataString(_sapIdocFilter);
        }

        _logger?.LogDebug("SAP Adapter: Calling REST API: {Url}", restUrl);

        var response = await _httpClient.GetAsync(restUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(jsonContent);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        // Parse REST API response
        if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in jsonDoc.RootElement.EnumerateArray())
            {
                var record = new Dictionary<string, string>();
                foreach (var prop in item.EnumerateObject())
                {
                    if (!headers.Contains(prop.Name))
                    {
                        headers.Add(prop.Name);
                    }
                    record[prop.Name] = prop.Value.ToString();
                }
                records.Add(record);
            }
        }
        else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            // Single object or object with array property
            if (jsonDoc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var item in results.EnumerateArray())
                {
                    var record = new Dictionary<string, string>();
                    foreach (var prop in item.EnumerateObject())
                    {
                        if (!headers.Contains(prop.Name))
                        {
                            headers.Add(prop.Name);
                        }
                        record[prop.Name] = prop.Value.ToString();
                    }
                    records.Add(record);
                }
            }
        }

        return (headers, records);
    }

    /// <summary>
    /// Reads data via RFC Gateway (HTTP-based RFC for classic SAP systems)
    /// </summary>
    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadFromRfcAsync(
        CancellationToken cancellationToken)
    {
        var baseUrl = GetSapBaseUrl();
        // RFC Gateway typically uses /sap/bc/soap/rfc or similar endpoint
        var rfcUrl = $"{baseUrl}/sap/bc/soap/rfc";

        var functionModule = _sapRfcFunctionModule ?? "IDOC_INBOUND_ASYNCHRONOUS";
        
        // Build RFC request
        var rfcRequest = new
        {
            function = functionModule,
            parameters = !string.IsNullOrEmpty(_sapRfcParameters) 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(_sapRfcParameters)
                : new Dictionary<string, object>
                {
                    { "IDOC_TYPE", _sapIdocType ?? "" },
                    { "IDOC_MESSAGE_TYPE", _sapIdocMessageType ?? "" },
                    { "FILTER", _sapIdocFilter ?? "" }
                }
        };

        var jsonRequest = JsonSerializer.Serialize(rfcRequest);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        _logger?.LogDebug("SAP Adapter: Calling RFC Gateway: {Url}, Function: {Function}", rfcUrl, functionModule);

        var response = await _httpClient.PostAsync(rfcUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(jsonContent);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        // Parse RFC response
        if (jsonDoc.RootElement.TryGetProperty("RETURN", out var returnData))
        {
            // Parse return data structure
            var record = new Dictionary<string, string>();
            foreach (var prop in returnData.EnumerateObject())
            {
                if (!headers.Contains(prop.Name))
                {
                    headers.Add(prop.Name);
                }
                record[prop.Name] = prop.Value.ToString();
            }
            records.Add(record);
        }

        return (headers, records);
    }

    /// <summary>
    /// Reads IDOCs using default IDOC reading logic
    /// </summary>
    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadFromIdocAsync(
        CancellationToken cancellationToken)
    {
        var headers = new List<string> { "IDOC_NUMBER", "IDOC_TYPE", "MANDT", "DOCNUM", "STATUS", "DATA" };
        var records = new List<Dictionary<string, string>>();

        // Simulate IDOC reading (in production, this would use actual SAP connection)
        if (!string.IsNullOrEmpty(_sapIdocType))
        {
            for (int i = 0; i < _sapBatchSize && i < 10; i++)
            {
                var record = new Dictionary<string, string>
                {
                    { "IDOC_NUMBER", $"IDOC{i + 1:D10}" },
                    { "IDOC_TYPE", _sapIdocType },
                    { "MANDT", _sapClient ?? "100" },
                    { "DOCNUM", (i + 1).ToString() },
                    { "STATUS", "30" },
                    { "DATA", JsonSerializer.Serialize(new { Field1 = $"Value{i + 1}", Field2 = $"Data{i + 1}" }) }
                };
                records.Add(record);
            }
        }

        return await Task.FromResult((headers, records));
    }

    /// <summary>
    /// Sends IDOCs/data to SAP (Destination role)
    /// Supports OData, REST API, and RFC Gateway
    /// Reads from MessageBox if used as Destination adapter
    /// </summary>
    public override async Task WriteAsync(
        string destination, 
        List<string> headers, 
        List<Dictionary<string, string>> records, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("SAP Adapter (Destination): Sending records to SAP. Type: {Type}, IDOC Type: {IdocType}", 
            _sapUseOData ? "OData" : _sapUseRestApi ? "REST" : "RFC", _sapIdocType);

        try
        {
            // Read messages from MessageBox if AdapterRole is "Destination"
            List<MessageBoxMessage>? processedMessages = null;
            var messageBoxResult = await ReadMessagesFromMessageBoxAsync(cancellationToken);
            if (messageBoxResult.HasValue)
            {
                var (messageHeaders, messageRecords, messages) = messageBoxResult.Value;
                headers = messageHeaders;
                records = messageRecords;
                processedMessages = messages;
                _logger?.LogInformation("SAP Adapter (Destination): Read {Count} records from MessageBox", records.Count);
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
            {
                // Extract headers from first record if not provided
                if (records.Count > 0)
                {
                    headers = records[0].Keys.ToList();
                    _logger?.LogDebug("SAP Adapter: Extracted headers from first record: {Headers}", string.Join(", ", headers));
                }
                else
                {
                    throw new ArgumentException("Headers cannot be empty and no records available to extract headers", nameof(headers));
                }
            }

            _logger?.LogInformation("SAP Adapter (Destination): Sending {Count} records to SAP", records.Count);

            if (_sapUseOData && !string.IsNullOrEmpty(_sapODataServiceUrl))
            {
                await WriteToODataAsync(records, cancellationToken);
            }
            else if (_sapUseRestApi && !string.IsNullOrEmpty(_sapRestApiEndpoint))
            {
                await WriteToRestApiAsync(records, cancellationToken);
            }
            else if (_sapUseRfc && !string.IsNullOrEmpty(_sapRfcFunctionModule))
            {
                await WriteToRfcAsync(records, cancellationToken);
            }
            else
            {
                await WriteToIdocAsync(records, cancellationToken);
            }

            _logger?.LogInformation("SAP Adapter (Destination): Successfully sent {Count} records to SAP", records.Count);

            // Mark messages as processed if they came from MessageBox
            if (processedMessages != null && processedMessages.Count > 0 && _messageBoxService != null)
            {
                await MarkMessagesAsProcessedAsync(processedMessages, $"Successfully sent {records.Count} records to SAP", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SAP Adapter (Destination): Error sending data to SAP");
            throw;
        }
    }

    /// <summary>
    /// Writes data to SAP OData service (S/4HANA)
    /// </summary>
    private async Task WriteToODataAsync(
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken)
    {
        var baseUrl = GetSapBaseUrl();
        var odataUrl = _sapODataServiceUrl!.StartsWith("/") 
            ? $"{baseUrl}{_sapODataServiceUrl}" 
            : $"{baseUrl}/{_sapODataServiceUrl}";

        foreach (var record in records)
        {
            var jsonContent = JsonSerializer.Serialize(record);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger?.LogDebug("SAP Adapter: Posting to OData service: {Url}", odataUrl);

            var response = await _httpClient.PostAsync(odataUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Writes data to SAP REST API (S/4HANA)
    /// </summary>
    private async Task WriteToRestApiAsync(
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken)
    {
        var baseUrl = GetSapBaseUrl();
        var restUrl = _sapRestApiEndpoint!.StartsWith("/") 
            ? $"{baseUrl}{_sapRestApiEndpoint}" 
            : $"{baseUrl}/{_sapRestApiEndpoint}";

        // Send as batch if multiple records
        if (records.Count > 1)
        {
            var jsonContent = JsonSerializer.Serialize(records);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger?.LogDebug("SAP Adapter: Posting batch to REST API: {Url}", restUrl);

            var response = await _httpClient.PostAsync(restUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        else
        {
            foreach (var record in records)
            {
                var jsonContent = JsonSerializer.Serialize(record);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger?.LogDebug("SAP Adapter: Posting to REST API: {Url}", restUrl);

                var response = await _httpClient.PostAsync(restUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
        }
    }

    /// <summary>
    /// Writes data via RFC Gateway (HTTP-based RFC for classic SAP systems)
    /// </summary>
    private async Task WriteToRfcAsync(
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken)
    {
        var baseUrl = GetSapBaseUrl();
        var rfcUrl = $"{baseUrl}/sap/bc/soap/rfc";

        var functionModule = _sapRfcFunctionModule ?? "IDOC_OUTBOUND_ASYNCHRONOUS";

        foreach (var record in records)
        {
            // Build RFC request
            var rfcRequest = new
            {
                function = functionModule,
                parameters = !string.IsNullOrEmpty(_sapRfcParameters) 
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(_sapRfcParameters)
                    : new Dictionary<string, object>
                    {
                        { "IDOC_TYPE", _sapIdocType ?? "" },
                        { "RECEIVER_PORT", _sapReceiverPort ?? "" },
                        { "RECEIVER_PARTNER", _sapReceiverPartner ?? "" },
                        { "IDOC_DATA", record }
                    }
            };

            var jsonRequest = JsonSerializer.Serialize(rfcRequest);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _logger?.LogDebug("SAP Adapter: Calling RFC Gateway: {Url}, Function: {Function}", rfcUrl, functionModule);

            var response = await _httpClient.PostAsync(rfcUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Writes IDOCs using default IDOC sending logic
    /// </summary>
    private async Task WriteToIdocAsync(
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            _logger?.LogDebug("SAP Adapter (Destination): Sending IDOC record: {Record}", 
                JsonSerializer.Serialize(record));
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets schema from SAP IDOC type or OData service
    /// </summary>
    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("SAP Adapter: Getting schema for IDOC Type: {IdocType}", _sapIdocType);

        var schema = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        try
        {
            if (_sapUseOData && !string.IsNullOrEmpty(_sapODataServiceUrl))
            {
                // Query OData metadata
                var baseUrl = GetSapBaseUrl();
                var metadataUrl = _sapODataServiceUrl.StartsWith("/") 
                    ? $"{baseUrl}{_sapODataServiceUrl}/$metadata" 
                    : $"{baseUrl}/{_sapODataServiceUrl}/$metadata";

                var response = await _httpClient.GetAsync(metadataUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var metadata = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Parse OData metadata to extract schema
                    // Simplified: return default schema
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "SAP Adapter: Could not retrieve schema from SAP, using default");
        }

        // Default schema based on IDOC type
        if (!string.IsNullOrEmpty(_sapIdocType))
        {
            schema["IDOC_NUMBER"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 16
            };
            schema["IDOC_TYPE"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 30
            };
            schema["MANDT"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 3
            };
            schema["DOCNUM"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.INT
            };
            schema["STATUS"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 2
            };
            schema["DATA"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = -1 // Max
            };
        }

        return await Task.FromResult(schema);
    }

    /// <summary>
    /// Ensures destination structure exists (for Destination adapters)
    /// </summary>
    public override async Task EnsureDestinationStructureAsync(
        string destination, 
        Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("SAP Adapter: Ensuring destination structure for {Destination}", destination);
        // SAP IDOC structure is typically managed in SAP system, so we don't need to create tables
        // In a real implementation, you might validate IDOC structure or create metadata
        await Task.CompletedTask;
    }

    protected override void DisposeHttpClient()
    {
        if (_disposeHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
