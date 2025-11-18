using System.Globalization;
using System.Text.RegularExpressions;
using InterfaceConfigurator.Main.Core.Models;
using Microsoft.Extensions.Logging;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for applying field mappings and transformations to data
/// </summary>
public class FieldTransformationService
{
    private readonly ILogger<FieldTransformationService>? _logger;

    public FieldTransformationService(ILogger<FieldTransformationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Transform a row using field mappings
    /// </summary>
    public Dictionary<string, string> TransformRow(
        Dictionary<string, string> sourceRow,
        FieldMappingConfiguration mappingConfig,
        int rowNumber = 0)
    {
        var transformedRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var mapping in mappingConfig.Mappings)
        {
            try
            {
                var transformedValue = ApplyTransformation(sourceRow, mapping, rowNumber);
                
                if (transformedValue != null)
                {
                    transformedRow[mapping.TargetColumn] = transformedValue;
                }
                else if (mapping.IsRequired && string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    errors.Add($"Required column '{mapping.TargetColumn}' has no value and no default");
                }
                else if (!string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    transformedRow[mapping.TargetColumn] = mapping.DefaultValue;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error transforming column '{mapping.SourceColumn}' -> '{mapping.TargetColumn}': {ex.Message}";
                errors.Add(errorMsg);
                _logger?.LogWarning(ex, "Transformation error for row {RowNumber}, mapping {SourceColumn} -> {TargetColumn}", 
                    rowNumber, mapping.SourceColumn, mapping.TargetColumn);

                if (!mappingConfig.SkipFailedTransformations)
                {
                    throw new InvalidOperationException($"Transformation failed: {string.Join("; ", errors)}");
                }
            }
        }

        if (errors.Count > 0 && !mappingConfig.SkipFailedTransformations)
        {
            throw new InvalidOperationException($"Transformation errors: {string.Join("; ", errors)}");
        }

        return transformedRow;
    }

    private string? ApplyTransformation(Dictionary<string, string> sourceRow, FieldMapping mapping, int rowNumber)
    {
        // Get source value
        var sourceValue = sourceRow.TryGetValue(mapping.SourceColumn, out var val) ? val : null;

        // Check conditional logic first
        if (!string.IsNullOrEmpty(mapping.ConditionalExpression))
        {
            var conditionResult = EvaluateCondition(sourceRow, mapping.ConditionalExpression);
            if (conditionResult)
            {
                return mapping.ConditionalTrueValue ?? sourceValue;
            }
            else
            {
                return mapping.ConditionalFalseValue ?? sourceValue;
            }
        }

        // Apply transformation based on type
        return mapping.TransformationType.ToUpperInvariant() switch
        {
            "NONE" => sourceValue,
            "RENAME" => sourceValue,
            "CONCATENATE" => ApplyConcatenation(sourceRow, mapping),
            "SPLIT" => ApplySplit(sourceValue, mapping),
            "FORMAT" => ApplyFormatting(sourceValue, mapping),
            "DEFAULT" => sourceValue ?? mapping.DefaultValue,
            "LOOKUP" => ApplyLookup(sourceValue, mapping),
            _ => sourceValue
        };
    }

    private string? ApplyConcatenation(Dictionary<string, string> sourceRow, FieldMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.TransformationExpression))
        {
            return string.Join(" ", mapping.AdditionalSourceColumns.Select(col => 
                sourceRow.TryGetValue(col, out var val) ? val : string.Empty));
        }

        // Simple expression evaluation (e.g., "FirstName + ' ' + LastName")
        var result = mapping.TransformationExpression;
        foreach (var col in mapping.AdditionalSourceColumns)
        {
            var colValue = sourceRow.TryGetValue(col, out var val) ? val : string.Empty;
            result = result.Replace(col, $"\"{colValue}\"", StringComparison.OrdinalIgnoreCase);
        }

        // Simple string concatenation evaluation
        return EvaluateStringExpression(result);
    }

    private string? ApplySplit(string? sourceValue, FieldMapping mapping)
    {
        if (string.IsNullOrEmpty(sourceValue) || string.IsNullOrEmpty(mapping.TransformationExpression))
            return sourceValue;

        var separator = mapping.TransformationExpression;
        var parts = sourceValue.Split(new[] { separator }, StringSplitOptions.None);
        
        // Return first part, or use index if specified
        if (mapping.AdditionalSourceColumns.Count > 0 && int.TryParse(mapping.AdditionalSourceColumns[0], out var index))
        {
            return index < parts.Length ? parts[index] : null;
        }
        
        return parts.Length > 0 ? parts[0] : null;
    }

    private string? ApplyFormatting(string? sourceValue, FieldMapping mapping)
    {
        if (string.IsNullOrEmpty(sourceValue))
            return mapping.DefaultValue;

        if (string.IsNullOrEmpty(mapping.DataTypeConversion))
            return sourceValue;

        return mapping.DataTypeConversion.ToUpperInvariant() switch
        {
            "DATE" => FormatDate(sourceValue, mapping.FormatPattern),
            "DATETIME" => FormatDateTime(sourceValue, mapping.FormatPattern),
            "DECIMAL" => FormatDecimal(sourceValue),
            "INTEGER" => FormatInteger(sourceValue),
            "UPPERCASE" => sourceValue.ToUpperInvariant(),
            "LOWERCASE" => sourceValue.ToLowerInvariant(),
            "TRIM" => sourceValue.Trim(),
            _ => sourceValue
        };
    }

    private string? ApplyLookup(string? sourceValue, FieldMapping mapping)
    {
        // Lookup functionality would require a lookup table service
        // For now, return source value
        _logger?.LogWarning("Lookup transformation not yet implemented for {SourceColumn}", mapping.SourceColumn);
        return sourceValue;
    }

    private string? FormatDate(string? value, string? formatPattern)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return string.IsNullOrEmpty(formatPattern) 
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date.ToString(formatPattern, CultureInfo.InvariantCulture);
        }

        return value; // Return original if parsing fails
    }

    private string? FormatDateTime(string? value, string? formatPattern)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return string.IsNullOrEmpty(formatPattern)
                ? dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : dateTime.ToString(formatPattern, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private string? FormatDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Remove commas and currency symbols
        var cleaned = value.Replace(",", "").Replace("$", "").Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
        {
            return dec.ToString(CultureInfo.InvariantCulture);
        }

        return value;
    }

    private string? FormatInteger(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var cleaned = value.Replace(",", "").Trim();
        if (int.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        return value;
    }

    private bool EvaluateCondition(Dictionary<string, string> sourceRow, string expression)
    {
        // Simple condition evaluation (e.g., "Status == 'Active'")
        // For production, consider using System.Linq.Dynamic.Core
        try
        {
            var parts = expression.Split(new[] { "==", "!=", ">", "<", ">=", "<=" }, StringSplitOptions.None);
            if (parts.Length != 2)
                return false;

            var left = parts[0].Trim();
            var right = parts[1].Trim().Trim('\'', '"');

            var leftValue = sourceRow.TryGetValue(left, out var val) ? val : null;
            return leftValue?.Equals(right, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private string EvaluateStringExpression(string expression)
    {
        // Simple string expression evaluation
        // For production, consider using System.Linq.Dynamic.Core
        try
        {
            // Handle simple concatenation: "FirstName + ' ' + LastName"
            var parts = Regex.Split(expression, @"\s*\+\s*");
            var result = new List<string>();
            
            foreach (var part in parts)
            {
                var trimmed = part.Trim().Trim('"', '\'');
                result.Add(trimmed);
            }
            
            return string.Join("", result);
        }
        catch
        {
            return expression;
        }
    }
}

