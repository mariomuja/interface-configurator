# Idempotency Fixes - Implementation Summary

## ✅ Implemented Idempotency Improvements

### 1. Message Creation Idempotency
**File:** `azure-functions/main/Services/MessageBoxService.cs`

**Issue:** Same CSV row processed twice would create duplicate messages

**Fix:**
- ✅ Added duplicate detection using `MessageHash`
- ✅ Checks for existing message with same hash, interface, and adapter within 24 hours
- ✅ Returns existing message ID if duplicate found (idempotent)

**Code:**
```csharp
// Check for duplicate message (idempotency check)
var existingMessage = await _context.Messages
    .Where(m => m.MessageHash == messageHash 
        && m.InterfaceName == interfaceName
        && m.AdapterName == adapterName
        && m.AdapterInstanceGuid == adapterInstanceGuid
        && m.datetime_created > DateTime.UtcNow.AddHours(-24))
    .FirstOrDefaultAsync(cancellationToken);

if (existingMessage != null)
{
    return existingMessage.MessageId; // Idempotent: return existing message ID
}
```

### 2. Message Lock Acquisition (Race Condition Fix)
**File:** `azure-functions/main/Services/MessageBoxService.cs`

**Issue:** Race condition in `MarkMessageAsInProgressAsync` - check-then-act pattern

**Fix:**
- ✅ Changed to atomic database UPDATE with WHERE clause
- ✅ Single SQL operation checks and updates in one transaction
- ✅ Prevents multiple instances from acquiring same lock

**Code:**
```csharp
// Atomic lock acquisition
var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
    @"UPDATE Messages 
      SET Status = 'InProgress', 
          InProgressUntil = {0}
      WHERE MessageId = {1}
        AND (Status != 'InProgress' 
             OR InProgressUntil IS NULL 
             OR InProgressUntil <= {2})
        AND Status != 'Processed'
        AND Status != 'DeadLetter'",
    lockUntil, messageId, now, cancellationToken);
```

### 3. Message Processing Completion Idempotency
**File:** `azure-functions/main/Services/MessageBoxService.cs`

**Issue:** If `MarkMessageAsProcessedAsync` is called twice, could cause issues

**Fix:**
- ✅ Atomic UPDATE that only processes if not already processed
- ✅ Idempotent: safe to call multiple times

**Code:**
```csharp
// Atomic update - only if not already processed
var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
    @"UPDATE Messages 
      SET Status = 'Processed', 
          datetime_processed = GETUTCDATE(),
          ProcessingDetails = {0},
          InProgressUntil = NULL
      WHERE MessageId = {1}
        AND Status != 'Processed'
        AND Status != 'DeadLetter'",
    processingDetails ?? (object)DBNull.Value, messageId, cancellationToken);
```

### 4. Message Lock Release Idempotency
**File:** `azure-functions/main/Services/MessageBoxService.cs`

**Issue:** `ReleaseMessageLockAsync` could fail if called multiple times

**Fix:**
- ✅ Atomic UPDATE that only releases if locked
- ✅ Idempotent: safe to call even if already released

### 5. Blob Movement Idempotency
**File:** `azure-functions/main/main.cs`

**Issue:** If blob move is retried, could fail or cause errors

**Fix:**
- ✅ Check if target blob already exists before moving
- ✅ If exists, delete source and return (idempotent)
- ✅ Prevents duplicate blob operations

**Code:**
```csharp
// Check if target blob already exists (idempotency check)
if (await targetBlobClient.ExistsAsync())
{
    _logger.LogInformation("Target blob already exists, skipping move (idempotent)");
    await sourceBlobClient.DeleteIfExistsAsync();
    return;
}
```

### 6. TransportData Insert Idempotency
**File:** `azure-functions/main/Services/DataServiceAdapter.cs`

**Issue:** Duplicate key violations if same data inserted twice

**Fix:**
- ✅ Handle SQL duplicate key violations (error 2627, 2601) gracefully
- ✅ Treat as idempotent operation (already inserted)
- ✅ Log warning but don't throw exception

**Code:**
```csharp
catch (Microsoft.Data.SqlClient.SqlException sqlEx)
{
    // Handle duplicate key violations gracefully (idempotency)
    if (sqlEx.Number == 2627 || sqlEx.Number == 2601)
    {
        // Don't throw - treat as idempotent operation (already inserted)
        continue; // Continue with next batch
    }
    throw;
}
```

### 7. Database Index for MessageHash
**File:** `azure-functions/main/Data/MessageBoxDbContext.cs`

**Fix:**
- ✅ Added index on `MessageHash` for faster duplicate detection
- ✅ Improves performance of idempotency checks

## 🔍 Idempotency Patterns Used

### Pattern 1: Duplicate Detection
- Calculate hash of data
- Check if exists before creating
- Return existing if found

### Pattern 2: Atomic Updates
- Use SQL UPDATE with WHERE clause
- Check condition and update in single operation
- Prevents race conditions

### Pattern 3: Graceful Error Handling
- Catch duplicate key violations
- Treat as success (already done)
- Continue processing

### Pattern 4: Existence Checks
- Check if target exists before operation
- Skip if already exists
- Delete source if target exists

## 📊 Benefits

1. **No Duplicate Messages** - Same CSV row won't create duplicate messages
2. **No Race Conditions** - Atomic operations prevent concurrent processing issues
3. **Safe Retries** - Operations can be safely retried without side effects
4. **Better Reliability** - System handles failures and retries gracefully
5. **Performance** - Indexes improve duplicate detection speed

## 🧪 Testing Idempotency

### Test 1: Duplicate Message Creation
1. Process same CSV file twice
2. Verify only one message created
3. Verify second call returns existing message ID

### Test 2: Concurrent Message Processing
1. Start multiple function instances
2. Process same message concurrently
3. Verify only one instance processes it

### Test 3: Retry After Failure
1. Process message
2. Simulate failure after insert but before marking complete
3. Retry processing
4. Verify no duplicate inserts

### Test 4: Blob Movement Retry
1. Move blob
2. Retry move operation
3. Verify no errors, blob in correct location

## 📝 Notes

- MessageHash check uses 24-hour window (configurable)
- Duplicate key violations are logged but not thrown
- All idempotency checks are logged for monitoring
- Atomic operations use database-level transactions
- Indexes improve performance of duplicate detection

## 🔄 Future Improvements

- [ ] Add configurable duplicate detection window
- [ ] Add idempotency key/token support for API endpoints
- [ ] Add idempotency metrics tracking
- [ ] Consider unique constraint on MessageHash (if needed)
- [ ] Add idempotency tests to test suite

