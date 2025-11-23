using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using Microsoft.EntityFrameworkCore;

namespace InterfaceConfigurator.Main.Core.Factories;

/// <summary>
/// Central registry for checking feature enablement status
/// Uses caching to minimize database queries
/// Now uses MessageBoxDbContext (moved from ApplicationDbContext)
/// </summary>
public class FeatureRegistry : IFeatureRegistry
{
    private readonly MessageBoxDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeatureRegistry> _logger;
    private const string CACHE_KEY_ENABLED_FEATURES = "EnabledFeatures";
    private const int CACHE_DURATION_MINUTES = 5;

    public FeatureRegistry(
        MessageBoxDbContext context,
        IMemoryCache cache,
        ILogger<FeatureRegistry> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsFeatureEnabledAsync(int featureNumber)
    {
        try
        {
            var enabledFeatures = await GetEnabledFeatureNumbersAsync();
            return enabledFeatures.Contains(featureNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature {FeatureNumber} status", featureNumber);
            // Default to disabled on error (fail-safe)
            return false;
        }
    }

    public async Task<bool> IsFeatureEnabledByNameAsync(string featureName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return false;

            var feature = await _context.Features
                .FirstOrDefaultAsync(f => f.Title == featureName || f.Description.Contains(featureName));
            
            if (feature == null)
                return false;

            return feature.IsEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature {FeatureName} status", featureName);
            return false;
        }
    }

    public async Task<List<int>> GetEnabledFeatureNumbersAsync()
    {
        // Try to get from cache first
        if (_cache.TryGetValue(CACHE_KEY_ENABLED_FEATURES, out List<int>? cachedFeatures) && cachedFeatures != null)
        {
            return cachedFeatures;
        }

        // Query database
        try
        {
            var enabledFeatures = await _context.Features
                .Where(f => f.IsEnabled)
                .Select(f => f.FeatureNumber)
                .ToListAsync();

            // Cache for 5 minutes
            _cache.Set(CACHE_KEY_ENABLED_FEATURES, enabledFeatures, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            return enabledFeatures;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading enabled features from database");
            // Return empty list on error (fail-safe - all features disabled)
            return new List<int>();
        }
    }

    public async Task RefreshCacheAsync()
    {
        _cache.Remove(CACHE_KEY_ENABLED_FEATURES);
        // Pre-load cache
        await GetEnabledFeatureNumbersAsync();
        _logger.LogInformation("Feature registry cache refreshed");
    }
}

