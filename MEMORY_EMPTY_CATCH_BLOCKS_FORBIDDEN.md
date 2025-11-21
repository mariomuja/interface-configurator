# Memory: Empty Catch Blocks Are FORBIDDEN

## ⚠️ CRITICAL RULE

**Empty catch blocks (`catch { }` or `catch (Exception ex) { }`) are FORBIDDEN in this codebase.**

## Why This Rule Exists

Empty catch blocks silently swallow errors, making debugging extremely difficult and preventing proper error reporting:

1. **No Error Visibility**: Errors are completely hidden from logs and monitoring
2. **Debugging Nightmare**: No way to know what went wrong when issues occur
3. **Function Runtime Blind**: Azure Functions runtime can't report errors
4. **Silent Failures**: Critical errors go unnoticed until they cause bigger problems
5. **Production Issues**: Problems that could be caught early remain hidden

## Required Pattern

**ALWAYS log exceptions in catch blocks, even if you don't rethrow:**

```csharp
// ✅ CORRECT: Log the exception
try
{
    await SomeOperationAsync();
}
catch (Exception ex)
{
    _logger?.LogWarning(ex, "Error in SomeOperationAsync: {ErrorMessage}", ex.Message);
    // Continue execution if this is non-critical
}
```

## Examples from This Codebase

### ✅ Good Examples

**SFTP Client Disposal:**
```csharp
try 
{ 
    client.Dispose(); 
} 
catch (Exception disposeEx)
{
    _logger?.LogWarning(disposeEx, "Error disposing SFTP client: {ErrorMessage}", disposeEx.Message);
}
```

**Message Lock Release:**
```csharp
try
{
    await _messageBoxService.ReleaseMessageLockAsync(message.MessageId, "Error", cancellationToken);
}
catch (Exception releaseEx)
{
    _logger?.LogWarning(releaseEx, "Error releasing message lock for message {MessageId}: {ErrorMessage}", 
        message.MessageId, releaseEx.Message);
}
```

**Encoding Detection (Non-Critical):**
```csharp
try
{
    var utf8Bytes = Encoding.UTF8.GetBytes(content);
    var roundTrip = Encoding.UTF8.GetString(utf8Bytes);
    if (roundTrip == content)
    {
        return "UTF-8";
    }
}
catch (Exception utf8Ex)
{
    _logger?.LogDebug(utf8Ex, "UTF-8 encoding detection failed: {ErrorMessage}", utf8Ex.Message);
}
```

### ❌ FORBIDDEN Examples

```csharp
// ❌ FORBIDDEN: Empty catch block
try
{
    await SomeOperationAsync();
}
catch { }  // ERROR: Silently swallows all exceptions!

// ❌ FORBIDDEN: Catch with variable but no logging
try
{
    await SomeOperationAsync();
}
catch (Exception ex) { }  // ERROR: Exception caught but not logged!
```

## Logging Levels

Use appropriate logging levels based on severity:

- **LogError**: Critical errors that affect functionality
- **LogWarning**: Non-critical errors that should be investigated
- **LogDebug**: Expected failures in fallback scenarios (e.g., encoding detection)

## When Logging Might Fail

If logging itself might fail (e.g., logging to database), use fallback:

```csharp
try
{
    await _loggingService.LogAsync("error", "Processing failed", errorDetails);
}
catch (Exception logEx)
{
    // Try ILogger first
    try
    {
        _logger?.LogWarning(logEx, "Failed to log to database: {ErrorMessage}", logEx.Message);
    }
    catch (Exception loggerEx)
    {
        // Fallback to console if ILogger also fails
        Console.Error.WriteLine($"Failed to log: {logEx.Message}");
        Console.Error.WriteLine($"Logger also failed: {loggerEx.Message}");
    }
}
```

## Code Review Checklist

When reviewing code, **ALWAYS** check for:

- [ ] No empty catch blocks (`catch { }`)
- [ ] No catch blocks with variables but no logging (`catch (Exception ex) { }`)
- [ ] All catch blocks log exceptions using `_logger?.Log*`
- [ ] Appropriate logging level is used (Error/Warning/Debug)
- [ ] Critical errors are rethrown or returned as failures
- [ ] Non-critical errors don't break execution but are logged

## How to Find Empty Catch Blocks

### Using grep/ripgrep:
```bash
# Find empty catch blocks
grep -r "catch\s*{" azure-functions/
grep -r "catch\s*(\s*)\s*{" azure-functions/
```

### Using VS Code:
1. Search: `catch\s*\{`
2. Review each match
3. Ensure exceptions are logged

## Enforcement

- **Pre-commit**: Code reviews must reject any PR with empty catch blocks
- **CI/CD**: Consider adding a linting rule to detect empty catch blocks
- **Documentation**: This rule is documented in:
  - `CODING_STANDARDS.md`
  - `azure-functions/CODING_STANDARDS.md`
  - `MEMORY_EMPTY_CATCH_BLOCKS_FORBIDDEN.md` (this file)

## Remember

> **"If you catch an exception, you must do something with it - even if that something is logging it. Silent failures are worse than no error handling at all."**

## References

- [Microsoft: Exception Handling Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Azure Functions: Logging and Monitoring](https://learn.microsoft.com/en-us/azure/azure-functions/functions-monitoring)
- See also: `CODING_STANDARDS.md` for detailed error handling guidelines

---

**Last Updated**: 2025-11-21
**Status**: ACTIVE - This rule is enforced in all code reviews
















