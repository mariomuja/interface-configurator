using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class ExponentialBackoffRetryPolicyTests
{
    private readonly Mock<ILogger<ExponentialBackoffRetryPolicy>> _loggerMock;

    public ExponentialBackoffRetryPolicyTests()
    {
        _loggerMock = new Mock<ILogger<ExponentialBackoffRetryPolicy>>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceedOnFirstAttempt()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            logger: _loggerMock.Object
        );

        var attemptCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(async () =>
        {
            attemptCount++;
            return await Task.FromResult("success");
        }, CancellationToken.None);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetryOnTransientError()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            logger: _loggerMock.Object
        );

        var attemptCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new HttpRequestException("Transient error");
            }
            return await Task.FromResult("success");
        }, CancellationToken.None);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailAfterMaxRetries()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 2,
            baseDelay: TimeSpan.FromMilliseconds(10),
            logger: _loggerMock.Object
        );

        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await retryPolicy.ExecuteAsync(() =>
            {
                attemptCount++;
                return Task.FromException<string>(new HttpRequestException("Always fails"));
            }, CancellationToken.None);
        });

        Assert.Equal(3, attemptCount); // Initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRetryOnNonRetryableException()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            logger: _loggerMock.Object
        );

        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await retryPolicy.ExecuteAsync(() =>
            {
                attemptCount++;
                return Task.FromException<string>(new InvalidOperationException("Non-retryable"));
            }, CancellationToken.None);
        });

        Assert.Equal(1, attemptCount); // Should not retry
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomRetryCondition_ShouldRespectCondition()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            logger: _loggerMock.Object
        );

        var attemptCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(
            async () =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new InvalidOperationException("Retry this");
                }
                return await Task.FromResult("success");
            },
            ex => ex.Message.Contains("Retry this"),
            CancellationToken.None
        );

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public void MaxRetryAttempts_ShouldReturnConfiguredValue()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 5,
            logger: _loggerMock.Object
        );

        // Assert
        Assert.Equal(5, retryPolicy.MaxRetryAttempts);
    }

    [Fact]
    public void BaseDelay_ShouldReturnConfiguredValue()
    {
        // Arrange
        var baseDelay = TimeSpan.FromSeconds(2);
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            baseDelay: baseDelay,
            logger: _loggerMock.Object
        );

        // Assert
        Assert.Equal(baseDelay, retryPolicy.BaseDelay);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var retryPolicy = new ExponentialBackoffRetryPolicy(
            maxRetryAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(100),
            logger: _loggerMock.Object
        );

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                await Task.Delay(200, cts.Token);
                return "success";
            }, cts.Token);
        });
    }
}

