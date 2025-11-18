# MessageBox Pattern Architecture

## Overview

The MessageBox is a central staging area (similar to Microsoft BizTalk Server) that ensures **guaranteed delivery** of data. All data flows through the MessageBox, enabling event-driven processing and reliable message routing.

## Core Concepts

### Debatching

Source adapters **debatch** data into individual records:
- Each record becomes a separate message in MessageBox
- Messages are independent and can be processed separately
- Enables parallel processing and error isolation

### Event-Driven Processing

When a message is added to MessageBox:
1. An event is triggered in the Event Queue
2. Destination adapters subscribe to messages
3. Each adapter processes messages independently
4. Subscriptions track processing status

### Guaranteed Delivery

Messages remain in MessageBox until **all** subscribing destination adapters have successfully processed them:
- If one destination fails, others can still process
- Failed messages remain for retry
- No data loss until all destinations confirm

## Database Schema

### Messages Table

Stores individual messages (debatched records):

```sql
CREATE TABLE Messages (
    MessageId UNIQUEIDENTIFIER PRIMARY KEY,
    InterfaceName NVARCHAR(255) NOT NULL,
    AdapterName NVARCHAR(100) NOT NULL,
    AdapterType NVARCHAR(50) NOT NULL, -- "Source" or "Destination"
    AdapterInstanceGuid UNIQUEIDENTIFIER NOT NULL,
    MessageData NVARCHAR(MAX) NOT NULL, -- JSON: {"headers": [...], "record": {...}}
    Status NVARCHAR(50) NOT NULL, -- "Pending", "Processed", "Error"
    datetime_created DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

### MessageSubscriptions Table

Tracks which adapters have processed which messages:

```sql
CREATE TABLE MessageSubscriptions (
    SubscriptionId UNIQUEIDENTIFIER PRIMARY KEY,
    MessageId UNIQUEIDENTIFIER NOT NULL,
    SubscriberAdapterName NVARCHAR(100) NOT NULL,
    InterfaceName NVARCHAR(255) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- "Pending", "Processed", "Error"
    datetime_created DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    datetime_processed DATETIME2 NULL,
    ProcessingDetails NVARCHAR(MAX) NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    FOREIGN KEY (MessageId) REFERENCES Messages(MessageId)
);
```

### AdapterInstances Table

Maintains metadata about adapter instances:

```sql
CREATE TABLE AdapterInstances (
    AdapterInstanceGuid UNIQUEIDENTIFIER PRIMARY KEY,
    InterfaceName NVARCHAR(255) NOT NULL,
    InstanceName NVARCHAR(255) NOT NULL,
    AdapterName NVARCHAR(100) NOT NULL,
    AdapterType NVARCHAR(50) NOT NULL,
    IsEnabled BIT NOT NULL DEFAULT 1,
    datetime_created DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

## Message Flow

### Step 1: Source Adapter Writes to MessageBox

```
Source Adapter (e.g., CsvAdapter)
    │
    ├─> Reads data (e.g., CSV file with 100 rows)
    │
    ├─> Debatches: Creates 100 separate messages
    │
    └─> For each record:
        │
        ├─> Creates message in Messages table
        │   • MessageId (GUID)
        │   • InterfaceName
        │   • AdapterName ("CSV")
        │   • AdapterType ("Source")
        │   • AdapterInstanceGuid
        │   • MessageData (JSON)
        │   • Status ("Pending")
        │
        └─> Triggers event in Event Queue
```

### Step 2: Destination Adapter Subscribes

```
Destination Adapter (e.g., SqlServerAdapter)
    │
    ├─> Reads pending messages from MessageBox
    │   • Filters by InterfaceName
    │   • Status = "Pending"
    │
    └─> Creates subscription for each message
        • MessageId
        • SubscriberAdapterName ("SqlServer")
        • Status ("Pending")
```

### Step 3: Destination Adapter Processes

```
Destination Adapter
    │
    ├─> For each message:
    │   │
    │   ├─> Extracts record from MessageData JSON
    │   │
    │   ├─> Processes record (validates, transforms, writes)
    │   │
    │   └─> Marks subscription as "Processed"
    │       • Updates Status = "Processed"
    │       • Sets datetime_processed
    │
    └─> If processing fails:
        └─> Marks subscription as "Error"
            • Updates Status = "Error"
            • Sets ErrorMessage
```

### Step 4: Message Removal

```
After each subscription is processed:
    │
    ├─> System checks MessageSubscriptions
    │   • Query: All subscriptions for MessageId
    │
    ├─> Evaluates: Are ALL subscriptions "Processed"?
    │   │
    │   ├─> YES → Removes message from MessageBox
    │   │
    │   └─> NO → Message stays (guaranteed delivery)
```

## Multiple Destinations

The MessageBox pattern supports **one source → multiple destinations**:

```
Source: CSV (100 rows)
    │
    └─> Creates 100 messages in MessageBox

Destination 1: SqlServerAdapter
    ├─> Creates 100 subscriptions
    ├─> Processes all messages
    └─> Marks all subscriptions as "Processed"

Destination 2: CsvAdapter (as destination)
    ├─> Creates 100 subscriptions
    ├─> Processes all messages
    └─> Marks all subscriptions as "Processed"

System checks:
    ├─> Message 1: SqlServer="Processed", CSV="Processed" → ✅ Remove
    ├─> Message 2: SqlServer="Processed", CSV="Processed" → ✅ Remove
    └─> ... (all messages removed)
```

## Error Handling

### Partial Failure Scenario

```
Message 50 processing:
    │
    ├─> SqlServerAdapter: ✅ Success → "Processed"
    │
    └─> CsvAdapter: ❌ Error → "Error"
        │
        └─> Message stays in MessageBox
            • SqlServer already processed (no data loss)
            • CsvAdapter can retry later
            • Guaranteed delivery maintained
```

## Benefits

- ✅ **Guaranteed Delivery**: Data never lost
- ✅ **Multiple Destinations**: One source feeds multiple destinations
- ✅ **Error Isolation**: One failure doesn't affect others
- ✅ **Audit Trail**: Complete processing history
- ✅ **Retry Capability**: Failed messages can be reprocessed
- ✅ **Scalability**: Independent message processing

## Implementation

The `MessageBoxService` implements the `IMessageBoxService` interface:

- `WriteSingleRecordMessageAsync()`: Writes one debatched record
- `WriteMessagesAsync()`: Writes multiple debatched records
- `ReadMessagesAsync()`: Reads pending messages
- `MarkMessageAsProcessedAsync()`: Marks subscription as processed
- `RemoveMessageAsync()`: Removes message after all subscriptions processed




