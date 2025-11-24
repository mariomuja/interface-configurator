using System.Text.Json;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for validating adapter configurations against JSON schema
/// </summary>
public interface IConfigurationValidationService
{
    /// <summary>
    /// Validates a configuration object against the schema
    /// </summary>
    ValidationResult ValidateConfiguration(object configuration, string adapterName, string adapterType);

    /// <summary>
    /// Validates a JSON string against the schema
    /// </summary>
    ValidationResult ValidateConfigurationJson(string jsonConfiguration, string adapterName, string adapterType);

    /// <summary>
    /// Gets the current schema version
    /// </summary>
    string GetSchemaVersion();

    /// <summary>
    /// Checks if a configuration schema version is compatible
    /// </summary>
    bool IsSchemaVersionCompatible(string schemaVersion);
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? SchemaVersion { get; set; }
}

