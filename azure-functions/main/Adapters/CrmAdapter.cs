using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net;
using System.Xml.Linq;
using System.Linq;
using ServiceBusMessage = InterfaceConfigurator.Main.Core.Interfaces.ServiceBusMessage;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// Microsoft CRM Adapter for reading and writing data via Web API
/// Supports OAuth 2.0 and FetchXML queries (Microsoft recommended)
/// Can be used as Source (read from CRM) or Destination (write to CRM)
/// </summary>
public class CrmAdapter : HttpClientAdapterBase
{
    public override string AdapterName => "CRM";
    public override string AdapterAlias => "Microsoft CRM";
    public override bool SupportsRead => true;
    public override bool SupportsWrite => true;

    // CRM Connection Properties
    private readonly string? _organizationUrl;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string? _entityName;
    private readonly string? _fetchXml;
    private readonly int _pollingInterval;
    private readonly int _crmBatchSize;
    private readonly bool _useBatch;

    public CrmAdapter(
        IServiceBusService? serviceBusService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        int batchSize = 100,
        string adapterRole = "Source",
        ILogger? logger = null,
        // CRM Properties
        string? organizationUrl = null,
        string? username = null,
        string? password = null,
        string? entityName = null,
        string? fetchXml = null,
        int pollingInterval = 60,
        int adapterBatchSize = 100,
        bool useBatch = true,
        HttpClient? httpClient = null) // Optional HttpClient for testing
        : base(serviceBusService, interfaceName, adapterInstanceGuid, adapterBatchSize, adapterRole, logger, null, null, httpClient)
    {
        _organizationUrl = organizationUrl;
        _username = username;
        _password = password;
        _entityName = entityName;
        _fetchXml = fetchXml;
        _pollingInterval = pollingInterval;
        _crmBatchSize = adapterBatchSize;
        _useBatch = useBatch;
    }

    /// <summary>
    /// Gets authentication token for CRM
    /// Supports OAuth 2.0 (preferred) or Basic Auth (legacy)
    /// Implements GetAccessTokenInternalAsync from HttpClientAdapterBase
    /// </summary>
    protected override async Task<string> GetAccessTokenInternalAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_organizationUrl))
        {
            throw new InvalidOperationException("CRM OrganizationUrl is required");
        }

        // Try OAuth 2.0 first (if client credentials are provided)
        // For simplicity, we'll use Basic Auth if username/password are provided
        // In production, you should use OAuth 2.0 with Azure AD

        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            // Basic Auth (legacy, but still supported)
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            SetAccessToken(authValue, DateTime.UtcNow.AddHours(1)); // Basic auth doesn't expire, but we'll refresh after 1 hour
            return authValue;
        }

        throw new InvalidOperationException("CRM authentication requires Username and Password, or OAuth credentials");
    }

    /// <summary>
    /// Reads data from Microsoft CRM (Source role) via Web API
    /// Supports FetchXML queries (recommended) or OData queries
    /// </summary>
    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CRM Adapter (Source): Reading from entity '{EntityName}'. FetchXML: {FetchXml}", 
            _entityName, _fetchXml);

        if (string.IsNullOrEmpty(_organizationUrl) || string.IsNullOrEmpty(_entityName))
        {
            throw new InvalidOperationException("CRM OrganizationUrl and EntityName are required");
        }

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        try
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);

            var baseUrl = _organizationUrl.TrimEnd('/');
            string apiUrl;

            if (!string.IsNullOrEmpty(_fetchXml))
            {
                // Use FetchXML query (recommended for complex queries)
                apiUrl = $"{baseUrl}/api/data/v9.2/RetrieveMultiple";
                
                var fetchRequest = new
                {
                    query = new
                    {
                        fetchXml = _fetchXml
                    }
                };

                var jsonContent = JsonSerializer.Serialize(fetchRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", accessToken);
                request.Headers.Add("OData-MaxVersion", "4.0");
                request.Headers.Add("OData-Version", "4.0");
                request.Headers.Add("Prefer", "odata.include-annotations=\"*\"");

                _logger?.LogDebug("CRM Adapter: Calling FetchXML API: {Url}", apiUrl);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);

                // Parse FetchXML response
                if (jsonDoc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    foreach (var item in valueArray.EnumerateArray())
                    {
                        var record = new Dictionary<string, string>();
                        foreach (var prop in item.EnumerateObject())
                        {
                            if (!headers.Contains(prop.Name))
                            {
                                headers.Add(prop.Name);
                            }

                            var value = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString() ?? "",
                                JsonValueKind.Number => prop.Value.GetRawText(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                JsonValueKind.Null => "",
                                _ => prop.Value.GetRawText()
                            };
                            record[prop.Name] = value;
                        }
                        records.Add(record);
                    }
                }
            }
            else
            {
                // Use OData query
                apiUrl = $"{baseUrl}/api/data/v9.2/{_entityName}";
                apiUrl += $"?$top={_crmBatchSize}";
                apiUrl += "&$select=*";

                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", accessToken);
                request.Headers.Add("OData-MaxVersion", "4.0");
                request.Headers.Add("OData-Version", "4.0");
                request.Headers.Add("Prefer", "odata.include-annotations=\"*\"");

                _logger?.LogDebug("CRM Adapter: Calling OData API: {Url}", apiUrl);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(jsonContent);

                // Parse OData response
                if (jsonDoc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    foreach (var item in valueArray.EnumerateArray())
                    {
                        var record = new Dictionary<string, string>();
                        foreach (var prop in item.EnumerateObject())
                        {
                            if (!headers.Contains(prop.Name))
                            {
                                headers.Add(prop.Name);
                            }

                            var value = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString() ?? "",
                                JsonValueKind.Number => prop.Value.GetRawText(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                JsonValueKind.Null => "",
                                _ => prop.Value.GetRawText()
                            };
                            record[prop.Name] = value;
                        }
                        records.Add(record);
                    }
                }
            }

            _logger?.LogInformation("CRM Adapter (Source): Read {Count} records", records.Count);

            // Write to Service Bus if used as source (with debatching - one message per record)
            if (AdapterRole == "Source" && records.Count > 0)
            {
                LogProcessingState("CrmAdapter.ReadAsync", "SendingToServiceBus", 
                    $"Sending {records.Count} CRM records to Service Bus (debatching to individual messages)");
                
                var messagesSent = await WriteRecordsToServiceBusWithDebatchingAsync(headers, records, cancellationToken);
                
                LogProcessingState("CrmAdapter.ReadAsync", "ServiceBusSent", 
                    $"Successfully sent {messagesSent} messages to Service Bus for {records.Count} CRM records");
            }

            return (headers, records);
        }
        catch (Exception ex)
        {
            LogProcessingState("CrmAdapter.ReadAsync", "Error", 
                "Failed to read and process data from Microsoft CRM", ex);
            throw;
        }
    }

    /// <summary>
    /// Processes data from Microsoft CRM directly within the container app
    /// Reads entity records, debatches to single records, sends to Service Bus
    /// This method ensures all work is done in the container app, not via blob triggers
    /// </summary>
    public async Task ProcessDataFromCrmAsync(CancellationToken cancellationToken = default)
    {
        if (AdapterRole != "Source")
        {
            LogProcessingState("ProcessDataFromCrm", "Skipped", $"AdapterRole is {AdapterRole}, not Source");
            return;
        }

        if (_serviceBusService == null || string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        {
            LogProcessingState("ProcessDataFromCrm", "Error", 
                $"ServiceBusService={_serviceBusService != null}, InterfaceName={_interfaceName ?? "NULL"}, AdapterInstanceGuid={_adapterInstanceGuid.HasValue}");
            return;
        }

        if (string.IsNullOrEmpty(_organizationUrl) || string.IsNullOrEmpty(_entityName))
        {
            LogProcessingState("ProcessDataFromCrm", "Error", 
                "OrganizationUrl or EntityName is not configured");
            throw new InvalidOperationException("CRM OrganizationUrl and EntityName are required");
        }

        try
        {
            LogProcessingState("ProcessDataFromCrm", "Starting", 
                $"Interface: {_interfaceName}, AdapterInstanceGuid: {_adapterInstanceGuid.Value}, Entity: {_entityName}");

            // Read data from Microsoft CRM
            var (headers, records) = await ReadAsync(string.Empty, cancellationToken);

            if (records == null || records.Count == 0)
            {
                LogProcessingState("ProcessDataFromCrm", "NoRecords", 
                    $"No records found in CRM entity {_entityName}");
                return;
            }

            LogProcessingState("ProcessDataFromCrm", "DataRead", 
                $"Read {records.Count} records from CRM entity {_entityName}, {headers.Count} headers");

            // Debatch and send to Service Bus (one message per record)
            LogProcessingState("ProcessDataFromCrm", "Debatching", 
                $"Debatching {records.Count} CRM records to individual Service Bus messages");
            
            var messagesSent = await WriteRecordsToServiceBusWithDebatchingAsync(headers, records, cancellationToken);
            
            LogProcessingState("ProcessDataFromCrm", "Completed", 
                $"Successfully processed {records.Count} CRM records, sent {messagesSent} messages to Service Bus");
        }
        catch (Exception ex)
        {
            LogProcessingState("ProcessDataFromCrm", "Error", 
                "Failed to process data from Microsoft CRM", ex);
            throw;
        }
    }

    /// <summary>
    /// Writes data to Microsoft CRM (Destination role) via Web API
    /// Supports ExecuteMultiple for batch operations (recommended by Microsoft)
    /// Reads from Service Bus if used as Destination adapter
    /// </summary>
    public override async Task WriteAsync(
        string destination, 
        List<string> headers, 
        List<Dictionary<string, string>> records, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CRM Adapter (Destination): Writing records to entity '{EntityName}'", _entityName);

        if (string.IsNullOrEmpty(_organizationUrl) || string.IsNullOrEmpty(_entityName))
        {
            throw new InvalidOperationException("CRM OrganizationUrl and EntityName are required");
        }

        try
        {
            // Read messages from Service Bus if AdapterRole is "Destination"
            List<ServiceBusMessage>? processedMessages = null;
            var serviceBusResult = await ReadMessagesFromServiceBusAsync(cancellationToken);
            if (serviceBusResult.HasValue)
            {
                var (messageHeaders, messageRecords, messages) = serviceBusResult.Value;
                headers = messageHeaders;
                records = messageRecords;
                processedMessages = messages;
                _logger?.LogInformation("CRM Adapter (Destination): Read {Count} records from Service Bus", records.Count);
            }
            else if (AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
            {
                // No messages found, but continue to fallback: use provided records if available
                _logger?.LogWarning("No messages found in Service Bus. Will use provided records if available.");
            }
            
            // If no messages were read from Service Bus, use provided records (fallback for direct calls)
            // This allows adapters to work both ways: via Service Bus (timer-based) or direct (blob trigger)
            if (records == null || records.Count == 0)
            {
                _logger?.LogInformation("No records from Service Bus and no records provided. Nothing to write.");
                return;
            }
            
            // Validate headers and records if not reading from Service Bus
            if (headers == null || headers.Count == 0)
            {
                // Extract headers from first record if not provided
                if (records.Count > 0)
                {
                    headers = records[0].Keys.ToList();
                    _logger?.LogDebug("CRM Adapter: Extracted headers from first record: {Headers}", string.Join(", ", headers));
                }
                else
                {
                    throw new ArgumentException("Headers cannot be empty and no records available to extract headers", nameof(headers));
                }
            }

            _logger?.LogInformation("CRM Adapter (Destination): Writing {Count} records to entity '{EntityName}'", records.Count, _entityName);

            var accessToken = await GetAccessTokenAsync(cancellationToken);

            var baseUrl = _organizationUrl.TrimEnd('/');
            var entityUrl = $"{baseUrl}/api/data/v9.2/{_entityName}";

            if (_useBatch && records.Count > 1)
            {
                // Use ExecuteMultiple for batch operations (recommended by Microsoft)
                await WriteExecuteMultipleAsync(baseUrl, accessToken, entityUrl, records, cancellationToken);
            }
            else
            {
                // Write records individually
                foreach (var record in records)
                {
                    await WriteSingleRecordAsync(entityUrl, accessToken, record, cancellationToken);
                }
            }

            _logger?.LogInformation("CRM Adapter (Destination): Successfully wrote {Count} records", records.Count);

            // Mark messages as processed if they came from Service Bus
            if (processedMessages != null && processedMessages.Count > 0)
            {
                await MarkMessagesAsProcessedAsync(processedMessages, $"Successfully wrote {records.Count} records to Microsoft CRM", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CRM Adapter (Destination): Error writing to Microsoft CRM");
            throw;
        }
    }

    /// <summary>
    /// Writes a single record to CRM
    /// </summary>
    private async Task WriteSingleRecordAsync(string entityUrl, string accessToken, Dictionary<string, string> record, CancellationToken cancellationToken)
    {
        var jsonContent = JsonSerializer.Serialize(record);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, entityUrl)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", accessToken);
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");
        request.Headers.Add("Prefer", "return=representation");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Writes multiple records using ExecuteMultiple (recommended by Microsoft for batch operations)
    /// </summary>
    private async Task WriteExecuteMultipleAsync(string baseUrl, string accessToken, string entityUrl, List<Dictionary<string, string>> records, CancellationToken cancellationToken)
    {
        var executeMultipleUrl = $"{baseUrl}/api/data/v9.2/ExecuteMultiple";

        var requests = new List<object>();
        foreach (var record in records)
        {
            requests.Add(new
            {
                RequestName = "Create",
                Target = record
            });
        }

        var executeRequest = new
        {
            Requests = requests,
            Settings = new
            {
                ContinueOnError = true,
                ReturnResponses = true
            }
        };

        var jsonContent = JsonSerializer.Serialize(executeRequest);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, executeMultipleUrl)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", accessToken);
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogDebug("CRM Adapter: ExecuteMultiple response: {Response}", responseContent);
    }

    /// <summary>
    /// Gets schema from Microsoft CRM entity metadata
    /// </summary>
    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CRM Adapter: Getting schema for entity '{EntityName}'", _entityName);

        var schema = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        try
        {
            if (string.IsNullOrEmpty(_organizationUrl) || string.IsNullOrEmpty(_entityName))
            {
                return schema;
            }

            var accessToken = await GetAccessTokenAsync(cancellationToken);
            var baseUrl = _organizationUrl.TrimEnd('/');
            var metadataUrl = $"{baseUrl}/api/data/v9.2/EntityDefinitions(LogicalName='{_entityName}')/Attributes";

            var request = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", accessToken);
            request.Headers.Add("OData-MaxVersion", "4.0");
            request.Headers.Add("OData-Version", "4.0");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(jsonContent);

                if (jsonDoc.RootElement.TryGetProperty("value", out var attributes))
                {
                    foreach (var attr in attributes.EnumerateArray())
                    {
                        if (attr.TryGetProperty("LogicalName", out var logicalName))
                        {
                            var attrName = logicalName.GetString();
                            if (string.IsNullOrEmpty(attrName)) continue;

                            var columnInfo = new CsvColumnAnalyzer.ColumnTypeInfo();

                            if (attr.TryGetProperty("AttributeType", out var attrType))
                            {
                                var typeName = attrType.GetString();
                                switch (typeName)
                                {
                                    case "String":
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR;
                                        if (attr.TryGetProperty("MaxLength", out var maxLength))
                                        {
                                            columnInfo.MaxLength = maxLength.GetInt32();
                                        }
                                        else
                                        {
                                            columnInfo.MaxLength = 4000;
                                        }
                                        break;
                                    case "Int32":
                                    case "Integer":
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.INT;
                                        break;
                                    case "Decimal":
                                    case "Money":
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.DECIMAL;
                                        break;
                                    case "DateTime":
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.DATETIME2;
                                        break;
                                    case "Boolean":
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.BIT;
                                        break;
                                    case "Uniqueidentifier":
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR;
                                        columnInfo.MaxLength = 36;
                                        break;
                                    default:
                                        columnInfo.DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR;
                                        columnInfo.MaxLength = 4000;
                                        break;
                                }
                            }

                            schema[attrName] = columnInfo;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "CRM Adapter: Could not retrieve schema from CRM, using default");
        }

        // Fallback to default schema
        if (schema.Count == 0 && !string.IsNullOrEmpty(_entityName))
        {
            schema["contactid"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 36
            };
            schema["firstname"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 50
            };
            schema["lastname"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 50
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
        _logger?.LogInformation("CRM Adapter: Ensuring destination structure for entity {EntityName}", _entityName);
        // CRM entity structure is managed in CRM, so we don't need to create it
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
