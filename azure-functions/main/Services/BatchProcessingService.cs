using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for batch processing Service Bus messages
/// Groups messages into batches for efficient processing
/// </summary>
public class BatchProcessingService
{
    private readonly ILogger<BatchProcessingService>? _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;

    public BatchProcessingService(
        int batchSize = 100,
        TimeSpan? batchTimeout = null,
        ILogger<BatchProcessingService>? logger = null)
    {
        _batchSize = batchSize;
        _batchTimeout = batchTimeout ?? TimeSpan.FromSeconds(5);
        _logger = logger;
    }

    /// <summary>
    /// Processes items in batches
    /// </summary>
    public async Task<List<TResult>> ProcessBatchAsync<TItem, TResult>(
        IEnumerable<TItem> items,
        Func<List<TItem>, CancellationToken, Task<List<TResult>>> batchProcessor,
        CancellationToken cancellationToken = default)
    {
        var correlationId = CorrelationIdHelper.Ensure();
        var results = new List<TResult>();
        var itemList = items.ToList();
        var totalItems = itemList.Count;
        var batchNumber = 0;

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Starting batch processing: {TotalItems} items, BatchSize={BatchSize}",
            correlationId, totalItems, _batchSize);

        for (int i = 0; i < itemList.Count; i += _batchSize)
        {
            batchNumber++;
            var batch = itemList.Skip(i).Take(_batchSize).ToList();
            
            _logger?.LogDebug(
                "[CorrelationId: {CorrelationId}] Processing batch {BatchNumber}: {BatchSize} items (items {StartIndex}-{EndIndex})",
                correlationId, batchNumber, batch.Count, i, Math.Min(i + _batchSize - 1, totalItems - 1));

            try
            {
                using var batchCts = new CancellationTokenSource(_batchTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, batchCts.Token);
                
                var batchResults = await batchProcessor(batch, linkedCts.Token);
                results.AddRange(batchResults);
                
                _logger?.LogDebug(
                    "[CorrelationId: {CorrelationId}] Batch {BatchNumber} completed: {ResultCount} results",
                    correlationId, batchNumber, batchResults.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "[CorrelationId: {CorrelationId}] Batch {BatchNumber} failed: {Error}",
                    correlationId, batchNumber, ex.Message);
                throw;
            }
        }

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Batch processing completed: {TotalBatches} batches, {TotalResults} results",
            correlationId, batchNumber, results.Count);

        return results;
    }

    /// <summary>
    /// Processes items in batches with parallel execution
    /// </summary>
    public async Task<List<TResult>> ProcessBatchParallelAsync<TItem, TResult>(
        IEnumerable<TItem> items,
        Func<List<TItem>, CancellationToken, Task<List<TResult>>> batchProcessor,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default)
    {
        var correlationId = CorrelationIdHelper.Ensure();
        var itemList = items.ToList();
        var batches = itemList
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / _batchSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Starting parallel batch processing: {TotalItems} items, {BatchCount} batches, MaxConcurrency={MaxConcurrency}",
            correlationId, itemList.Count, batches.Count, maxConcurrency);

        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = batches.Select(async (batch, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var batchCts = new CancellationTokenSource(_batchTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, batchCts.Token);
                
                return await batchProcessor(batch, linkedCts.Token);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var flattenedResults = results.SelectMany(r => r).ToList();

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Parallel batch processing completed: {TotalResults} results",
            correlationId, flattenedResults.Count);

        return flattenedResults;
    }
}

