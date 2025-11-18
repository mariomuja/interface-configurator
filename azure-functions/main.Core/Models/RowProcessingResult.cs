namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Result of processing a single CSV row
/// </summary>
public class RowProcessingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, string>? RowData { get; set; }
    public int RowNumber { get; set; }

    public static RowProcessingResult SuccessResult(Dictionary<string, string> rowData, int rowNumber)
    {
        return new RowProcessingResult
        {
            Success = true,
            RowData = rowData,
            RowNumber = rowNumber
        };
    }

    public static RowProcessingResult FailureResult(string errorMessage, Dictionary<string, string>? rowData, int rowNumber, Exception? exception = null)
    {
        return new RowProcessingResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            RowData = rowData,
            RowNumber = rowNumber,
            Exception = exception
        };
    }
}






