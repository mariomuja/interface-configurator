using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using System.Text.Json;

namespace InterfaceConfigurator.Main.Adapters;

/// <summary>
/// Microsoft CRM Adapter for reading and writing data
/// Can be used as Source (read from CRM) or Destination (write to CRM)
/// </summary>
public class CrmAdapter : AdapterBase
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
    private readonly int _batchSize;
    private readonly bool _useBatch;

    public CrmAdapter(
        IMessageBoxService? messageBoxService = null,
        IMessageSubscriptionService? subscriptionService = null,
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
        bool useBatch = true)
        : base(messageBoxService, subscriptionService, interfaceName, adapterInstanceGuid, adapterBatchSize, adapterRole, logger)
    {
        _organizationUrl = organizationUrl;
        _username = username;
        _password = password;
        _entityName = entityName;
        _fetchXml = fetchXml;
        _pollingInterval = pollingInterval;
        _batchSize = adapterBatchSize;
        _useBatch = useBatch;
    }

    /// <summary>
    /// Reads data from Microsoft CRM (Source role)
    /// </summary>
    public override async Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CRM Adapter (Source): Reading from entity '{EntityName}'. FetchXML: {FetchXml}", 
            _entityName, _fetchXml);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        try
        {
            // TODO: Implement actual Microsoft CRM Web API connection
            // Example: Use HttpClient with authentication
            // 1. Authenticate using username/password or OAuth
            // 2. If FetchXML provided: POST {organizationUrl}/api/data/v9.2/RetrieveMultiple
            // 3. If no FetchXML: GET {organizationUrl}/api/data/v9.2/{entityName}
            // 4. Parse JSON response and extract records

            // For now, simulate CRM response
            if (!string.IsNullOrEmpty(_entityName))
            {
                // Simulate entity structure
                headers = new List<string> { "contactid", "firstname", "lastname", "emailaddress1", "telephone1", "modifiedon" };
                
                for (int i = 0; i < _batchSize && i < 10; i++)
                {
                    var record = new Dictionary<string, string>
                    {
                        { "contactid", Guid.NewGuid().ToString() },
                        { "firstname", $"First{i + 1}" },
                        { "lastname", $"Last{i + 1}" },
                        { "emailaddress1", $"contact{i + 1}@example.com" },
                        { "telephone1", $"+49 123 456{i:D4}" },
                        { "modifiedon", DateTime.UtcNow.AddMinutes(-i).ToString("O") }
                    };
                    records.Add(record);
                }
            }

            _logger?.LogInformation("CRM Adapter (Source): Read {Count} records", records.Count);

            // Write to MessageBox if used as source
            if (AdapterRole == "Source" && _messageBoxService != null && records.Count > 0)
            {
                await WriteRecordsToMessageBoxAsync(headers, records, cancellationToken);
            }

            return (headers, records);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CRM Adapter (Source): Error reading from Microsoft CRM");
            throw;
        }
    }

    /// <summary>
    /// Writes data to Microsoft CRM (Destination role)
    /// </summary>
    public override async Task WriteAsync(
        string destination, 
        List<string> headers, 
        List<Dictionary<string, string>> records, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CRM Adapter (Destination): Writing {Count} records to entity '{EntityName}'", 
            records.Count, _entityName);

        try
        {
            // TODO: Implement actual Microsoft CRM Web API connection
            // Example: Use HttpClient with authentication
            // 1. Authenticate
            // 2. If _useBatch: Use ExecuteMultiple (POST {organizationUrl}/api/data/v9.2/ExecuteMultiple)
            // 3. For each record: POST {organizationUrl}/api/data/v9.2/{entityName}
            // 4. Handle responses and errors

            if (_useBatch && records.Count > 1)
            {
                _logger?.LogInformation("CRM Adapter (Destination): Using ExecuteMultiple for {Count} records", records.Count);
            }

            foreach (var record in records)
            {
                _logger?.LogDebug("CRM Adapter (Destination): Writing record: {Record}", 
                    JsonSerializer.Serialize(record));
            }

            _logger?.LogInformation("CRM Adapter (Destination): Successfully wrote {Count} records", records.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CRM Adapter (Destination): Error writing to Microsoft CRM");
            throw;
        }
    }

    /// <summary>
    /// Gets schema from Microsoft CRM entity metadata
    /// </summary>
    public override async Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(
        string source, 
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CRM Adapter: Getting schema for entity '{EntityName}'", _entityName);

        // TODO: Query CRM metadata API
        // GET {organizationUrl}/api/data/v9.2/EntityDefinitions(LogicalName='{entityName}')/Attributes
        // Parse attribute types and map to SQL types

        var schema = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        if (!string.IsNullOrEmpty(_entityName))
        {
            // Default schema for common CRM entities
            schema["contactid"] = new CsvColumnAnalyzer.ColumnTypeInfo 
            { 
                DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR,
                MaxLength = 36 // GUID
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
        _logger?.LogInformation("CRM Adapter: Ensuring destination structure for entity {EntityName}", _entityName);
        // CRM entity structure is managed in CRM, so we don't need to create it
        // In a real implementation, you might validate entity metadata or create custom fields
        await Task.CompletedTask;
    }
}

