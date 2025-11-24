using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net;
using System.Linq;
using ServiceBusMessage = InterfaceConfigurator.Main.Core.Interfaces.ServiceBusMessage;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// Microsoft Dynamics 365 Adapter for reading and writing data via OData Web API
/// Supports OAuth 2.0 Client Credentials Flow (recommended by Microsoft)
/// Can be used as Source (read from Dynamics 365) or Destination (write to Dynamics 365)
/// </summary>
public class Dynamics365Adapter : HttpClientAdapterBase
{
    public override string AdapterName => "Dynamics365";
    public override string AdapterAlias => "Microsoft Dynamics 365";
    public override bool SupportsRead => true;
    public override bool SupportsWrite => true;

    // Dynamics 365 Connection Properties
    private readonly string? _tenantId;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _instanceUrl;
    private readonly string? _entityName;
    private readonly string? _odataFilter;
    private readonly int _pollingInterval;
    private readonly int _dynamicsBatchSize;
    private readonly int _pageSize;
    private readonly bool _useBatch;

    public Dynamics365Adapter(
        IServiceBusService? serviceBusService = null,
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
        string? interfaceName = null,
        Guid? adapterInstanceGuid = null,
        int batchSize = 100,
        string adapterRole = "Source",
        ILogger? logger = null,
        // Dynamics 365 Properties
        string? tenantId = null,
        string? clientId = null,
        string? clientSecret = null,
        string? instanceUrl = null,
        string? entityName = null,
        string? odataFilter = null,
        int pollingInterval = 60,
        int adapterBatchSize = 100,
        int pageSize = 50,
        bool useBatch = true,
        HttpClient? httpClient = null) // Optional HttpClient for testing
        : base(serviceBusService, messageBoxService, subscriptionService, interfaceName, adapterInstanceGuid, adapterBatchSize, adapterRole, logger, httpClient)
    {
        _tenantId = tenantId;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _instanceUrl = instanceUrl;
        _entityName = entityName;
        _odataFilter = odataFilter;
        _pollingInterval = pollingInterval;
        _dynamicsBatchSize = adapterBatchSize;
        _pageSize = pageSize;
        _useBatch = useBatch;
    }

    /// <summary>
    /// Gets OAuth 2.0 access token using Client Credentials Flow (Microsoft recommended)
    /// Implements GetAccessTokenInternalAsync from HttpClientAdapterBase
    /// </summary>
    protected override async Task<string> GetAccessTokenInternalAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tenantId) || string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
        {
            throw new InvalidOperationException("Dynamics 365 authentication requires TenantId, ClientId, and ClientSecret");
        }

        var tokenUrl = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";
        
        var scope = "https://graph.microsoft.com/.default";
        // For Dynamics 365, use the instance URL scope
        if (!string.IsNullOrEmpty(_instanceUrl))
        {
            scope = $"{_instanceUrl.TrimEnd('/')}/.default";
        }
        
        var requestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("scope", scope),
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        _logger?.LogDebug("Dynamics 365 Adapter: Requesting OAuth token from {TokenUrl}", tokenUrl);

        var response = await _httpClient.PostAsync(tokenUrl, requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(jsonContent);

        var accessToken = tokenResponse.GetProperty("access_token").GetString();
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();
        
        SetAccessToken(accessToken!, expiresIn);

        _logger?.LogDebug("Dynamics 365 Adapter: OAuth token obtained, expires in {ExpiresIn} seconds", expiresIn);

        return accessToken!;
    }

    /// <summary>
    /// Reads data from Dynamics 365 (Source role) via OData Web API
    /// </summary>
    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Dynamics 365 Adapter (Source): Reading from entity '{EntityName}'. Filter: {Filter}", 
            _entityName, _odataFilter);

        if (string.IsNullOrEmpty(_instanceUrl) || string.IsNullOrEmpty(_entityName))
        {
            throw new InvalidOperationException("Dynamics 365 InstanceUrl and EntityName are required");
        }

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        try
        {
            // Get OAuth token (uses caching from base class)
            var accessToken = await GetAccessTokenAsync(cancellationToken);

            // Build OData query URL
            var baseUrl = _instanceUrl.TrimEnd('/');
            var entityUrl = $"{baseUrl}/api/data/v9.2/{_entityName}";

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(_odataFilter))
            {
                queryParams.Add($"$filter={Uri.EscapeDataString(_odataFilter)}");
            }
            queryParams.Add($"$top={_pageSize}");
            queryParams.Add("$select=*"); // Get all fields

            if (queryParams.Count > 0)
            {
                entityUrl += "?" + string.Join("&", queryParams);
            }

            _logger?.LogDebug("Dynamics 365 Adapter: Calling OData API: {Url}", entityUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, entityUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("OData-MaxVersion", "4.0");
            request.Headers.Add("OData-Version", "4.0");
            request.Headers.Add("Prefer", "odata.include-annotations=\"*\"");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(jsonContent);

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

                        // Handle different value types
                        var value = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => "",
                            JsonValueKind.Object => prop.Value.GetRawText(),
                            JsonValueKind.Array => prop.Value.GetRawText(),
                            _ => prop.Value.GetRawText()
                        };
                        record[prop.Name] = value;
                    }
                    records.Add(record);
                }

                // Handle paging with @odata.nextLink
                if (jsonDoc.RootElement.TryGetProperty("@odata.nextLink", out var nextLink))
                {
                    _logger?.LogDebug("Dynamics 365 Adapter: More records available via nextLink");
                    // In production, you might want to fetch all pages
                }
            }

            _logger?.LogInformation("Dynamics 365 Adapter (Source): Read {Count} records", records.Count);

            // Write to MessageBox if used as source
            if (AdapterRole == "Source" && _messageBoxService != null && records.Count > 0)
            {
                await WriteRecordsToMessageBoxAsync(headers, records, cancellationToken);
            }

            return (headers, records);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Dynamics 365 Adapter (Source): Error reading from Dynamics 365");
            throw;
        }
    }

    /// <summary>
    /// Writes data to Dynamics 365 (Destination role) via OData Web API
    /// Supports batch requests for better performance
    /// Reads from MessageBox if used as Destination adapter
    /// </summary>
    public override async Task WriteAsync(
        string destination, 
        List<string> headers, 
        List<Dictionary<string, string>> records, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Dynamics 365 Adapter (Destination): Writing records to entity '{EntityName}'", _entityName);

        if (string.IsNullOrEmpty(_instanceUrl) || string.IsNullOrEmpty(_entityName))
        {
            throw new InvalidOperationException("Dynamics 365 InstanceUrl and EntityName are required");
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
                _logger?.LogInformation("Dynamics 365 Adapter (Destination): Read {Count} records from Service Bus", records.Count);
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
                    _logger?.LogDebug("Dynamics 365 Adapter: Extracted headers from first record: {Headers}", string.Join(", ", headers));
                }
                else
                {
                    throw new ArgumentException("Headers cannot be empty and no records available to extract headers", nameof(headers));
                }
            }

            _logger?.LogInformation("Dynamics 365 Adapter (Destination): Writing {Count} records to entity '{EntityName}'", records.Count, _entityName);

            // Get OAuth token (uses caching from base class)
            var accessToken = await GetAccessTokenAsync(cancellationToken);

            var baseUrl = _instanceUrl.TrimEnd('/');
            var entityUrl = $"{baseUrl}/api/data/v9.2/{_entityName}";

            if (_useBatch && records.Count > 1)
            {
                // Use OData batch request for multiple records
                await WriteBatchAsync(entityUrl, accessToken, records, cancellationToken);
            }
            else
            {
                // Write records individually
                foreach (var record in records)
                {
                    await WriteSingleRecordAsync(entityUrl, accessToken, record, cancellationToken);
                }
            }

            _logger?.LogInformation("Dynamics 365 Adapter (Destination): Successfully wrote {Count} records", records.Count);

            // Mark messages as processed if they came from MessageBox
            if (processedMessages != null && processedMessages.Count > 0 && _messageBoxService != null)
            {
                await MarkMessagesAsProcessedAsync(processedMessages, $"Successfully wrote {records.Count} records to Dynamics 365", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Dynamics 365 Adapter (Destination): Error writing to Dynamics 365");
            throw;
        }
    }

    /// <summary>
    /// Writes a single record to Dynamics 365
    /// </summary>
    private async Task WriteSingleRecordAsync(string entityUrl, string accessToken, Dictionary<string, string> record, CancellationToken cancellationToken)
    {
        var jsonContent = JsonSerializer.Serialize(record);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, entityUrl)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");
        request.Headers.Add("Prefer", "return=representation");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Writes multiple records using OData batch request
    /// </summary>
    private async Task WriteBatchAsync(string entityUrl, string accessToken, List<Dictionary<string, string>> records, CancellationToken cancellationToken)
    {
        var baseUrl = entityUrl.Substring(0, entityUrl.IndexOf("/api/"));
        var batchUrl = $"{baseUrl}/api/data/v9.2/$batch";

        // Build batch request body
        var batchId = Guid.NewGuid().ToString();
        var changesetId = Guid.NewGuid().ToString();
        
        var batchContent = new StringBuilder();
        batchContent.AppendLine($"--batch_{batchId}");
        batchContent.AppendLine("Content-Type: multipart/mixed; boundary=changeset_" + changesetId);
        batchContent.AppendLine();

        foreach (var record in records)
        {
            var jsonContent = JsonSerializer.Serialize(record);
            batchContent.AppendLine($"--changeset_{changesetId}");
            batchContent.AppendLine("Content-Type: application/http");
            batchContent.AppendLine("Content-Transfer-Encoding: binary");
            batchContent.AppendLine();
            batchContent.AppendLine($"POST {entityUrl} HTTP/1.1");
            batchContent.AppendLine("Content-Type: application/json");
            batchContent.AppendLine();
            batchContent.AppendLine(jsonContent);
            batchContent.AppendLine();
        }

        batchContent.AppendLine($"--changeset_{changesetId}--");
        batchContent.AppendLine($"--batch_{batchId}--");

        var content = new StringContent(batchContent.ToString(), Encoding.UTF8, $"multipart/mixed; boundary=batch_{batchId}");

        var request = new HttpRequestMessage(HttpMethod.Post, batchUrl)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gets schema from Dynamics 365 entity metadata
    /// </summary>
    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Dynamics 365 Adapter: Getting schema for entity '{EntityName}'", _entityName);

        var schema = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        try
        {
            if (string.IsNullOrEmpty(_instanceUrl) || string.IsNullOrEmpty(_entityName))
            {
                return schema;
            }

            var accessToken = await GetAccessTokenAsync(cancellationToken);
            var baseUrl = _instanceUrl.TrimEnd('/');
            var metadataUrl = $"{baseUrl}/api/data/v9.2/EntityDefinitions(LogicalName='{_entityName}')/Attributes";

            var request = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("OData-MaxVersion", "4.0");
            request.Headers.Add("OData-Version", "4.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
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

                            // Map Dynamics 365 attribute types to SQL types
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
            _logger?.LogWarning(ex, "Dynamics 365 Adapter: Could not retrieve schema from Dynamics 365, using default");
        }

        // Fallback to default schema if metadata query failed
        if (schema.Count == 0 && !string.IsNullOrEmpty(_entityName))
        {
            schema["accountid"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 36
            };
            schema["name"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 160
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
        _logger?.LogInformation("Dynamics 365 Adapter: Ensuring destination structure for entity {EntityName}", _entityName);
        // Dynamics 365 entity structure is managed in Dynamics 365, so we don't need to create it
        // In a real implementation, you might validate entity metadata or create custom fields
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
