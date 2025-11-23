using System.Text.RegularExpressions;

namespace InterfaceConfigurator.Main.Helpers;

/// <summary>
/// Helper class for input validation
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates interface name
    /// </summary>
    public static ValidationResult ValidateInterfaceName(string? interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return ValidationResult.Failure("Interface name is required");
        }

        if (interfaceName.Length < 3 || interfaceName.Length > 100)
        {
            return ValidationResult.Failure("Interface name must be between 3 and 100 characters");
        }

        if (!Regex.IsMatch(interfaceName, @"^[a-zA-Z0-9_-]+$"))
        {
            return ValidationResult.Failure("Interface name can only contain letters, numbers, hyphens, and underscores");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates field separator
    /// </summary>
    public static ValidationResult ValidateFieldSeparator(string? separator)
    {
        if (string.IsNullOrWhiteSpace(separator))
        {
            return ValidationResult.Failure("Field separator is required");
        }

        if (separator.Length > 10)
        {
            return ValidationResult.Failure("Field separator must be 10 characters or less");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates batch size
    /// </summary>
    public static ValidationResult ValidateBatchSize(int batchSize)
    {
        if (batchSize < 1)
        {
            return ValidationResult.Failure("Batch size must be at least 1");
        }

        if (batchSize > 10000)
        {
            return ValidationResult.Failure("Batch size cannot exceed 10,000");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates polling interval
    /// </summary>
    public static ValidationResult ValidatePollingInterval(int interval)
    {
        if (interval < 1)
        {
            return ValidationResult.Failure("Polling interval must be at least 1 second");
        }

        if (interval > 3600)
        {
            return ValidationResult.Failure("Polling interval cannot exceed 3600 seconds (1 hour)");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates file mask
    /// </summary>
    public static ValidationResult ValidateFileMask(string? fileMask)
    {
        if (string.IsNullOrWhiteSpace(fileMask))
        {
            return ValidationResult.Failure("File mask is required");
        }

        if (fileMask.Length > 100)
        {
            return ValidationResult.Failure("File mask must be 100 characters or less");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
}

