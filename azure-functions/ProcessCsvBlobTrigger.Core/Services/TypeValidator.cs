using System.Globalization;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Services;

/// <summary>
/// Validates CSV values against expected SQL data types
/// </summary>
public class TypeValidator
{
    /// <summary>
    /// Validates if a value can be converted to the expected SQL data type
    /// </summary>
    public bool ValidateValueType(string value, CsvColumnAnalyzer.SqlDataType expectedType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true; // Empty values are allowed (will be NULL)
        }

        return expectedType switch
        {
            CsvColumnAnalyzer.SqlDataType.INT => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            CsvColumnAnalyzer.SqlDataType.DECIMAL => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            CsvColumnAnalyzer.SqlDataType.DATETIME2 => IsValidDateTime(value),
            CsvColumnAnalyzer.SqlDataType.BIT => IsValidBoolean(value),
            CsvColumnAnalyzer.SqlDataType.UNIQUEIDENTIFIER => Guid.TryParse(value, out _),
            CsvColumnAnalyzer.SqlDataType.NVARCHAR => true, // Strings are always valid
            _ => true
        };
    }

    /// <summary>
    /// Converts a value to the appropriate type for SQL insertion
    /// </summary>
    public object? ConvertValue(string value, CsvColumnAnalyzer.SqlDataType targetType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return targetType switch
        {
            CsvColumnAnalyzer.SqlDataType.INT => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            CsvColumnAnalyzer.SqlDataType.DECIMAL => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture),
            CsvColumnAnalyzer.SqlDataType.DATETIME2 => ParseDateTime(value),
            CsvColumnAnalyzer.SqlDataType.BIT => ParseBoolean(value),
            CsvColumnAnalyzer.SqlDataType.UNIQUEIDENTIFIER => Guid.Parse(value),
            CsvColumnAnalyzer.SqlDataType.NVARCHAR => value,
            _ => value
        };
    }

    private bool IsValidDateTime(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return true;

        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "MM/dd/yyyy",
            "dd.MM.yyyy",
            "dd/MM/yyyy"
        };

        return formats.Any(f => DateTime.TryParseExact(value, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
    }

    private DateTime ParseDateTime(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "MM/dd/yyyy",
            "dd.MM.yyyy",
            "dd/MM/yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;
        }

        throw new FormatException($"Cannot parse '{value}' as DateTime");
    }

    private bool IsValidBoolean(string value)
    {
        var booleanPatterns = new[] { "true", "false", "yes", "no", "1", "0", "y", "n" };
        return booleanPatterns.Contains(value.Trim().ToLowerInvariant());
    }

    private bool ParseBoolean(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        return lower switch
        {
            "true" or "yes" or "1" or "y" => true,
            "false" or "no" or "0" or "n" => false,
            _ => throw new FormatException($"Cannot parse '{value}' as Boolean")
        };
    }
}

