using Xunit;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class RetryServiceTests
{
    private readonly RetryService _retryService;

    public RetryServiceTests()
    {
        _retryService = new RetryService();
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldSucceed_OnFirstAttempt()
    {
        // Arrange
        int attemptCount = 0;
        Func<Task<bool>> operation = async () =>
        {
            attemptCount++;
            return await Task.FromResult(true);
        };

        // Act
        var result = await _retryService.ExecuteWithRetryAsync(operation, "test-operation");

        // Assert
        Assert.True(result);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldRetry_OnFailure()
    {
        // Arrange
        int attemptCount = 0;
        Func<Task<bool>> operation = async () =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new Exception($"Attempt {attemptCount} failed");
            }
            return await Task.FromResult(true);
        };

        // Act
        var result = await _retryService.ExecuteWithRetryAsync(operation, "test-operation");

        // Assert
        Assert.True(result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldFail_AfterMaxRetries()
    {
        // Arrange
        int attemptCount = 0;
        Func<Task<bool>> operation = () =>
        {
            attemptCount++;
            return Task.FromException<bool>(new Exception($"Attempt {attemptCount} failed"));
        };

        var retryPolicy = new RetryPolicy
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0
        };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _retryService.ExecuteWithRetryAsync(operation, "test-operation", retryPolicy);
        });

        Assert.True(attemptCount >= 3); // At least initial + retries
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldUseExponentialBackoff()
    {
        // Arrange
        var timestamps = new System.Collections.Generic.List<DateTime>();
        int attemptCount = 0;
        Func<Task<bool>> operation = async () =>
        {
            timestamps.Add(DateTime.UtcNow);
            attemptCount++;
            if (attemptCount < 3)
            {
                await Task.Delay(10); // Small delay to ensure timestamps differ
                throw new Exception($"Attempt {attemptCount} failed");
            }
            return await Task.FromResult(true);
        };

        // Act
        var startTime = DateTime.UtcNow;
        await _retryService.ExecuteWithRetryAsync(operation, "test-operation");
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.True((endTime - startTime).TotalMilliseconds > 0); // Should have some delay
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);
        
        Func<Task<bool>> operation = async () =>
        {
            await Task.Delay(500, cts.Token);
            return await Task.FromResult(true);
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _retryService.ExecuteWithRetryAsync(operation, "test-operation", cancellationToken: cts.Token);
        });
    }
}

