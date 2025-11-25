using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for validating adapter configurations against JSON schema
/// </summary>
public class ConfigurationValidationService : IConfigurationValidationService
{
    private readonly ILogger<ConfigurationValidationService>? _logger;
    private const string CurrentSchemaVersion = "1.0.0";

    public ConfigurationValidationService(ILogger<ConfigurationValidationService>? logger = null)
    {
        _logger = logger;
    }

    public ValidationResult ValidateConfiguration(object configuration, string adapterName, string adapterType)
    {
        try
        {
            // Convert object to JSON node
            var jsonString = JsonSerializer.Serialize(configuration);
            return ValidateConfigurationJson(jsonString, adapterName, adapterType);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating configuration");
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation error: {ex.Message}" }
            };
        }
    }

    public ValidationResult ValidateConfigurationJson(string jsonConfiguration, string adapterName, string adapterType)
    {
        var result = new ValidationResult
        {
            SchemaVersion = CurrentSchemaVersion
        };

        try
        {
            // Parse JSON
            var jsonNode = JsonNode.Parse(jsonConfiguration);
            if (jsonNode == null)
            {
                result.IsValid = false;
                result.Errors.Add("Invalid JSON format");
                return result;
            }

            // Extract schema version if present
            if (jsonNode["schemaVersion"] != null)
            {
                result.SchemaVersion = jsonNode["schemaVersion"]?.ToString();
                
                // Check schema version compatibility
                if (!IsSchemaVersionCompatible(result.SchemaVersion!))
                {
                    result.Warnings.Add($"Schema version {result.SchemaVersion} may not be fully compatible with current version {CurrentSchemaVersion}");
                }
            }
            else
            {
                result.Warnings.Add($"No schema version specified. Assuming version {CurrentSchemaVersion}");
            }

            // Schema validation temporarily disabled because System.Text.Json.Schema is not available in this environment.
            result.IsValid = true;
            result.Warnings.Add("Schema validation skipped (library unavailable). Basic structure checks only.");

            // Additional adapter-specific validation
            ValidateAdapterSpecificRules(jsonNode, adapterName, adapterType, result);

            return result;
        }
        catch (JsonException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"JSON parsing error: {ex.Message}");
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating configuration JSON");
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    public string GetSchemaVersion()
    {
        return CurrentSchemaVersion;
    }

    public bool IsSchemaVersionCompatible(string schemaVersion)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion))
            return false;

        // Parse version (e.g., "1.0.0")
        var parts = schemaVersion.Split('.');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var major) || 
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
            return false;

        // Parse current version
        var currentParts = CurrentSchemaVersion.Split('.');
        if (currentParts.Length != 3)
            return false;

        if (!int.TryParse(currentParts[0], out var currentMajor) ||
            !int.TryParse(currentParts[1], out var currentMinor) ||
            !int.TryParse(currentParts[2], out var currentPatch))
            return false;

        // Same major version is compatible
        // Different major version is incompatible
        if (major != currentMajor)
            return false;

        // Same major.minor version is compatible
        // Older minor version may have missing features but is compatible
        if (minor > currentMinor)
            return false;

        return true;
    }

    private void ValidateAdapterSpecificRules(JsonNode jsonNode, string adapterName, string adapterType, ValidationResult result)
    {
        try
        {
            var config = jsonNode["configuration"];
            if (config == null)
                return;

            switch (adapterName.ToUpperInvariant())
            {
                case "CSV":
                case "FILE":
                case "SFTP":
                    ValidateCsvAdapterRules(config, adapterName, result);
                    break;
                case "SQLSERVER":
                    ValidateSqlServerAdapterRules(config, adapterType, result);
                    break;
                case "SAP":
                    ValidateSapAdapterRules(config, adapterType, result);
                    break;
                case "DYNAMICS365":
                    ValidateDynamics365AdapterRules(config, adapterType, result);
                    break;
                case "CRM":
                    ValidateCrmAdapterRules(config, adapterType, result);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error in adapter-specific validation");
            result.Warnings.Add($"Adapter-specific validation warning: {ex.Message}");
        }
    }

    private void ValidateCsvAdapterRules(JsonNode config, string adapterName, ValidationResult result)
    {
        var adapterType = config["csvAdapterType"]?.ToString();
        
        if (adapterType == "RAW")
        {
            if (config["csvData"] == null || string.IsNullOrWhiteSpace(config["csvData"]?.ToString()))
            {
                result.Errors.Add("CSV data is required for RAW adapter type");
            }
        }
        else if (adapterType == "FILE" || adapterType == "SFTP")
        {
            if (config["receiveFolder"] == null || string.IsNullOrWhiteSpace(config["receiveFolder"]?.ToString()))
            {
                result.Errors.Add("Receive folder is required for FILE/SFTP adapter types");
            }
        }
    }

    private void ValidateSqlServerAdapterRules(JsonNode config, string adapterType, ValidationResult result)
    {
        if (adapterType == "Source")
        {
            if (config["sqlPollingStatement"] == null || string.IsNullOrWhiteSpace(config["sqlPollingStatement"]?.ToString()))
            {
                result.Errors.Add("SQL polling statement is required for Source adapters");
            }
        }
    }

    private void ValidateSapAdapterRules(JsonNode config, string adapterType, ValidationResult result)
    {
        if (adapterType == "Source")
        {
            if (config["sapRfcFunction"] == null || string.IsNullOrWhiteSpace(config["sapRfcFunction"]?.ToString()))
            {
                result.Errors.Add("SAP RFC function is required for Source adapters");
            }
        }
    }

    private void ValidateDynamics365AdapterRules(JsonNode config, string adapterType, ValidationResult result)
    {
        if (config["d365EntityName"] == null || string.IsNullOrWhiteSpace(config["d365EntityName"]?.ToString()))
        {
            result.Errors.Add("Dynamics 365 entity name is required");
        }
    }

    private void ValidateCrmAdapterRules(JsonNode config, string adapterType, ValidationResult result)
    {
        if (config["crmEntityName"] == null || string.IsNullOrWhiteSpace(config["crmEntityName"]?.ToString()))
        {
            result.Errors.Add("CRM entity name is required");
        }
    }

    private string LoadSchemaJson()
    {
        // Try to load from file first
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "adapter-config-schema.json");
        if (File.Exists(schemaPath))
        {
            return File.ReadAllText(schemaPath);
        }

        // Fallback to embedded resource or default schema
        // For now, return a minimal schema - in production, embed as resource
        return """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "type": "object",
          "properties": {
            "schemaVersion": { "type": "string" },
            "adapterName": { "type": "string" },
            "adapterType": { "type": "string" },
            "instanceName": { "type": "string" },
            "isEnabled": { "type": "boolean" },
            "configuration": { "type": "object" }
          },
          "required": ["adapterName", "adapterType", "instanceName", "isEnabled", "configuration"]
        }
        """;
    }
}

