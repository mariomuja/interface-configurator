using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class TokenBucketRateLimiterTests
{
    private readonly Mock<ILogger<TokenBucketRateLimiter>> _loggerMock;

    public TokenBucketRateLimiterTests()
    {
        _loggerMock = new Mock<ILogger<TokenBucketRateLimiter>>();
    }

    [Fact]
    public void CanExecute_ShouldReturnTrueWhenTokensAvailable()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            MaxRequests = 10,
            TimeWindow = TimeSpan.FromMinutes(1)
        };
        var rateLimiter = new TokenBucketRateLimiter(config, _loggerMock.Object);

        // Act
        var canExecute = rateLimiter.CanExecute();

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void CanExecute_ShouldReturnFalseWhenNoTokensAvailable()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            MaxRequests = 1,
            TimeWindow = TimeSpan.FromMinutes(1)
        };
        var rateLimiter = new TokenBucketRateLimiter(config, _loggerMock.Object);

        // Act
        rateLimiter.CanExecute(); // Consume first token
        var canExecute = rateLimiter.CanExecute(); // Try to consume second token

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public async Task WaitAsync_ShouldWaitWhenNoTokensAvailable()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            MaxRequests = 1,
            TimeWindow = TimeSpan.FromMilliseconds(100) // Very short window for testing
        };
        var rateLimiter = new TokenBucketRateLimiter(config, _loggerMock.Object);

        // Act
        rateLimiter.CanExecute(); // Consume token
        var startTime = DateTime.UtcNow;
        await rateLimiter.WaitAsync(); // Should wait for token refill
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(elapsed.TotalMilliseconds >= 90); // Should have waited for refill
    }

    [Fact]
    public void GetConfig_ShouldReturnConfiguredConfig()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            MaxRequests = 20,
            TimeWindow = TimeSpan.FromMinutes(2),
            Identifier = "test-limiter"
        };
        var rateLimiter = new TokenBucketRateLimiter(config, _loggerMock.Object);

        // Act
        var returnedConfig = rateLimiter.GetConfig();

        // Assert
        Assert.Equal(config.MaxRequests, returnedConfig.MaxRequests);
        Assert.Equal(config.TimeWindow, returnedConfig.TimeWindow);
        Assert.Equal(config.Identifier, returnedConfig.Identifier);
    }

    [Fact]
    public void CanExecute_ShouldRefillTokensOverTime()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            MaxRequests = 2,
            TimeWindow = TimeSpan.FromMilliseconds(200)
        };
        var rateLimiter = new TokenBucketRateLimiter(config, _loggerMock.Object);

        // Act - Consume all tokens
        Assert.True(rateLimiter.CanExecute());
        Assert.True(rateLimiter.CanExecute());
        Assert.False(rateLimiter.CanExecute()); // No tokens left

        // Wait for refill
        Thread.Sleep(250);

        // Assert - Should have tokens again
        Assert.True(rateLimiter.CanExecute());
    }

    [Fact]
    public async Task WaitAsync_ShouldRespectCancellationToken()
    {
        // Arrange
        var config = new RateLimitConfig
        {
            MaxRequests = 1,
            TimeWindow = TimeSpan.FromSeconds(10)
        };
        var rateLimiter = new TokenBucketRateLimiter(config, _loggerMock.Object);
        rateLimiter.CanExecute(); // Consume token

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await rateLimiter.WaitAsync(cts.Token);
        });
    }
}

