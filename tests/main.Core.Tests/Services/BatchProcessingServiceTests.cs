using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class BatchProcessingServiceTests
{
    private readonly Mock<ILogger<BatchProcessingService>> _loggerMock;
    private readonly BatchProcessingService _service;

    public BatchProcessingServiceTests()
    {
        _loggerMock = new Mock<ILogger<BatchProcessingService>>();
        _service = new BatchProcessingService(
            batchSize: 3,
            batchTimeout: TimeSpan.FromSeconds(5),
            logger: _loggerMock.Object
        );
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldProcessAllItems()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
        var processedItems = new List<int>();

        // Act
        var results = await _service.ProcessBatchAsync(
            items,
            (batch, ct) =>
            {
                processedItems.AddRange(batch);
                return Task.FromResult(batch.Select(i => i * 2).ToList());
            }
        );

        // Assert
        Assert.Equal(items.Count, processedItems.Count);
        Assert.Equal(items.Count, results.Count);
        Assert.All(results, r => Assert.Contains(r / 2, items));
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldRespectBatchSize()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var batchSizes = new List<int>();

        // Act
        await _service.ProcessBatchAsync(
            items,
            (batch, ct) =>
            {
                batchSizes.Add(batch.Count);
                return Task.FromResult(batch.Select(i => i).ToList());
            }
        );

        // Assert
        // Should have batches of size 3, 3, 3, 1 (last batch)
        Assert.True(batchSizes.All(size => size <= 3));
        Assert.Equal(4, batchSizes.Count); // 3+3+3+1 = 10 items
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldHandleEmptyList()
    {
        // Arrange
        var items = new List<int>();

        // Act
        var results = await _service.ProcessBatchAsync(
            items,
            (batch, ct) => Task.FromResult(batch.Select(i => i * 2).ToList())
        );

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldPropagateExceptions()
    {
        // Arrange
        var items = new List<string> { "1", "2", "3" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.ProcessBatchAsync<string, string>(
                items,
                (batch, ct) =>
                {
                    return Task.FromException<List<string>>(new InvalidOperationException("Test error"));
                }
            );
        });
    }

    [Fact]
    public async Task ProcessBatchParallelAsync_ShouldProcessInParallel()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var processingOrder = new List<int>();
        var lockObject = new object();

        // Act
        var results = await _service.ProcessBatchParallelAsync(
            items,
            async (batch, ct) =>
            {
                await Task.Delay(50); // Simulate processing time
                lock (lockObject)
                {
                    processingOrder.AddRange(batch);
                }
                return batch.Select(i => i).ToList();
            },
            maxConcurrency: 3
        );

        // Assert
        Assert.Equal(items.Count, results.Count);
        // Processing order might be different due to parallelism
        Assert.Equal(items.OrderBy(i => i), results.OrderBy(i => i));
    }

    [Fact]
    public async Task ProcessBatchParallelAsync_ShouldRespectMaxConcurrency()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var concurrentBatches = 0;
        var maxConcurrent = 0;
        var lockObject = new object();

        // Act
        await _service.ProcessBatchParallelAsync(
            items,
            async (batch, ct) =>
            {
                lock (lockObject)
                {
                    concurrentBatches++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentBatches);
                }

                await Task.Delay(100);

                lock (lockObject)
                {
                    concurrentBatches--;
                }

                return batch.Select(i => i).ToList();
            },
            maxConcurrency: 2
        );

        // Assert
        Assert.True(maxConcurrent <= 2);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).ToList();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.ProcessBatchAsync(
                items,
                async (batch, ct) =>
                {
                    await Task.Delay(100, ct);
                    return batch.Select(i => i).ToList();
                },
                cts.Token
            );
        });
    }
}

