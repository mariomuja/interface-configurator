using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Data;
using System.Collections.Concurrent;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Schema Registry for managing data schemas per interface
/// Supports schema versioning and evolution
/// </summary>
public class SchemaRegistryService
{
    private readonly InterfaceConfigDbContext? _context;
    private readonly ILogger<SchemaRegistryService>? _logger;
    
    // In-memory cache for schemas
    private readonly ConcurrentDictionary<string, Schema> _schemaCache = new();

    public SchemaRegistryService(
        InterfaceConfigDbContext? context,
        ILogger<SchemaRegistryService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get schema for an interface (latest version by default)
    /// </summary>
    public async Task<Schema?> GetSchemaAsync(
        string interfaceName,
        string version = "latest",
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{interfaceName}:{version}";
        
        if (_schemaCache.TryGetValue(cacheKey, out var cachedSchema))
        {
            return cachedSchema;
        }

        if (_context == null)
        {
            return null;
        }

        try
        {
            // Store schemas in ProcessLogs table with special format
            // In production, you might want a dedicated Schemas table
            // Use raw SQL since ProcessLog model is in azure-functions project
            var schemaJson = await _context.Database
                .SqlQueryRaw<string>(
                    "SELECT TOP 1 Details FROM ProcessLogs WHERE Component = {0} AND (InterfaceName = {1} OR InterfaceName IS NULL) ORDER BY datetime_created DESC",
                    "SchemaRegistry", interfaceName)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(schemaJson))
            {
                var schema = JsonSerializer.Deserialize<Schema>(schemaJson);
                if (schema != null)
                {
                    _schemaCache.TryAdd(cacheKey, schema);
                    return schema;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error retrieving schema for {InterfaceName}", interfaceName);
        }

        return null;
    }

    /// <summary>
    /// Register or update schema for an interface
    /// </summary>
    public async Task RegisterSchemaAsync(
        string interfaceName,
        Schema schema,
        CancellationToken cancellationToken = default)
    {
        if (_context == null)
        {
            _logger?.LogWarning("SchemaRegistry: Database context not available. Cannot persist schema.");
            return;
        }

        try
        {
            var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
            
            // Store in ProcessLogs table
            // Note: Using dynamic object since ProcessLog is in azure-functions project
            // In production, create a dedicated Schemas table
            var logEntry = new
            {
                datetime_created = DateTime.UtcNow,
                Level = "Info",
                Message = $"Schema registered: {interfaceName}",
                Details = schemaJson,
                Component = "SchemaRegistry",
                InterfaceName = interfaceName
            };
            
            // Use raw SQL to insert since we can't reference ProcessLog model here
            await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO ProcessLogs (datetime_created, Level, Message, Details, Component, InterfaceName) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                logEntry.datetime_created, logEntry.Level, logEntry.Message, logEntry.Details, logEntry.Component, logEntry.InterfaceName);

            // Update cache
            var cacheKey = $"{interfaceName}:{schema.Version}";
            _schemaCache.AddOrUpdate(cacheKey, schema, (key, old) => schema);

            _logger?.LogInformation("Schema registered for {InterfaceName}, Version={Version}", interfaceName, schema.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error registering schema for {InterfaceName}", interfaceName);
            throw;
        }
    }

    /// <summary>
    /// Validate record against schema
    /// </summary>
    public async Task<ValidationResult> ValidateRecordAsync(
        Dictionary<string, string> record,
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        var schema = await GetSchemaAsync(interfaceName, cancellationToken: cancellationToken);
        
        if (schema == null)
        {
            // No schema registered - allow all records
            return new ValidationResult { IsValid = true };
        }

        var errors = new List<string>();

        // Check required fields
        foreach (var requiredField in schema.RequiredFields)
        {
            if (!record.ContainsKey(requiredField) || string.IsNullOrWhiteSpace(record[requiredField]))
            {
                errors.Add($"Required field '{requiredField}' is missing or empty");
            }
        }

        // Check field types
        foreach (var field in schema.Fields)
        {
            if (record.TryGetValue(field.Name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                if (!ValidateFieldType(value, field.Type))
                {
                    errors.Add($"Field '{field.Name}' has invalid type. Expected: {field.Type}, Got: {InferType(value)}");
                }
            }
        }

        // Check field constraints
        foreach (var constraint in schema.Constraints)
        {
            if (record.TryGetValue(constraint.FieldName, out var value))
            {
                if (!ValidateConstraint(value, constraint))
                {
                    errors.Add($"Field '{constraint.FieldName}' violates constraint: {constraint.Description}");
                }
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private bool ValidateFieldType(string value, string expectedType)
    {
        return expectedType.ToLowerInvariant() switch
        {
            "string" => true, // All values are strings in our system
            "int" or "integer" => int.TryParse(value, out _),
            "decimal" or "double" or "float" => double.TryParse(value, out _),
            "bool" or "boolean" => bool.TryParse(value, out _),
            "date" or "datetime" => DateTime.TryParse(value, out _),
            _ => true // Unknown type - allow
        };
    }

    private string InferType(string value)
    {
        if (int.TryParse(value, out _)) return "int";
        if (double.TryParse(value, out _)) return "decimal";
        if (bool.TryParse(value, out _)) return "bool";
        if (DateTime.TryParse(value, out _)) return "datetime";
        return "string";
    }

    private bool ValidateConstraint(string value, FieldConstraint constraint)
    {
        switch (constraint.Type.ToLowerInvariant())
        {
            case "minlength":
                return value.Length >= (int)constraint.Value;
            case "maxlength":
                return value.Length <= (int)constraint.Value;
            case "pattern":
                if (constraint.Value is string pattern)
                {
                    return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
                }
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Check schema compatibility between two versions
    /// </summary>
    public async Task<CompatibilityResult> CheckCompatibilityAsync(
        string interfaceName,
        string oldVersion,
        string newVersion,
        CancellationToken cancellationToken = default)
    {
        var oldSchema = await GetSchemaAsync(interfaceName, oldVersion, cancellationToken);
        var newSchema = await GetSchemaAsync(interfaceName, newVersion, cancellationToken);

        if (oldSchema == null || newSchema == null)
        {
            return new CompatibilityResult
            {
                IsCompatible = false,
                Message = "One or both schemas not found"
            };
        }

        var issues = new List<string>();

        // Check for removed required fields
        foreach (var oldRequired in oldSchema.RequiredFields)
        {
            if (!newSchema.RequiredFields.Contains(oldRequired) && 
                !newSchema.Fields.Any(f => f.Name == oldRequired))
            {
                issues.Add($"Required field '{oldRequired}' was removed");
            }
        }

        // Check for type changes
        foreach (var oldField in oldSchema.Fields)
        {
            var newField = newSchema.Fields.FirstOrDefault(f => f.Name == oldField.Name);
            if (newField != null && oldField.Type != newField.Type)
            {
                issues.Add($"Field '{oldField.Name}' type changed from {oldField.Type} to {newField.Type}");
            }
        }

        return new CompatibilityResult
        {
            IsCompatible = issues.Count == 0,
            Issues = issues
        };
    }
}

/// <summary>
/// Schema definition for an interface
/// </summary>
public class Schema
{
    public string InterfaceName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SchemaField> Fields { get; set; } = new();
    public List<string> RequiredFields { get; set; } = new();
    public List<FieldConstraint> Constraints { get; set; } = new();
}

public class SchemaField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string, int, decimal, bool, date
    public bool IsNullable { get; set; } = true;
    public string? Description { get; set; }
}

public class FieldConstraint
{
    public string FieldName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // minLength, maxLength, pattern, etc.
    public object Value { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class CompatibilityResult
{
    public bool IsCompatible { get; set; }
    public List<string> Issues { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
