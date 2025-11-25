using System.Diagnostics;

namespace InterfaceConfigurator.Main.Core.Helpers;

/// <summary>
/// Helper for managing correlation IDs across async operations
/// Uses AsyncLocal to maintain correlation ID across async boundaries
/// </summary>
public static class CorrelationIdHelper
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets the current correlation ID
    /// </summary>
    public static string? Current
    {
        get => _correlationId.Value ?? Activity.Current?.Id ?? Activity.Current?.RootId;
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Generates a new correlation ID
    /// </summary>
    public static string Generate()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Sets a correlation ID for the current async context
    /// </summary>
    public static void Set(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    /// <summary>
    /// Clears the correlation ID for the current async context
    /// </summary>
    public static void Clear()
    {
        _correlationId.Value = null;
    }

    /// <summary>
    /// Ensures a correlation ID exists, generating one if needed
    /// </summary>
    public static string Ensure()
    {
        if (string.IsNullOrWhiteSpace(Current))
        {
            Current = Generate();
        }
        return Current!;
    }
}

