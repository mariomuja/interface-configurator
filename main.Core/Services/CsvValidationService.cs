using System.Text;
using Microsoft.Extensions.Logging;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for validating CSV files before processing (encoding, delimiters, structure)
/// </summary>
public class CsvValidationService
{
    private readonly ILogger? _logger;

    public CsvValidationService(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate CSV content
    /// </summary>
    public CsvValidationResult ValidateCsv(string csvContent, string? expectedDelimiter = null, string? quoteCharacter = null)
    {
        var result = new CsvValidationResult
        {
            IsValid = true,
            Issues = new List<string>()
        };

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            result.IsValid = false;
            result.Issues.Add("CSV content is empty");
            return result;
        }

        // Detect if file is binary (contains null bytes or too many non-printable characters)
        if (!IsTextFile(csvContent))
        {
            result.IsValid = false;
            result.Issues.Add("File appears to be binary or contains invalid characters. CSV files must contain only text data.");
            return result;
        }

        // Detect encoding
        result.Encoding = DetectEncoding(csvContent);
        if (result.Encoding == null)
        {
            result.IsValid = false;
            result.Issues.Add("Could not detect encoding. File may contain invalid characters.");
        }

        // Detect delimiter
        result.DetectedDelimiter = DetectDelimiter(csvContent, expectedDelimiter);
        if (string.IsNullOrEmpty(result.DetectedDelimiter))
        {
            result.IsValid = false;
            result.Issues.Add("Could not detect CSV delimiter");
        }
        else if (!string.IsNullOrEmpty(expectedDelimiter) && result.DetectedDelimiter != expectedDelimiter)
        {
            result.IsValid = false;
            result.Issues.Add($"Delimiter mismatch: Expected '{expectedDelimiter}', detected '{result.DetectedDelimiter}'");
        }

        // Validate structure
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            result.IsValid = false;
            result.Issues.Add("CSV file has no lines");
            return result;
        }

        result.LineCount = lines.Length;
        result.HasHeader = lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]);

        if (!result.HasHeader)
        {
            result.IsValid = false;
            result.Issues.Add("CSV file appears to have no header row");
        }

        // Use configured quote character or default to double quote
        var quoteChar = !string.IsNullOrEmpty(quoteCharacter) ? quoteCharacter[0] : '"';
        result.QuoteCharacter = quoteChar.ToString();

        // Check column count consistency (handling multi-line quoted values)
        if (lines.Length > 0 && !string.IsNullOrEmpty(result.DetectedDelimiter))
        {
            // Parse CSV properly handling quoted multi-line values
            var (headerColumns, _) = ParseCsvLineWithQuotes(lines[0], result.DetectedDelimiter, quoteChar);
            var headerColumnCount = headerColumns.Count;
            result.ColumnCount = headerColumnCount;

            if (headerColumnCount == 0)
            {
                result.IsValid = false;
                result.Issues.Add("Header row has no columns");
            }
            else if (headerColumnCount == 1 && !lines[0].Contains(result.DetectedDelimiter))
            {
                // Check if file is plain text without CSV structure
                var hasDelimiterInAnyLine = false;
                for (int i = 1; i < Math.Min(10, lines.Length); i++)
                {
                    if (lines[i].Contains(result.DetectedDelimiter))
                    {
                        hasDelimiterInAnyLine = true;
                        break;
                    }
                }
                
                if (!hasDelimiterInAnyLine)
                {
                    result.IsValid = false;
                    result.Issues.Add("File does not appear to be in CSV format. No delimiter found in header or data rows. File may be plain text without CSV structure.");
                }
            }

            // Check ALL rows for consistency (required: all rows must have the same column count)
            // Handle multi-line quoted values by parsing the entire CSV content properly
            var inconsistentRows = new List<int>();
            var allRows = ParseCsvRowsWithQuotes(csvContent, result.DetectedDelimiter, quoteChar);

            for (int i = 1; i < allRows.Count; i++)
            {
                if (allRows[i].Count == 0)
                    continue; // Skip empty rows

                var columnCount = allRows[i].Count;
                if (columnCount != headerColumnCount)
                {
                    inconsistentRows.Add(i + 1); // +1 because line numbers are 1-based
                }
            }

            if (inconsistentRows.Count > 0)
            {
                // Don't mark as invalid - just warn. Invalid rows will be skipped during processing.
                // Only mark as invalid if ALL rows are invalid (no valid data to process)
                var validRowsCount = allRows.Count - 1 - inconsistentRows.Count; // -1 for header
                if (validRowsCount == 0)
                {
                    result.IsValid = false;
                    var errorMessage = $"CSV validation failed: All data rows have incorrect column counts (expected {headerColumnCount} columns). ";
                    if (inconsistentRows.Count <= 20)
                    {
                        errorMessage += $"Rows with errors: {string.Join(", ", inconsistentRows)}";
                    }
                    else
                    {
                        errorMessage += $"First 20 rows with errors: {string.Join(", ", inconsistentRows.Take(20))} (and {inconsistentRows.Count - 20} more)";
                    }
                    result.Issues.Add(errorMessage);
                }
                else
                {
                    // Some rows are invalid, but there are valid rows - warn but don't fail validation
                    var warningMessage = $"CSV validation warning: {inconsistentRows.Count} row(s) will be skipped due to incorrect column counts (expected {headerColumnCount} columns). " +
                        $"{validRowsCount} valid row(s) will be processed. ";
                    if (inconsistentRows.Count <= 20)
                    {
                        warningMessage += $"Rows to be skipped: {string.Join(", ", inconsistentRows)}";
                    }
                    else
                    {
                        warningMessage += $"First 20 rows to be skipped: {string.Join(", ", inconsistentRows.Take(20))} (and {inconsistentRows.Count - 20} more)";
                    }
                    result.Issues.Add(warningMessage);
                    // Don't set IsValid = false - processing can continue with valid rows
                }
            }
        }

        // Check for common issues
        if (csvContent.Contains("\0"))
        {
            result.IsValid = false;
            result.Issues.Add("CSV file contains null characters (\\0)");
        }

        // Check for BOM
        if (csvContent.StartsWith("\uFEFF", StringComparison.Ordinal))
        {
            result.HasBom = true;
            result.Issues.Add("CSV file has UTF-8 BOM (Byte Order Mark). This is usually fine but may cause issues with some parsers.");
        }

        return result;
    }

    private string? DetectEncoding(string content)
    {
        // Try to detect UTF-8
        try
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(content);
            var roundTrip = Encoding.UTF8.GetString(utf8Bytes);
            if (roundTrip == content)
            {
                return "UTF-8";
            }
        }
        catch (Exception utf8Ex)
        {
            _logger?.LogDebug(utf8Ex, "UTF-8 encoding detection failed: {ErrorMessage}", utf8Ex.Message);
        }

        // Try to detect UTF-16
        try
        {
            var utf16Bytes = Encoding.Unicode.GetBytes(content);
            var roundTrip = Encoding.Unicode.GetString(utf16Bytes);
            if (roundTrip == content)
            {
                return "UTF-16";
            }
        }
        catch (Exception utf16Ex)
        {
            _logger?.LogDebug(utf16Ex, "UTF-16 encoding detection failed: {ErrorMessage}", utf16Ex.Message);
        }

        // Default to UTF-8 if we can't detect
        return "UTF-8";
    }

    /// <summary>
    /// Detects the CSV delimiter from file content
    /// </summary>
    public string DetectDelimiter(string csvContent, string? expectedDelimiter = null)
    {
        var firstLineForCheck = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).FirstOrDefault();
        
        if (!string.IsNullOrEmpty(expectedDelimiter))
        {
            // Verify expected delimiter exists
            if (firstLineForCheck != null && firstLineForCheck.Contains(expectedDelimiter))
            {
                return expectedDelimiter;
            }
        }

        // Common delimiters to check (including rarely used UTF characters)
        var delimiters = new[] { ",", ";", "\t", "|", "‖", "║" }; // ‖ = Double Vertical Line (U+2016), ║ = Box Drawing Double Vertical Line (U+2551)
        var firstLine = firstLineForCheck;

        if (string.IsNullOrEmpty(firstLine))
            return "‖"; // Default to Double Vertical Line

        // For multi-character delimiters, use string matching
        var delimiterCounts = new Dictionary<string, int>();
        foreach (var delimiter in delimiters)
        {
            if (delimiter.Length == 1)
            {
                delimiterCounts[delimiter] = firstLine.Count(c => c.ToString() == delimiter);
            }
            else
            {
                // For multi-character delimiters, count occurrences
                delimiterCounts[delimiter] = (firstLine.Length - firstLine.Replace(delimiter, "").Length) / delimiter.Length;
            }
        }

        // Return delimiter with highest count, or Double Vertical Line as default
        var bestDelimiter = delimiterCounts.OrderByDescending(kvp => kvp.Value).First();
        return bestDelimiter.Value > 0 ? bestDelimiter.Key : "‖";
    }

    /// <summary>
    /// Checks if content is a text file (not binary)
    /// </summary>
    private bool IsTextFile(string content)
    {
        // Check for null bytes (binary files contain null bytes)
        if (content.Contains('\0'))
        {
            return false;
        }

        // Check for too many non-printable characters (except common whitespace and control chars)
        int nonPrintableCount = 0;
        int totalChars = Math.Min(content.Length, 10000); // Check first 10KB
        
        for (int i = 0; i < totalChars; i++)
        {
            var ch = content[i];
            // Allow common whitespace and control characters
            if (!char.IsControl(ch) || ch == '\r' || ch == '\n' || ch == '\t' || ch == '\uFEFF')
            {
                continue;
            }
            
            // Count other control characters
            if (char.IsControl(ch))
            {
                nonPrintableCount++;
            }
        }

        // If more than 5% are non-printable control characters, likely binary
        if (totalChars > 0 && (nonPrintableCount * 100.0 / totalChars) > 5)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses CSV line handling quoted values and multi-line text
    /// </summary>
    private (List<string> columns, bool hasUnclosedQuote) ParseCsvLineWithQuotes(string line, string delimiter, char quoteChar)
    {
        var columns = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasUnclosedQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            var nextCh = i + 1 < line.Length ? line[i + 1] : '\0';

            if (ch == quoteChar)
            {
                if (inQuotes && nextCh == quoteChar)
                {
                    // Escaped quote (double quote)
                    current.Append(quoteChar);
                    i++; // Skip next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                    if (!inQuotes)
                    {
                        hasUnclosedQuote = false;
                    }
                    else
                    {
                        hasUnclosedQuote = true;
                    }
                }
            }
            else if (!inQuotes && line.Substring(i).StartsWith(delimiter))
            {
                // End of value (using custom separator)
                columns.Add(current.ToString());
                current.Clear();
                i += delimiter.Length - 1; // Skip separator
            }
            else
            {
                current.Append(ch);
            }
        }

        // Add last value
        columns.Add(current.ToString());
        hasUnclosedQuote = inQuotes;

        return (columns, hasUnclosedQuote);
    }

    /// <summary>
    /// Parses all CSV rows handling multi-line quoted values
    /// </summary>
    private List<List<string>> ParseCsvRowsWithQuotes(string csvContent, string delimiter, char quoteChar)
    {
        var rows = new List<List<string>>();
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentRow = new StringBuilder();
        var currentColumns = new List<string>();

        foreach (var line in lines)
        {
            currentRow.Append(line);
            
            // Parse the accumulated line
            var (columns, hasUnclosedQuote) = ParseCsvLineWithQuotes(currentRow.ToString(), delimiter, quoteChar);
            
            if (!hasUnclosedQuote)
            {
                // Quote is closed, this is a complete row
                rows.Add(columns);
                currentRow.Clear();
                currentColumns.Clear();
            }
            else
            {
                // Quote is still open, this is a multi-line value
                // Add newline and continue
                currentRow.Append("\n");
            }
        }

        // Add last row if any
        if (currentRow.Length > 0)
        {
            var (columns, _) = ParseCsvLineWithQuotes(currentRow.ToString(), delimiter, quoteChar);
            rows.Add(columns);
        }

        return rows;
    }

    private int CountColumns(string line, string delimiter)
    {
        if (string.IsNullOrEmpty(line))
            return 0;

        // Simple column count (doesn't handle quoted values with delimiters)
        // This is a basic check - full parsing would be done by CsvProcessingService
        var columns = line.Split(new[] { delimiter }, StringSplitOptions.None);
        return columns.Length;
    }
}

/// <summary>
/// Result of CSV validation
/// </summary>
public class CsvValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public string? Encoding { get; set; }
    public string? DetectedDelimiter { get; set; }
    public string? QuoteCharacter { get; set; }
    public int LineCount { get; set; }
    public int ColumnCount { get; set; }
    public bool HasHeader { get; set; }
    public bool HasBom { get; set; }
}

