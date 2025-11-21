# Coding Standards - Error Handling

> **⚠️ MEMORY**: Empty catch blocks are FORBIDDEN. See `MEMORY_EMPTY_CATCH_BLOCKS_FORBIDDEN.md` for details.

## ⚠️ CRITICAL: Never Use Empty Catch Blocks

### The Problem

Empty catch blocks (`catch { }` or `catch (Exception ex) { }`) silently swallow errors, making debugging extremely difficult and preventing the function runtime from reporting errors.

**BAD - Never do this:**
```csharp
try
{
    await _loggingService.LogAsync("info", "Processing started");
}
catch { }  // ❌ ERROR: Silently swallows all exceptions!
```

### Why This Is Dangerous

1. **No Error Visibility**: Errors are completely hidden
2. **Debugging Nightmare**: No way to know what went wrong
3. **Function Runtime Blind**: Azure Functions runtime can't report errors
4. **Silent Failures**: Critical errors go unnoticed

### The Solution

**Always log exceptions, even in catch blocks:**

#### Option 1: Log to ILogger (Preferred)
```csharp
try
{
    await _loggingService.LogAsync("info", "Processing started");
}
catch (Exception logEx)
{
    _logger.LogWarning(logEx, "Failed to log to database: {ErrorMessage}", logEx.Message);
}
```

#### Option 2: Log to Console.Error (Fallback)
```csharp
try
{
    await _loggingService.LogAsync("info", "Processing started");
}
catch (Exception logEx)
{
    Console.Error.WriteLine($"Failed to log: {logEx.Message}");
}
```

#### Option 3: Multi-Layer Fallback (Best Practice)
```csharp
try
{
    await _loggingService.LogAsync("info", "Processing started");
}
catch (Exception logEx)
{
    // Try ILogger first
    try
    {
        _logger.LogWarning(logEx, "Failed to log to database: {ErrorMessage}", logEx.Message);
    }
    catch (Exception loggerEx)
    {
        // Fallback to console if ILogger also fails
        try
        {
            Console.Error.WriteLine($"Failed to log: {logEx.Message}");
            Console.Error.WriteLine($"Logger also failed: {loggerEx.Message}");
        }
        catch
        {
            // Absolute last resort - if even Console.Error fails, we can't do anything
            // This should never happen, but we handle it gracefully
        }
    }
}
```

## Error Handling Guidelines

### 1. Logging Errors

When logging fails, always log the failure:

```csharp
try
{
    await _loggingService.LogAsync("error", "Processing failed", errorDetails);
}
catch (Exception logEx)
{
    // Log the logging failure
    _logger.LogWarning(logEx, "Failed to log error to database: {ErrorMessage}", logEx.Message);
    // Don't throw - logging failures shouldn't break the function
}
```

### 2. Non-Critical Operations

For non-critical operations (like logging), catch and log but don't throw:

```csharp
try
{
    await _loggingService.LogAsync("info", "Operation completed");
}
catch (Exception logEx)
{
    _logger.LogWarning(logEx, "Failed to log completion: {ErrorMessage}", logEx.Message);
    // Continue execution - logging failure is not critical
}
```

### 3. Critical Operations

For critical operations, log the error and rethrow or return failure:

```csharp
try
{
    await _dataService.ProcessChunksAsync(chunks);
}
catch (Exception ex)
{
    // Log the error
    _logger.LogError(ex, "Failed to process chunks: {ErrorMessage}", ex.Message);
    
    // Try to log to database as well
    try
    {
        await _loggingService.LogAsync("error", "Chunk processing failed", ex.Message);
    }
    catch (Exception logEx)
    {
        _logger.LogWarning(logEx, "Failed to log processing error: {ErrorMessage}", logEx.Message);
    }
    
    // Rethrow or return failure
    throw; // or return ProcessingResult.Failure(...)
}
```

## Code Review Checklist

When reviewing code, check for:

- [ ] No empty catch blocks (`catch { }`)
- [ ] All catch blocks log exceptions
- [ ] Critical errors are rethrown or returned as failures
- [ ] Non-critical errors (like logging) don't break execution
- [ ] Fallback logging mechanisms are in place

## Examples from This Codebase

### ✅ Good Example (CsvProcessor.cs)
```csharp
try
{
    await _loggingService.LogAsync("info", "Processing started");
}
catch (Exception logEx)
{
    _logger.LogWarning(logEx, "Failed to log start: {ErrorMessage}", logEx.Message);
}
```

### ❌ Bad Example (Never do this)
```csharp
try
{
    await _loggingService.LogAsync("info", "Processing started");
}
catch { }  // ❌ BAD: Silently swallows errors
```

## Tools to Find Empty Catch Blocks

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

## Remember

> **"If you catch an exception, you must do something with it - even if that something is logging it. Silent failures are worse than no error handling at all."**

## References

- [Microsoft: Exception Handling Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Azure Functions: Logging and Monitoring](https://learn.microsoft.com/en-us/azure/azure-functions/functions-monitoring)

