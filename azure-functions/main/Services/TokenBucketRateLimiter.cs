using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Token bucket rate limiter implementation
/// Allows bursts up to max requests, then refills at steady rate
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly ILogger<TokenBucketRateLimiter>? _logger;
    private readonly RateLimitConfig _config;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly object _lockObject = new();

    public TokenBucketRateLimiter(
        RateLimitConfig config,
        ILogger<TokenBucketRateLimiter>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    public RateLimitConfig GetConfig() => _config;

    public bool CanExecute()
    {
        var bucket = GetOrCreateBucket();
        return bucket.TryConsume();
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        var bucket = GetOrCreateBucket();
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        
        while (!bucket.TryConsume())
        {
            var waitTime = bucket.GetTimeUntilNextToken();
            
            _logger?.LogDebug(
                "[CorrelationId: {CorrelationId}] Rate limit reached. Waiting {WaitTime}ms before next token available.",
                correlationId, waitTime.TotalMilliseconds);
            
            await Task.Delay(waitTime, cancellationToken);
        }
    }

    private TokenBucket GetOrCreateBucket()
    {
        var key = _config.Identifier ?? "default";
        return _buckets.GetOrAdd(key, _ => new TokenBucket(_config.MaxRequests, _config.TimeWindow, _logger));
    }

    private class TokenBucket
    {
        private readonly int _maxTokens;
        private readonly TimeSpan _refillInterval;
        private readonly ILogger? _logger;
        private int _tokens;
        private DateTime _lastRefill;

        public TokenBucket(int maxTokens, TimeSpan timeWindow, ILogger? logger = null)
        {
            _maxTokens = maxTokens;
            _refillInterval = TimeSpan.FromMilliseconds(timeWindow.TotalMilliseconds / maxTokens);
            _logger = logger;
            _tokens = maxTokens;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryConsume()
        {
            lock (this)
            {
                Refill();
                
                if (_tokens > 0)
                {
                    _tokens--;
                    return true;
                }
                
                return false;
            }
        }

        public TimeSpan GetTimeUntilNextToken()
        {
            lock (this)
            {
                Refill();
                
                if (_tokens > 0)
                {
                    return TimeSpan.Zero;
                }
                
                var timeSinceLastRefill = DateTime.UtcNow - _lastRefill;
                var timeUntilNextRefill = _refillInterval - timeSinceLastRefill;
                
                return timeUntilNextRefill > TimeSpan.Zero ? timeUntilNextRefill : _refillInterval;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var timeSinceLastRefill = now - _lastRefill;
            
            if (timeSinceLastRefill >= _refillInterval)
            {
                var tokensToAdd = (int)(timeSinceLastRefill.TotalMilliseconds / _refillInterval.TotalMilliseconds);
                _tokens = Math.Min(_maxTokens, _tokens + tokensToAdd);
                _lastRefill = now;
            }
        }
    }
}

