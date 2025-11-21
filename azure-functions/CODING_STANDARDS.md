# Azure Functions Coding Standards

## Error Handling

> **⚠️ MEMORY**: Empty catch blocks are FORBIDDEN. See `../MEMORY_EMPTY_CATCH_BLOCKS_FORBIDDEN.md` for details.

### ⚠️ NEVER Use Empty Catch Blocks

**CRITICAL RULE**: All catch blocks must log exceptions. Empty catch blocks (`catch { }`) are forbidden.

#### Why?
- Empty catch blocks hide errors completely
- Makes debugging impossible
- Function runtime can't report errors
- Silent failures go unnoticed

#### Required Pattern

```csharp
try
{
    await _loggingService.LogAsync("info", "Message");
}
catch (Exception logEx)
{
    // ALWAYS log the exception
    _logger.LogWarning(logEx, "Failed to log: {ErrorMessage}", logEx.Message);
}
```

#### Fallback Pattern (When ILogger might fail)

```csharp
try
{
    await _loggingService.LogAsync("info", "Message");
}
catch (Exception logEx)
{
    try
    {
        _logger.LogWarning(logEx, "Failed to log: {ErrorMessage}", logEx.Message);
    }
    catch (Exception loggerEx)
    {
        // Fallback to console
        Console.Error.WriteLine($"Failed to log: {logEx.Message}");
    }
}
```

## Code Review Checklist

- [ ] No empty catch blocks
- [ ] All exceptions are logged
- [ ] Critical errors are handled appropriately
- [ ] Logging failures don't break execution

See main [CODING_STANDARDS.md](../CODING_STANDARDS.md) for full guidelines.

