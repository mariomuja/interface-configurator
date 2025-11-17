# Reprocessing Ideas for Failed CSV Rows

## Overview
Rows in the `csv-error` folder require manual validation by integration engineers. After fixing the source of failure, these rows need to be reprocessed. Below are ideas for implementing reprocessing functionality (not yet implemented).

## Idea 1: Manual Reprocessing via Azure Portal/CLI
**Description:** Integration engineers manually trigger reprocessing through Azure Portal or Azure CLI.

**Implementation:**
- Create an Azure Function HTTP endpoint: `POST /api/reprocess-error-row`
- Accepts error file name or blob path as parameter
- Validates the file exists in `csv-error` folder
- Moves file back to `csv-incoming` folder
- Function automatically processes it again

**Pros:**
- Simple to implement
- Full control for engineers
- Can be triggered from anywhere

**Cons:**
- Requires manual intervention
- No batch processing

## Idea 2: Reprocessing Queue/API Endpoint
**Description:** Create a dedicated API endpoint that accepts error file names for reprocessing.

**Implementation:**
- Vercel API endpoint: `POST /api/reprocess-errors`
- Accepts array of error file names
- Validates each file exists
- Moves files to `csv-incoming` folder
- Returns processing status

**Pros:**
- Can be called from frontend
- Supports batch reprocessing
- Easy to integrate with UI

**Cons:**
- Requires frontend changes
- No automatic retry logic

## Idea 3: Automatic Retry with Exponential Backoff
**Description:** Automatically retry failed rows after a delay, with exponential backoff.

**Implementation:**
- Store failed row metadata in a separate table: `FailedRowRetries`
- Columns: `Id`, `OriginalBlobName`, `RowNumber`, `Error`, `RetryCount`, `NextRetryTime`
- Azure Function Timer Trigger checks for rows ready to retry
- Moves row back to `csv-incoming` after delay
- Max retries: 3, with increasing delays (1h, 4h, 24h)

**Pros:**
- Automatic recovery from transient errors
- Reduces manual intervention
- Handles temporary issues (network, DB locks)

**Cons:**
- May retry rows that will always fail
- Requires additional infrastructure
- Complex to implement

## Idea 4: Reprocessing Dashboard in Frontend
**Description:** Create a UI dashboard showing all failed rows with reprocessing buttons.

**Implementation:**
- New frontend component: `ErrorRowsDashboard`
- Lists all files in `csv-error` folder
- Shows error details for each row
- "Reprocess" button for each file/row
- "Reprocess All" button for batch operations
- Status indicators (pending, processing, success, failed)

**Pros:**
- User-friendly interface
- Visual feedback
- Easy to use for non-technical users

**Cons:**
- Requires frontend development
- Needs API endpoints for file listing

## Idea 5: Email Notification with Reprocessing Link
**Description:** Send email notifications to integration engineers with direct reprocessing links.

**Implementation:**
- When rows fail, send email via SendGrid/Azure Communication Services
- Email contains:
  - Error file name
  - Error details
  - Direct reprocessing link (one-click)
  - Link to error file in blob storage
- Reprocessing link calls API endpoint to reprocess

**Pros:**
- Immediate notification
- One-click reprocessing
- Good for critical errors

**Cons:**
- Requires email service setup
- May generate many emails for large failures

## Idea 6: Metadata File for Error Context
**Description:** Store error metadata alongside error CSV files for better context.

**Implementation:**
- When saving error row, also create `{filename}.error.json`:
  ```json
  {
    "originalFile": "transport-123.csv",
    "rowNumber": 5,
    "error": "Type mismatch: 'abc' cannot be converted to INT",
    "errorTime": "2025-11-17T10:30:00Z",
    "rowData": { "id": "1", "name": "Test", ... },
    "suggestedFix": "Change 'abc' to numeric value"
  }
  ```
- Reprocessing UI shows this metadata
- Helps engineers understand what to fix

**Pros:**
- Rich context for debugging
- Helps identify patterns
- Better error tracking

**Cons:**
- Additional storage
- More complex error handling

## Idea 7: Validation Rules Configuration
**Description:** Allow engineers to configure validation rules and auto-fix common issues.

**Implementation:**
- Configuration table: `ValidationRules`
- Rules define: column name, expected type, transformation function
- Example: "If age column contains 'N/A', replace with NULL"
- Reprocessing applies rules before validation
- Reduces manual fixes needed

**Pros:**
- Reduces recurring errors
- Self-healing system
- Configurable without code changes

**Cons:**
- Complex to implement
- Risk of incorrect transformations
- Requires careful testing

## Recommended Approach: Hybrid Solution

**Phase 1 (Quick Win):**
- Implement Idea 2: Reprocessing API endpoint
- Implement Idea 4: Basic reprocessing dashboard
- Implement Idea 6: Error metadata files

**Phase 2 (Enhancement):**
- Implement Idea 3: Automatic retry for transient errors
- Add Idea 5: Email notifications for critical failures

**Phase 3 (Advanced):**
- Implement Idea 7: Validation rules configuration
- Add analytics dashboard for error patterns

## Implementation Notes

### Error File Naming Convention
- Format: `{originalFilename}_row{rowNumber}_error_{timestamp}.csv`
- Example: `transport-123_row5_error_20251117-103000.csv`
- Makes it easy to identify original file and row

### Reprocessing Workflow
1. Engineer reviews error file and metadata
2. Fixes source data/system issue
3. Clicks "Reprocess" in dashboard or calls API
4. File moved from `csv-error` to `csv-incoming`
5. Function processes file again
6. Success: File moved to `csv-processed`
7. Failure: New error file created with updated error details

### Safety Measures
- Prevent infinite loops: Max reprocessing attempts per file
- Audit trail: Log all reprocessing attempts
- Validation: Re-validate before reprocessing
- Rollback: Ability to undo reprocessing if needed

