using System.Text;

namespace ProcessCsvBlobTrigger.Core.Helpers;

/// <summary>
/// Helper class for formatting exceptions including inner exceptions for logging
/// </summary>
public static class ExceptionHelper
{
    /// <summary>
    /// Formats an exception with all inner exceptions recursively
    /// </summary>
    /// <param name="exception">The exception to format</param>
    /// <param name="includeStackTrace">Whether to include stack traces</param>
    /// <returns>Formatted exception string</returns>
    public static string FormatException(Exception? exception, bool includeStackTrace = true)
    {
        if (exception == null)
        {
            return "Exception is null";
        }

        var sb = new StringBuilder();
        var depth = 0;
        var currentException = exception;

        while (currentException != null)
        {
            if (depth > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"--- Inner Exception #{depth} ---");
            }

            sb.AppendLine($"Type: {currentException.GetType().FullName}");
            sb.AppendLine($"Message: {currentException.Message}");

            if (!string.IsNullOrWhiteSpace(currentException.Source))
            {
                sb.AppendLine($"Source: {currentException.Source}");
            }

            if (currentException.TargetSite != null)
            {
                sb.AppendLine($"TargetSite: {currentException.TargetSite}");
            }

            if (includeStackTrace && !string.IsNullOrWhiteSpace(currentException.StackTrace))
            {
                sb.AppendLine($"StackTrace:");
                sb.AppendLine(currentException.StackTrace);
            }

            // Add additional exception properties if available
            if (currentException is AggregateException aggregateException)
            {
                sb.AppendLine($"InnerExceptions Count: {aggregateException.InnerExceptions.Count}");
                foreach (var innerEx in aggregateException.InnerExceptions)
                {
                    sb.AppendLine($"  - {innerEx.GetType().Name}: {innerEx.Message}");
                }
            }

            // Move to inner exception
            currentException = currentException.InnerException;
            depth++;

            // Prevent infinite loops (shouldn't happen, but safety check)
            if (depth > 20)
            {
                sb.AppendLine();
                sb.AppendLine("--- Maximum depth reached (possible circular reference) ---");
                break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a summary of the exception chain (without stack traces)
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <returns>Summary string</returns>
    public static string GetExceptionSummary(Exception? exception)
    {
        if (exception == null)
        {
            return "Exception is null";
        }

        var sb = new StringBuilder();
        var depth = 0;
        var currentException = exception;

        while (currentException != null)
        {
            if (depth > 0)
            {
                sb.Append(" -> ");
            }

            sb.Append($"{currentException.GetType().Name}: {currentException.Message}");

            currentException = currentException.InnerException;
            depth++;

            if (depth > 20)
            {
                sb.Append(" -> [Max depth reached]");
                break;
            }
        }

        return sb.ToString();
    }
}


