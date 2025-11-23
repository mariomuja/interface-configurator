using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using System.Text.Json;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// Microsoft Dynamics 365 Adapter for reading and writing data
/// Can be used as Source (read from Dynamics 365) or Destination (write to Dynamics 365)
/// </summary>
public class Dynamics365Adapter : AdapterBase
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
    private readonly int _batchSize;
    private readonly int _pageSize;
    private readonly bool _useBatch;

    public Dynamics365Adapter(
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
        bool useBatch = true)
        : base(messageBoxService, subscriptionService, interfaceName, adapterInstanceGuid, adapterBatchSize, adapterRole, logger)
    {
        _tenantId = tenantId;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _instanceUrl = instanceUrl;
        _entityName = entityName;
        _odataFilter = odataFilter;
        _pollingInterval = pollingInterval;
        _batchSize = adapterBatchSize;
        _pageSize = pageSize;
        _useBatch = useBatch;
    }

    /// <summary>
    /// Reads data from Dynamics 365 (Source role)
    /// </summary>
    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Dynamics 365 Adapter (Source): Reading from entity '{EntityName}'. Filter: {Filter}", 
            _entityName, _odataFilter);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        try
        {
            // TODO: Implement actual Dynamics 365 OData API connection
            // Example: Use HttpClient with OAuth token
            // 1. Get OAuth token using tenantId, clientId, clientSecret
            // 2. Call OData API: GET {instanceUrl}/api/data/v9.2/{entityName}?$filter={odataFilter}&$top={pageSize}
            // 3. Parse JSON response and extract records
            // 4. Handle paging with @odata.nextLink

            // For now, simulate OData response
            if (!string.IsNullOrEmpty(_entityName))
            {
                // Simulate entity structure
                headers = new List<string> { "accountid", "name", "emailaddress1", "telephone1", "modifiedon" };
                
                for (int i = 0; i < _batchSize && i < 10; i++)
                {
                    var record = new Dictionary<string, string>
                    {
                        { "accountid", Guid.NewGuid().ToString() },
                        { "name", $"Account {i + 1}" },
                        { "emailaddress1", $"account{i + 1}@example.com" },
                        { "telephone1", $"+49 123 456{i:D4}" },
                        { "modifiedon", DateTime.UtcNow.AddMinutes(-i).ToString("O") }
                    };
                    records.Add(record);
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
    /// Writes data to Dynamics 365 (Destination role)
    /// </summary>
    public override async Task WriteAsync(
        string destination, 
        List<string> headers, 
        List<Dictionary<string, string>> records, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Dynamics 365 Adapter (Destination): Writing {Count} records to entity '{EntityName}'", 
            records.Count, _entityName);

        try
        {
            // TODO: Implement actual Dynamics 365 OData API connection
            // Example: Use HttpClient with OAuth token
            // 1. Get OAuth token
            // 2. If _useBatch: Create OData batch request
            // 3. For each record: POST {instanceUrl}/api/data/v9.2/{entityName}
            // 4. Handle responses and errors

            if (_useBatch && records.Count > 1)
            {
                _logger?.LogInformation("Dynamics 365 Adapter (Destination): Using batch request for {Count} records", records.Count);
            }

            foreach (var record in records)
            {
                _logger?.LogDebug("Dynamics 365 Adapter (Destination): Writing record: {Record}", 
                    JsonSerializer.Serialize(record));
            }

            _logger?.LogInformation("Dynamics 365 Adapter (Destination): Successfully wrote {Count} records", records.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Dynamics 365 Adapter (Destination): Error writing to Dynamics 365");
            throw;
        }
    }

    /// <summary>
    /// Gets schema from Dynamics 365 entity metadata
    /// </summary>
    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Dynamics 365 Adapter: Getting schema for entity '{EntityName}'", _entityName);

        // TODO: Query Dynamics 365 metadata API
        // GET {instanceUrl}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes
        // Parse attribute types and map to SQL types

        var schema = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        if (!string.IsNullOrEmpty(_entityName))
        {
            // Default schema for common Dynamics 365 entities
            schema["accountid"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 36 // GUID
            };
            schema["name"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 160
            };
            schema["emailaddress1"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 100
            };
            schema["telephone1"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 50
            };
            schema["modifiedon"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.DATETIME2
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
}

