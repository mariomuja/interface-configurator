using System.Globalization;

namespace ProcessCsvBlobTrigger.Core.Services;

/// <summary>
/// Analyzes CSV columns to determine appropriate SQL data types
/// </summary>
public class CsvColumnAnalyzer
{
    public enum SqlDataType
    {
        NVARCHAR,      // String/text data
        INT,           // Integer numbers
        DECIMAL,       // Decimal numbers with precision
        DATETIME2,     // Date/time values
        BIT,           // Boolean (true/false, yes/no, 1/0)
        UNIQUEIDENTIFIER // GUID
    }

    public class ColumnTypeInfo
    {
        public SqlDataType DataType { get; set; }
        public int? MaxLength { get; set; } // For NVARCHAR
        public int? Precision { get; set; } // For DECIMAL
        public int? Scale { get; set; } // For DECIMAL
        public string SqlTypeDefinition { get; set; } = string.Empty;
    }

    /// <summary>
    /// Analyzes a column's values to determine the best SQL data type
    /// </summary>
    public ColumnTypeInfo AnalyzeColumn(string columnName, List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            // Default to NVARCHAR if no data
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.NVARCHAR,
                MaxLength = 255,
                SqlTypeDefinition = "NVARCHAR(255)"
            };
        }

        // Remove empty/null values for analysis
        var nonEmptyValues = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        if (nonEmptyValues.Count == 0)
        {
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.NVARCHAR,
                MaxLength = 255,
                SqlTypeDefinition = "NVARCHAR(255)"
            };
        }

        // Check for GUID format
        if (IsGuidColumn(nonEmptyValues))
        {
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.UNIQUEIDENTIFIER,
                SqlTypeDefinition = "UNIQUEIDENTIFIER"
            };
        }

        // Check for boolean
        if (IsBooleanColumn(nonEmptyValues))
        {
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.BIT,
                SqlTypeDefinition = "BIT"
            };
        }

        // Check for datetime
        if (IsDateTimeColumn(nonEmptyValues))
        {
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.DATETIME2,
                SqlTypeDefinition = "DATETIME2"
            };
        }

        // Check for integer
        if (IsIntegerColumn(nonEmptyValues))
        {
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.INT,
                SqlTypeDefinition = "INT"
            };
        }

        // Check for decimal
        if (IsDecimalColumn(nonEmptyValues))
        {
            var (precision, scale) = CalculateDecimalPrecision(nonEmptyValues);
            return new ColumnTypeInfo
            {
                DataType = SqlDataType.DECIMAL,
                Precision = precision,
                Scale = scale,
                SqlTypeDefinition = $"DECIMAL({precision},{scale})"
            };
        }

        // Default to NVARCHAR with calculated max length
        var maxLength = CalculateMaxStringLength(nonEmptyValues);
        return new ColumnTypeInfo
        {
            DataType = SqlDataType.NVARCHAR,
            MaxLength = maxLength,
            SqlTypeDefinition = maxLength > 4000 ? "NVARCHAR(MAX)" : $"NVARCHAR({maxLength})"
        };
    }

    private bool IsGuidColumn(List<string> values)
    {
        // Check if all non-empty values are valid GUIDs
        return values.All(v => Guid.TryParse(v, out _));
    }

    private bool IsBooleanColumn(List<string> values)
    {
        var booleanPatterns = new[] { "true", "false", "yes", "no", "1", "0", "y", "n" };
        return values.All(v => booleanPatterns.Contains(v.Trim().ToLowerInvariant()));
    }

    private bool IsDateTimeColumn(List<string> values)
    {
        // Try common datetime formats
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

        return values.All(v =>
        {
            if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return true;

            return formats.Any(f => DateTime.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
        });
    }

    private bool IsIntegerColumn(List<string> values)
    {
        return values.All(v => long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
    }

    private bool IsDecimalColumn(List<string> values)
    {
        return values.All(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _));
    }

    private (int precision, int scale) CalculateDecimalPrecision(List<string> values)
    {
        int maxPrecision = 18; // Default SQL Server DECIMAL precision
        int maxScale = 2; // Default scale

        foreach (var value in values)
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            {
                var parts = value.Split('.');
                var integerPart = parts[0].Replace("-", "").Length;
                var decimalPart = parts.Length > 1 ? parts[1].Length : 0;

                maxPrecision = Math.Max(maxPrecision, integerPart + decimalPart);
                maxScale = Math.Max(maxScale, decimalPart);
            }
        }

        // SQL Server DECIMAL max precision is 38
        maxPrecision = Math.Min(maxPrecision, 38);
        maxScale = Math.Min(maxScale, maxPrecision);

        return (maxPrecision, maxScale);
    }

    private int CalculateMaxStringLength(List<string> values)
    {
        var maxLength = values.Max(v => v?.Length ?? 0);
        
        // Round up to nearest 50 for efficiency, but cap at reasonable values
        if (maxLength <= 50) return 50;
        if (maxLength <= 100) return 100;
        if (maxLength <= 255) return 255;
        if (maxLength <= 500) return 500;
        if (maxLength <= 1000) return 1000;
        if (maxLength <= 4000) return 4000;
        
        // Use MAX for very long strings
        return -1; // -1 indicates MAX
    }
}






