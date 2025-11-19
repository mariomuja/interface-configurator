<style>
strong, b {
  font-weight: 300 !important;
}
</style>

# 📊 Interface Configuration Demo

<div align="center">

[![Live Preview](https://img.shields.io/badge/🌐_Live_Preview-000000?style=for-the-badge&logo=vercel&logoColor=white)](https://interface-configurator.vercel.app)
[![Azure](https://img.shields.io/badge/Azure-0078D4?style=for-the-badge&logo=microsoft-azure&logoColor=white)](https://azure.microsoft.com)
[![Terraform](https://img.shields.io/badge/Terraform-7B42BC?style=for-the-badge&logo=terraform&logoColor=white)](https://www.terraform.io)
[![Angular](https://img.shields.io/badge/Angular-DD0031?style=for-the-badge&logo=angular&logoColor=white)](https://angular.io)
[![Vercel](https://img.shields.io/badge/Vercel-000000?style=for-the-badge&logo=vercel&logoColor=white)](https://vercel.com)

**A complete data integration workflow demonstrating modern cloud-native integration patterns**

[Features](#-integration-concepts-implemented) • [Architecture](#-architecture-overview) • [Deployment](#-terraform-azure-infrastructure) • [Contact](#-contact)

</div>

---

## 🎯 What This Application Demonstrates

This application demonstrates a revolutionary approach to **data integration**: **Configuration over Implementation**. Instead of writing custom code for each new interface between systems, you simply **configure** what you want to connect—and it just works. No new implementation artifacts required.

### The Vision: Configure, Don't Implement

**Traditional Approach (Implementation-Based):**
- Each new interface requires custom code
- Business logic mixed with integration logic
- High maintenance overhead
- Difficult to scale

**This Approach (Configuration-Based):**
- **Tell the system what to connect** (e.g., "CSV → SQL Server" or "SQL Server → SAP")
- **Use the same code** for all interfaces
- **Zero implementation effort** for new interfaces
- **Pluggable adapters** handle the complexity

This application showcases a complete **data integration workflow** from CSV files to SQL Server database, demonstrating how a **pluggable adapter architecture** with **event-driven MessageBox pattern** enables true configuration-based integration. The same adapters can be used as both source and destination, and the **MessageBox ensures guaranteed delivery**—data stays in the staging area until all destination adapters have successfully processed it.

## 🚀 Integration Concepts Implemented

### 1. **Event-Driven Architecture**
- **Blob Storage Trigger**: Azure Function automatically triggered when CSV files are uploaded to Blob Storage
- **Asynchronous Processing**: Non-blocking data processing pipeline
- **Event Logging**: Comprehensive process logging for monitoring and debugging

### 2. **Dynamic Schema Management**
- **Schema-on-Write**: SQL table structure automatically adapts to CSV column structure
- **Dynamic Column Creation**: New CSV columns automatically create corresponding SQL columns
- **Type Inference**: Automatic data type detection and conversion (string, integer, decimal, date)
- **Schema Evolution**: Handles CSV schema changes without manual database migrations

### 3. **Row-Level Error Handling**
- **Type Validation**: Validates data types before insertion
- **Failed Row Isolation**: Individual failed rows saved as separate CSV files in error folder
- **Success/Failure Tracking**: Only successfully processed rows are inserted; failed rows are preserved for reprocessing
- **Error Details Logging**: Comprehensive error logging with exception details for troubleshooting

### 4. **Infrastructure as Code (IaC)**
- **Terraform**: Complete Azure infrastructure defined as code
- **Reproducible Deployments**: Infrastructure can be recreated identically across environments
- **Version Control**: All infrastructure changes tracked in Git
- **Automated Provisioning**: Single command deploys entire infrastructure stack

### 5. **Multi-Platform Architecture**
- **Frontend**: Angular application deployed on Vercel
- **Backend API**: Serverless functions on Vercel
- **Data Processing**: Azure Functions (C# .NET isolated runtime)
- **Storage**: Azure Blob Storage for CSV files
- **Database**: Azure SQL Database with dynamic schema

### 6. **Internationalization (i18n)**
- **5 Languages**: German, English, French, Spanish, Italian
- **Runtime Language Switching**: Users can change language without page reload
- **Persistent Language Preference**: Language selection saved in browser localStorage

### 7. **Data Quality & Validation**
- **Type Safety**: Automatic type detection and conversion
- **Data Integrity**: GUID primary keys (no IDENTITY columns)
- **Audit Trail**: `datetime_created` column with automatic timestamp on all tables
- **Error Recovery**: Failed rows preserved for manual review and reprocessing

### 8. **Configuration-Based Integration Architecture**
- **Configure, Don't Implement**: Define interfaces by configuration, not code
- **Zero Implementation Overhead**: Adding a new interface (e.g., "JSON → SAP") requires only configuration—no new code
- **Reusable Adapters**: Same adapter code works for all interfaces
- **Universal Adapters**: Each adapter can be used as both source and destination
  - `CsvAdapter` can read CSV files (source) or write CSV files (destination)
  - `SqlServerAdapter` can read from SQL tables (source) or write to SQL tables (destination)
  - Future adapters (JSON, SAP, REST APIs) follow the same pattern
- **Pluggable Architecture**: Swap adapters without changing core logic
- **Unified Interface**: All adapters implement `ReadAsync()`, `WriteAsync()`, `GetSchemaAsync()`, and `EnsureDestinationStructureAsync()`

### 9. **MessageBox Pattern for Guaranteed Delivery**
- **Staging Area**: All data flows through a central MessageBox (similar to Microsoft BizTalk Server)
- **Debatching**: Each record is stored as a separate message for individual processing
- **Event-Driven Processing**: When a message is added, events trigger destination adapters
- **Guaranteed Delivery**: Messages remain in MessageBox until **all** subscribing destination adapters have successfully processed them
- **Subscription Tracking**: Tracks which adapters have processed which messages
- **Error Isolation**: If one destination adapter fails, others can still process the message
- **No Data Loss**: Data is never removed until all destinations confirm successful processing

### 10. **Modern Development Practices**
- **Clean Architecture**: Separation of concerns (Services, Models, Data Access, Adapters)
- **Dependency Injection**: Loose coupling and testability
- **Error Handling**: Comprehensive exception handling with detailed logging
- **Code Standards**: Consistent coding patterns and documentation
- **Design Patterns**: Adapter Pattern for data source/destination abstraction

## 🏗️ Architecture Overview

The application uses a multi-platform infrastructure with a **configuration-based, event-driven architecture**:

- **Frontend**: Deployed on Vercel (Angular application with serverless functions)
- **Backend**: Deployed on Vercel serverless functions
- **Database**: Azure SQL Database (main database + MessageBox staging database)
- **Storage**: Azure Storage Accounts
- **Processing**: Azure Function App for serverless functions
- **MessageBox**: Central staging area for guaranteed delivery (Azure SQL Database)

## 📊 Complete Dataflow Diagram

The following diagram illustrates the complete end-to-end dataflow through the system, showing how data moves from source systems through the MessageBox to destination systems:

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                          COMPLETE SYSTEM DATAFLOW                                    │
└─────────────────────────────────────────────────────────────────────────────────────┘

┌──────────────────┐         ┌──────────────────┐         ┌──────────────────┐
│   Data Sources   │         │   Data Sources   │         │   Data Sources   │
│                  │         │                  │         │                  │
│  • CSV Files     │         │  • SQL Tables    │         │  • SFTP Servers  │
│  • Blob Storage  │         │  • Azure SQL DB  │         │  • REST APIs     │
│  • File Shares   │         │  • On-Prem SQL   │         │  • JSON Files    │
└────────┬─────────┘         └────────┬─────────┘         └────────┬─────────┘
         │                            │                            │
         │                            │                            │
         ▼                            ▼                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        SOURCE ADAPTER LAYER                                 │
│                                                                             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                │
│  │ CsvAdapter   │    │SqlServer     │    │ Future        │                │
│  │              │    │Adapter       │    │ Adapters      │                │
│  │ • RAW        │    │              │    │               │                │
│  │ • FILE       │    │ • Polling    │    │ • JSON        │                │
│  │ • SFTP       │    │ • Connection │    │ • SAP         │                │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘                │
│         │                   │                    │                        │
│         └───────────────────┴────────────────────┘                        │
│                              │                                             │
│                    ReadAsync() + Debatches                                 │
│                              │                                             │
└──────────────────────────────┼─────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         MESSAGEBOX (STAGING AREA)                          │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    Messages Table                                   │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │ MessageId │ InterfaceName │ AdapterName │ MessageData │ Status│  │  │
│  │  │ (GUID)    │ (String)      │ (String)    │ (JSON)      │(Enum) │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  MessageData Format:                                                │  │
│  │  {                                                                  │  │
│  │    "headers": ["Column1", "Column2", ...],                         │  │
│  │    "record": {"Column1": "Value1", "Column2": "Value2", ...}       │  │
│  │  }                                                                  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │              MessageSubscriptions Table                              │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │SubId│MessageId│SubscriberAdapter│Status│ProcessedAt│ErrorMsg│  │  │
│  │  │(GUID│(GUID)   │(String)         │(Enum)│(DateTime) │(String)│  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  Tracks which adapters have processed which messages                │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │              AdapterInstances Table                                  │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │InstanceGuid│InterfaceName│InstanceName│AdapterName│IsEnabled│  │  │
│  │  │(GUID)      │(String)     │(String)    │(String)   │(Bool)   │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  Maintains metadata about adapter instances                        │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  Event Queue (InMemoryEventQueue)                                           │
│  • Triggers when messages are added                                         │
│  • Notifies destination adapters                                            │
│  • Enables event-driven processing                                          │
└──────────────────────────────┬─────────────────────────────────────────────┘
                               │
                               │ ReadPendingMessages()
                               │ CreateSubscription()
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      DESTINATION ADAPTER LAYER                              │
│                                                                             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                │
│  │ CsvAdapter   │    │SqlServer     │    │ Future        │                │
│  │              │    │Adapter       │    │ Adapters      │                │
│  │ • Write CSV  │    │              │    │               │                │
│  │ • File Mask  │    │ • Write SQL  │    │ • JSON        │                │
│  │ • Batch Size │    │ • Transactions│    │ • SAP         │                │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘                │
│         │                   │                    │                        │
│         └───────────────────┴────────────────────┘                        │
│                              │                                             │
│                    WriteAsync() + MarkSubscriptionProcessed()               │
│                              │                                             │
└──────────────────────────────┼─────────────────────────────────────────────┘
                               │
                               ▼
┌──────────────────┐         ┌──────────────────┐         ┌──────────────────┐
│  Data Destinations│         │  Data Destinations│         │  Data Destinations│
│                  │         │                  │         │                  │
│  • CSV Files     │         │  • SQL Tables    │         │  • SFTP Servers  │
│  • Blob Storage  │         │  • Azure SQL DB  │         │  • REST APIs     │
│  • File Shares   │         │  • On-Prem SQL   │         │  • JSON Files    │
└──────────────────┘         └──────────────────┘         └──────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                    GUARANTEED DELIVERY CHECK                                │
│                                                                             │
│  After each subscription is marked "Processed":                             │
│                                                                             │
│  1. Query MessageSubscriptions for all subscriptions of MessageId           │
│  2. Check: Are ALL subscriptions "Processed"?                              │
│     ├─ YES → Remove message from MessageBox ✅                              │
│     └─ NO  → Keep message in MessageBox (waiting for remaining adapters)  │
│                                                                             │
│  This ensures:                                                               │
│  • No data loss until all destinations confirm                             │
│  • Multiple destinations can process independently                          │
│  • Failed destinations don't block successful ones                          │
│  • Complete audit trail of processing                                      │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                    CONFIGURATION LAYER (Runtime)                             │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │         Interface Configuration (JSON in Blob Storage)              │  │
│  │  {                                                                  │  │
│  │    "interfaceName": "FromCsvToSqlServerExample",                    │  │
│  │    "sourceAdapterName": "CSV",                                      │  │
│  │    "sourceConfiguration": {...},                                     │  │
│  │    "destinationAdapterName": "SqlServer",                           │  │
│  │    "destinationConfiguration": {...},                               │  │
│  │    "sourceIsEnabled": true,                                         │  │
│  │    "destinationIsEnabled": true                                     │  │
│  │  }                                                                  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  • Loaded into memory cache on startup                                     │
│  • Updated via API without redeployment                                    │
│  • Controls adapter behavior and properties                                 │
│  • Enables/disables adapters independently                                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key Dataflow Steps

1. **Source Adapter Processing** (Timer-Triggered Azure Function):
   - Loads enabled interface configurations
   - Instantiates source adapters based on configuration
   - Calls `ReadAsync()` to read data from source system
   - **Debatches** data: Each record becomes a separate message
   - Writes messages to MessageBox (`Messages` table)
   - Triggers events in Event Queue

2. **MessageBox Staging**:
   - Stores each debatched record as a separate message
   - Messages contain JSON data with headers and record values
   - Status tracked: "Pending", "Processed", "Error"
   - Event Queue notifies destination adapters

3. **Destination Adapter Processing** (Timer-Triggered Azure Function):
   - Loads enabled interface configurations
   - Instantiates destination adapters based on configuration
   - Reads pending messages from MessageBox
   - Creates subscriptions in `MessageSubscriptions` table
   - Processes each message:
     - Extracts record from JSON
     - Validates and transforms data
     - Ensures destination structure exists
     - Writes to destination system
   - Marks subscription as "Processed" or "Error"

4. **Guaranteed Delivery Check**:
   - After each subscription is processed, system checks:
     - Are ALL subscriptions for this message "Processed"?
     - If YES: Remove message from MessageBox
     - If NO: Keep message (waiting for remaining adapters)

5. **Multiple Destinations Support**:
   - One source can feed multiple destinations
   - Each destination creates its own subscription
   - Messages remain until ALL destinations confirm processing
   - Failed destinations don't block successful ones

### Configuration-Based Architecture

The system uses a **configuration-based approach** where interfaces are defined by **what you want to connect**, not by writing custom code:

```
┌─────────────────────────────────────────────────────────────┐
│                    Configuration Layer                       │
│  "Connect CSV → SQL Server"  (Just tell it what to do)     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    MessageBox (Staging Area)                 │
│  • Debatching: Each record = separate message               │
│  • Event-driven: Triggers destination adapters              │
│  • Guaranteed delivery: Data stays until all processed      │
└──────────────┬──────────────────────────┬───────────────────┘
               │                          │
        ┌──────▼──────┐          ┌────────▼────────┐
        │   Source    │          │   Destination   │
        │   Adapter   │          │    Adapter      │
        └──────┬──────┘          └────────┬────────┘
               │                          │
        ┌──────▼──────┐          ┌────────▼────────┐
        │    CSV      │          │   SQL Server    │
        │  Adapter    │          │    Adapter      │
        │             │          │                 │
        │ Can be used │          │  Can be used    │
        │ as Source   │          │  as Destination │
        │ OR          │          │  OR             │
        │ Destination │          │  Source         │
        └─────────────┘          └─────────────────┘
```

**Key Innovation: Universal Adapters**

Each adapter can be used as **both source and destination**:

- **`CsvAdapter`**:
  - **As Source**: Reads CSV files → debatches → writes to MessageBox
  - **As Destination**: Reads from MessageBox → writes CSV files
  - Same code, different role based on configuration

- **`SqlServerAdapter`**:
  - **As Source**: Reads SQL tables → debatches → writes to MessageBox
  - **As Destination**: Reads from MessageBox → writes to SQL tables
  - Same code, different role based on configuration

**Example Configurations (Zero Code Changes):**

1. **CSV → SQL Server**: `CsvAdapter` (source) → MessageBox → `SqlServerAdapter` (destination)
2. **SQL Server → CSV**: `SqlServerAdapter` (source) → MessageBox → `CsvAdapter` (destination)
3. **SQL Server → SQL Server**: `SqlServerAdapter` (source) → MessageBox → `SqlServerAdapter` (destination)
4. **Future: CSV → SAP**: `CsvAdapter` (source) → MessageBox → `SapAdapter` (destination) *(no changes to existing code)*

### MessageBox: Guaranteed Delivery Pattern

The **MessageBox** acts as a staging area (similar to Microsoft BizTalk Server) ensuring **guaranteed delivery**:

```
┌─────────────────────────────────────────────────────────────┐
│                      MessageBox Flow                        │
└─────────────────────────────────────────────────────────────┘

1. Source Adapter Reads Data
   └─> Debatches into individual records
   └─> Each record = separate message in MessageBox
   └─> Event triggered for each message

2. Event Queue
   └─> Destination adapters subscribe to messages
   └─> Each adapter creates a subscription

3. Destination Adapter Processes
   └─> Reads message from MessageBox
   └─> Processes record
   └─> Marks subscription as "Processed"

4. Message Removal (Only After All Processed)
   └─> System checks: Are ALL subscriptions processed?
   └─> If YES: Message removed from MessageBox
   └─> If NO: Message stays (guaranteed delivery)
```

### Detailed Architecture Flow

Here's the complete end-to-end flow of how data moves through the system:

#### Step 1: Source Adapter Reads and Debatches

```
┌─────────────────────────────────────────────────────────────┐
│                    Step 1: Source Processing               │
└─────────────────────────────────────────────────────────────┘

Source Adapter (e.g., CsvAdapter)
    │
    ├─> Reads data from source (CSV file, SQL table, etc.)
    │
    ├─> Debatches: Splits batch into individual records
    │   Example: 100 rows → 100 separate messages
    │
    └─> For each record:
        │
        ├─> Creates message in MessageBox
        │   • MessageId (unique GUID)
        │   • InterfaceName (e.g., "FromCsvToSqlServerExample")
        │   • AdapterName (e.g., "CSV")
        │   • AdapterType ("Source")
        │   • MessageData (JSON: {"headers": [...], "record": {...}})
        │   • Status ("Pending")
        │
        └─> Triggers event in Event Queue
            • MessageId
            • InterfaceName
            • EnqueuedAt timestamp
```

#### Step 2: Event-Driven Subscription

```
┌─────────────────────────────────────────────────────────────┐
│              Step 2: Event Queue & Subscription             │
└─────────────────────────────────────────────────────────────┘

Event Queue (InMemoryEventQueue)
    │
    ├─> Receives event for each new message
    │   • MessageId
    │   • InterfaceName
    │
    └─> Destination adapters poll/consume events
        │
        └─> For each destination adapter:
            │
            ├─> Reads pending messages from MessageBox
            │   • Filters by InterfaceName
            │   • Status = "Pending"
            │
            └─> Creates subscription in MessageSubscriptions table
                • MessageId
                • SubscriberAdapterName (e.g., "SqlServer")
                • Status ("Pending")
                • InterfaceName
```

#### Step 3: Destination Adapter Processing

```
┌─────────────────────────────────────────────────────────────┐
│            Step 3: Destination Processing                   │
└─────────────────────────────────────────────────────────────┘

Destination Adapter (e.g., SqlServerAdapter)
    │
    ├─> Reads messages from MessageBox
    │   • Filters by InterfaceName and Status="Pending"
    │   • Orders by datetime_created (oldest first)
    │
    ├─> For each message:
    │   │
    │   ├─> Extracts single record from message
    │   │   • Parses JSON: {"headers": [...], "record": {...}}
    │   │
    │   ├─> Processes record
    │   │   • Validates data types
    │   │   • Ensures destination structure
    │   │   • Writes to destination (SQL table, CSV file, etc.)
    │   │
    │   └─> Marks subscription as "Processed"
    │       • Updates MessageSubscriptions.Status = "Processed"
    │       • Sets datetime_processed
    │       • Adds ProcessingDetails
    │
    └─> If processing fails:
        └─> Marks subscription as "Error"
            • Updates MessageSubscriptions.Status = "Error"
            • Sets ErrorMessage
            • Message remains in MessageBox for retry
```

#### Step 4: Guaranteed Delivery Check

```
┌─────────────────────────────────────────────────────────────┐
│         Step 4: Message Removal (Guaranteed Delivery)        │
└─────────────────────────────────────────────────────────────┘

After each subscription is marked as "Processed":
    │
    ├─> System checks MessageSubscriptions table
    │   • Query: All subscriptions for this MessageId
    │
    ├─> Evaluates: Are ALL subscriptions "Processed"?
    │   │
    │   ├─> YES (All processed):
    │   │   │
    │   │   └─> Removes message from MessageBox
    │   │       • Message deleted from Messages table
    │   │       • Guaranteed delivery confirmed
    │   │
    │   └─> NO (Some still pending):
    │       │
    │       └─> Message stays in MessageBox
    │           • Status remains "Pending"
    │           • Waiting for remaining adapters
    │           • Guaranteed delivery in progress
```

#### Complete Flow Example: CSV → SQL Server

```
┌─────────────────────────────────────────────────────────────┐
│         Example: CSV → SQL Server Integration               │
└─────────────────────────────────────────────────────────────┘

1. CSV file uploaded to Blob Storage
   │
   └─> Azure Function triggered (Blob Trigger)

2. CsvAdapter.ReadAsync() called
   │
   ├─> Reads CSV file (100 rows)
   │
   └─> Debatches: Creates 100 messages in MessageBox
       │
       └─> Each message:
           • MessageId: {unique-guid}
           • InterfaceName: "FromCsvToSqlServerExample"
           • AdapterName: "CSV"
           • AdapterType: "Source"
           • MessageData: {"headers": ["Name", "Age"], "record": {"Name": "John", "Age": "30"}}
           • Status: "Pending"
           • Event enqueued

3. SqlServerAdapter.WriteAsync() called
   │
   ├─> Reads 100 pending messages from MessageBox
   │
   ├─> Creates 100 subscriptions in MessageSubscriptions
   │   • MessageId: {message-guid}
   │   • SubscriberAdapterName: "SqlServer"
   │   • Status: "Pending"
   │
   ├─> Processes each message:
   │   │
   │   ├─> Extracts record from message
   │   │
   │   ├─> Validates data types
   │   │
   │   ├─> Ensures SQL table structure matches
   │   │
   │   ├─> Inserts row into SQL Server
   │   │
   │   └─> Marks subscription as "Processed"
   │
   └─> After all 100 subscriptions processed:
       │
       └─> System checks: All subscriptions = "Processed"?
           │
           └─> YES → Removes all 100 messages from MessageBox
               • Guaranteed delivery confirmed
               • No data loss
```

#### Multiple Destinations Example

```
┌─────────────────────────────────────────────────────────────┐
│      Example: One Source → Multiple Destinations            │
└─────────────────────────────────────────────────────────────┘

Scenario: CSV → SQL Server AND CSV → JSON File

1. CsvAdapter reads CSV (100 rows)
   └─> Creates 100 messages in MessageBox

2. SqlServerAdapter processes messages
   ├─> Creates 100 subscriptions (SubscriberAdapterName: "SqlServer")
   ├─> Processes all 100 messages
   └─> Marks all 100 subscriptions as "Processed"

3. CsvAdapter (as destination) processes messages
   ├─> Creates 100 subscriptions (SubscriberAdapterName: "CSV")
   ├─> Processes all 100 messages
   └─> Marks all 100 subscriptions as "Processed"

4. System checks MessageSubscriptions:
   ├─> Message 1: SqlServer="Processed", CSV="Processed" → ✅ Remove
   ├─> Message 2: SqlServer="Processed", CSV="Processed" → ✅ Remove
   └─> ... (all 100 messages removed)

5. If SqlServerAdapter fails for Message 50:
   ├─> Message 50: SqlServer="Error", CSV="Processed"
   ├─> Message stays in MessageBox (guaranteed delivery)
   ├─> CSV destination already processed (no data loss)
   └─> SqlServerAdapter can retry Message 50 later
```

**Benefits of MessageBox:**

- ✅ **Guaranteed Delivery**: Data never lost—stays until all destinations confirm
- ✅ **Multiple Destinations**: One source can feed multiple destinations
- ✅ **Error Isolation**: If one destination fails, others still process
- ✅ **Audit Trail**: Complete history of what was processed when
- ✅ **Retry Capability**: Failed messages can be reprocessed
- ✅ **Scalability**: Process messages independently and in parallel

**Benefits of Configuration-Based Approach:**

- 🚀 **Zero Implementation**: New interfaces = configuration only
- 🔄 **Reusability**: Same adapters work for all interfaces
- 🧪 **Testability**: Test adapters once, use everywhere
- 📈 **Scalability**: Add new adapters without touching existing code
- 🛠️ **Maintainability**: Changes isolated to adapter level
- ⚡ **Speed**: Deploy new interfaces in minutes, not weeks

## 🔧 Terraform (Azure Infrastructure)

### Prerequisites

1. **Terraform** >= 1.0 installed
2. **Azure Subscription** with appropriate permissions
3. **Service Principal** with Contributor role on the subscription

### Setup

1. **Create terraform.tfvars file**:
   ```bash
   cd terraform
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your Service Principal credentials
   ```

   **Alternative: Use Environment Variables** (recommended for CI/CD):
   ```bash
   # Windows PowerShell
   $env:ARM_SUBSCRIPTION_ID="your-subscription-id"
   $env:ARM_CLIENT_ID="your-client-id"
   $env:ARM_CLIENT_SECRET="your-client-secret"
   $env:ARM_TENANT_ID="your-tenant-id"

   # Linux/Mac
   export ARM_SUBSCRIPTION_ID="your-subscription-id"
   export ARM_CLIENT_ID="your-client-id"
   export ARM_CLIENT_SECRET="your-client-secret"
   export ARM_TENANT_ID="your-tenant-id"
   ```

   If using environment variables, set the authentication variables to `null` in `terraform.tfvars`:
   ```hcl
   subscription_id = null
   client_id       = null
   client_secret   = null
   tenant_id       = null
   ```

2. **Initialize Terraform**:
   ```bash
   cd terraform
   terraform init
   ```

3. **Review the plan**:
   ```bash
   terraform plan
   ```

4. **Apply the configuration**:
   ```bash
   terraform apply
   ```

### Variables

Key variables to configure in `terraform.tfvars`:

- `subscription_id`: Azure subscription ID
- `client_id`: Service Principal Client ID
- `client_secret`: Service Principal Client Secret
- `tenant_id`: Azure Tenant ID
- `sql_admin_login`: SQL Server administrator username
- `sql_admin_password`: SQL Server administrator password
- `jwt_secret`: JWT secret for authentication
- `environment`: Environment name (dev, staging, prod)
- `location`: Azure region (default: West Europe)

### Resources Created

- **Resource Group**: Container for all resources
- **Azure SQL Server**: Logical SQL Server container
- **Azure SQL Database**: Application database
- **Storage Account**: General purpose storage
- **Function App** (optional): Serverless functions
- **Functions Storage Account**: Storage for Azure Functions

### Outputs

After applying, Terraform outputs:
- SQL Server connection details
- Function App URL (if enabled)
- Storage account information
- Resource group name

## 📦 Vercel Configuration

The frontend is deployed to Vercel. Configuration is in `vercel/vercel.json`.

### Deployment

#### Azure Functions

Azure Functions are automatically deployed via GitHub Actions using the **"Run from Package"** method (Microsoft's recommended approach).

**Quick Setup:**
```powershell
# Windows
.\setup-github-secrets.ps1
```

```bash
# Linux/Mac
./setup-github-secrets.sh
```

**Documentation:**
- [GitHub Actions Deployment](./GITHUB_ACTIONS_DEPLOYMENT.md) - Complete guide
- [Setup GitHub Secrets](./SETUP_GITHUB_SECRETS.md) - Automated setup
- [Deployment Checklist](./DEPLOYMENT_CHECKLIST.md) - Step-by-step checklist
- [Documentation Index](./DOCUMENTATION_INDEX.md) - Complete documentation overview

#### Vercel

Vercel deployments are automatically triggered on git push to the main branch.

## 🔐 Environment Variables

### Frontend (Vercel)

Set in Vercel dashboard or via CLI:

- `DATABASE_URL`: SQL Server connection string
- `JWT_SECRET`: JWT secret for authentication
- `NODE_ENV`: Environment (production)

### Azure Functions

Configured via Terraform in Function App settings:

- `DATABASE_URL`: SQL Server connection string
- `NODE_ENV`: Environment
- `FUNCTIONS_WORKER_RUNTIME`: Node.js runtime

## 🔒 Security Considerations

- **Secrets**: Never commit `terraform.tfvars` with real values
- **Firewall Rules**: Configure SQL Server firewall to allow only necessary IPs
- **SSL/TLS**: All connections use SSL/TLS encryption
- **CORS**: Configure CORS origins appropriately
- **JWT Secrets**: Use strong, randomly generated secrets

## 💰 Cost Optimization

- Use appropriate SKU sizes for your workload
- Consider using Azure SQL Database Basic tier for development
- Use consumption plan for Function Apps when possible

## 🐛 Troubleshooting

### Terraform Issues

- **Authentication**: Verify Service Principal credentials are correct
- **Permissions**: Ensure Service Principal has Contributor role on subscription
- **Resource Names**: Some names must be globally unique
- **Subscription**: Verify subscription_id matches your Azure subscription

### Database Connection Issues

- **Firewall**: Check SQL Server firewall rules
- **SSL**: Ensure SSL mode is set correctly
- **Credentials**: Verify username and password

### Deployment Issues

- **Build Errors**: Check Node.js version compatibility
- **Environment Variables**: Verify all required variables are set
- **CORS**: Check CORS configuration matches frontend URL
- **Function App Deployment**: See [GITHUB_ACTIONS_DEPLOYMENT.md](./GITHUB_ACTIONS_DEPLOYMENT.md)
- **GitHub Secrets**: See [SETUP_GITHUB_SECRETS.md](./SETUP_GITHUB_SECRETS.md)

## 🔧 Maintenance

### Updates

1. Modify Terraform files as needed
2. Run `terraform plan` to review changes
3. Apply with `terraform apply`
4. Update documentation

### Backups

- SQL Database backups are configured automatically
- Consider additional backup strategies for production

## 📚 Support

For issues or questions:
- Check Terraform documentation: https://registry.terraform.io/providers/hashicorp/azurerm
- Azure documentation: https://docs.microsoft.com/azure
- Vercel documentation: https://vercel.com/docs

---

## 👤 Contact

<div align="center">

**Mario Muja**

**Call me:** +49 1520 464 14 73 / +39 345 345 00 98

[![Email](https://img.shields.io/badge/Email-D14836?style=for-the-badge&logo=gmail&logoColor=white)](mailto:mario.muja@gmail.com)
[![GitHub](https://img.shields.io/badge/GitHub-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/mariomuja)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/mario-muja-016782347)

</div>

---

<div align="center">

*This project demonstrates a revolutionary **configuration-based integration approach** where interfaces are defined by configuration, not implementation. The same code works for all interfaces—you simply configure what you want to connect, and it just works. The MessageBox pattern ensures guaranteed delivery, and universal adapters enable true plug-and-play integration.*

*Modern cloud-native integration patterns • Infrastructure as Code • Configuration over Implementation*

Made with ❤️ using Azure, Terraform, Angular, and Vercel

</div>
