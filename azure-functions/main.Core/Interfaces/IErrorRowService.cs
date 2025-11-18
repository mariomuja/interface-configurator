using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for saving failed rows to error folder
/// </summary>
public interface IErrorRowService
{
    /// <summary>
    /// Saves a failed row as a CSV file in the error folder
    /// </summary>
    Task SaveFailedRowAsync(string originalBlobName, Dictionary<string, string> row, RowProcessingResult result, int rowNumber, CancellationToken cancellationToken = default);
}






