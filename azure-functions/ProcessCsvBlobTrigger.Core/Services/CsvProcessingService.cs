using System.Globalization;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Services;

public class CsvProcessingService : ICsvProcessingService
{
    private const int ChunkSize = 100;

    public List<Dictionary<string, string>> ParseCsv(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            return new List<Dictionary<string, string>>();

        var lines = csvContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count < 2)
            return new List<Dictionary<string, string>>();

        var headers = lines[0]
            .Split(',')
            .Select(h => h.Trim().Trim('"'))
            .ToArray();

        var records = lines
            .Skip(1)
            .Select(line =>
            {
                var values = line
                    .Split(',')
                    .Select(v => v.Trim().Trim('"'))
                    .ToArray();

                return headers
                    .Select((header, index) => new { header, value = values.Length > index ? values[index] : string.Empty })
                    .ToDictionary(x => x.header, x => x.value);
            })
            .ToList();

        return records;
    }

    public List<List<TransportData>> CreateChunks(List<Dictionary<string, string>> records)
    {
        var transportDataList = records
            .Where(record => !string.IsNullOrEmpty(record.GetValueOrDefault("id", string.Empty)))
            .Select(record =>
            {
                try
                {
                    return new TransportData
                    {
                        Id = int.Parse(record.GetValueOrDefault("id", "0"), CultureInfo.InvariantCulture),
                        Name = record.GetValueOrDefault("name", string.Empty),
                        Email = record.GetValueOrDefault("email", string.Empty),
                        Age = int.Parse(record.GetValueOrDefault("age", "0"), CultureInfo.InvariantCulture),
                        City = record.GetValueOrDefault("city", string.Empty),
                        Salary = decimal.Parse(record.GetValueOrDefault("salary", "0"), CultureInfo.InvariantCulture),
                        CreatedAt = DateTime.UtcNow
                    };
                }
                catch
                {
                    return null;
                }
            })
            .Where(data => data != null)
            .Cast<TransportData>()
            .ToList();

        return transportDataList
            .Chunk(ChunkSize)
            .Select(chunk => chunk.ToList())
            .ToList();
    }
}

