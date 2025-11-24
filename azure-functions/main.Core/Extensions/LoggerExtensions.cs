using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Core.Extensions;

/// <summary>
/// Extension methods for structured logging with correlation IDs
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs information with correlation ID
    /// </summary>
    public static void LogInformationWithCorrelation(
        this ILogger logger,
        string message,
        params object[] args)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        logger.LogInformation($"[CorrelationId: {correlationId}] {message}", args);
    }

    /// <summary>
    /// Logs error with correlation ID
    /// </summary>
    public static void LogErrorWithCorrelation(
        this ILogger logger,
        Exception exception,
        string message,
        params object[] args)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        logger.LogError(exception, $"[CorrelationId: {correlationId}] {message}", args);
    }

    /// <summary>
    /// Logs warning with correlation ID
    /// </summary>
    public static void LogWarningWithCorrelation(
        this ILogger logger,
        string message,
        params object[] args)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        logger.LogWarning($"[CorrelationId: {correlationId}] {message}", args);
    }

    /// <summary>
    /// Logs debug with correlation ID
    /// </summary>
    public static void LogDebugWithCorrelation(
        this ILogger logger,
        string message,
        params object[] args)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        logger.LogDebug($"[CorrelationId: {correlationId}] {message}", args);
    }

    /// <summary>
    /// Creates a scoped logger with correlation ID
    /// </summary>
    public static ILogger WithCorrelationId(this ILogger logger, string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = CorrelationIdHelper.Ensure();
        }
        else
        {
            CorrelationIdHelper.Set(correlationId);
        }
        
        return logger;
    }
}

