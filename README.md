# 📊 Interface Configuration Demo

<!-- CI/CD Test #5: Testing ready branch workflow -->

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

The following diagram illustrates the complete end-to-end dataflow through the system, showing how data moves from source systems through **Azure Service Bus** to destination systems, with **dynamic Container Apps** for each adapter instance:

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                          COMPLETE SYSTEM DATAFLOW                                    │
│                    (Service Bus + Dynamic Container Apps)                          │
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
│              SOURCE ADAPTER INSTANCES (Container Apps)                     │
│                    Dynamically Created on Configuration                     │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  Container App: ca-{source-guid-1}                                  │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │ CsvAdapter Instance                                           │  │  │
│  │  │ • Reads CSV from Blob Storage                                 │  │  │
│  │  │ • Debatches into individual records                          │  │  │
│  │  │ • Publishes to Service Bus Topic                             │  │  │
│  │  │ • Isolated: Own blob storage, own config                      │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  Container App: ca-{source-guid-2}                                  │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │ SqlServerAdapter Instance                                    │  │  │
│  │  │ • Polls SQL Server tables                                   │  │  │
│  │  │ • Debatches into individual records                         │  │  │
│  │  │ • Publishes to Service Bus Topic                            │  │  │
│  │  │ • Isolated: Own connection, own config                       │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ✅ Benefits:                                                               │
│  • No Logic Apps needed - Container Apps created automatically            │
│  • Clean separation: Send and receive processes isolated                 │
│  • Fault isolation: Errors don't affect other adapter instances            │
│  • Performance isolation: Slow adapter doesn't block others                │
│  • Dynamic creation: No deployment needed - created on configuration      │
│                              │                                             │
│                    ReadAsync() + Debatches + Publish                        │
│                              │                                             │
└──────────────────────────────┼─────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    AZURE SERVICE BUS (Messaging Hub)                       │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    Service Bus Topics                                │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │ Topic: {InterfaceName}                                         │  │  │
│  │  │  ┌────────────────────────────────────────────────────────┐  │  │  │
│  │  │  │ Message Properties:                                    │  │  │  │
│  │  │  │ • MessageId (GUID)                                     │  │  │  │
│  │  │  │ • InterfaceName                                        │  │  │  │
│  │  │  │ • AdapterName                                          │  │  │  │
│  │  │  │ • MessageData (JSON)                                   │  │  │  │
│  │  │  │ • EnqueuedTime                                         │  │  │  │
│  │  │  └────────────────────────────────────────────────────────┘  │  │  │
│  │  │                                                              │  │  │
│  │  │  MessageData Format:                                         │  │  │
│  │  │  {                                                           │  │  │
│  │  │    "headers": ["Column1", "Column2", ...],                  │  │  │
│  │  │    "record": {"Column1": "Value1", "Column2": "Value2"}   │  │  │
│  │  │  }                                                           │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │              Service Bus Subscriptions                        │  │  │
│  │  │  ┌────────────────────────────────────────────────────────┐  │  │  │
│  │  │  │ Subscription: {DestinationAdapterGuid}                 │  │  │  │
│  │  │  │ • Filters messages by InterfaceName                     │  │  │  │
│  │  │  │ • Each destination adapter has own subscription          │  │  │  │
│  │  │  │ • Automatic message routing                             │  │  │  │
│  │  │  │ • Dead-letter queue for failed messages                 │  │  │  │
│  │  │  └────────────────────────────────────────────────────────┘  │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  🚀 Service Bus Features:                                           │  │
│  │  • Guaranteed Delivery: Messages persist until processed          │  │
│  │  • At-Least-Once Delivery: Messages delivered reliably            │  │
│  │  • Dead-Letter Queue: Failed messages automatically moved         │  │
│  │  • Message Ordering: FIFO ordering per subscription               │  │
│  │  • Filtering: Topic filters for message routing                   │  │
│  │  • Scaling: Auto-scales to handle high throughput                 │  │
│  │  • Durability: Messages survive system restarts                   │  │
│  │  • Multiple Subscriptions: One topic, many subscribers           │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │              AdapterInstances Table (InterfaceConfigDb)              │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │InstanceGuid│InterfaceName│InstanceName│AdapterName│IsEnabled│  │  │
│  │  │(GUID)      │(String)     │(String)    │(String)   │(Bool)   │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  │                                                                     │  │
│  │  Maintains metadata about adapter instances                        │  │
│  │  Used to create Container Apps and Service Bus subscriptions      │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────┬─────────────────────────────────────────────┘
                               │
                               │ Subscribe() + Receive() + Complete()
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│           DESTINATION ADAPTER INSTANCES (Container Apps)                   │
│                    Dynamically Created on Configuration                     │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  Container App: ca-{dest-guid-1}                                    │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │ SqlServerAdapter Instance                                     │  │  │
│  │  │ • Subscribes to Service Bus Topic                             │  │  │
│  │  │ • Receives messages from subscription                         │  │  │
│  │  │ • Writes to SQL Server tables                                 │  │  │
│  │  │ • Completes messages after successful write                   │  │  │
│  │  │ • Isolated: Own connection, own config, own processing       │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  Container App: ca-{dest-guid-2}                                    │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │ CsvAdapter Instance (as Destination)                            │  │  │
│  │  │ • Subscribes to Service Bus Topic                              │  │  │
│  │  │ • Receives messages from subscription                          │  │  │
│  │  │ • Writes CSV files to Blob Storage                             │  │  │
│  │  │ • Completes messages after successful write                    │  │  │
│  │  │ • Isolated: Own blob storage, own config                      │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ✅ Benefits:                                                               │
│  • No Logic Apps needed - Container Apps created automatically            │
│  • Clean separation: Each adapter instance runs independently              │
│  • Fault isolation: Error in one adapter doesn't affect others             │
│  • Performance isolation: Slow adapter doesn't block others                │
│  • Dynamic creation: No deployment needed - created on configuration       │
│                              │                                             │
│                    ReceiveAsync() + WriteAsync() + Complete()               │
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
│                    GUARANTEED DELIVERY (Service Bus)                        │
│                                                                             │
│  Service Bus ensures guaranteed delivery:                                   │
│                                                                             │
│  1. Message Published to Topic                                              │
│     └─> Message persisted in Service Bus                                    │
│                                                                             │
│  2. Multiple Subscriptions Receive                                          │
│     ├─> Subscription 1 (Destination Adapter 1) receives message            │
│     ├─> Subscription 2 (Destination Adapter 2) receives message           │
│     └─> Each subscription processes independently                           │
│                                                                             │
│  3. Message Completion                                                      │
│     ├─> After successful processing: Complete() called                      │
│     ├─> Message removed from subscription                                   │
│     └─> Other subscriptions still have access to message                    │
│                                                                             │
│  4. Error Handling                                                          │
│     ├─> If processing fails: Abandon() or DeadLetter() called              │
│     ├─> Message moved to dead-letter queue                                  │
│     └─> Can be reprocessed later                                            │
│                                                                             │
│  This ensures:                                                               │
│  • No data loss - Messages persist until processed                          │
│  • Multiple destinations can process independently                          │
│  • Failed destinations don't block successful ones                          │
│  • Automatic retry via dead-letter queue                                    │
│  • Complete audit trail via Service Bus metrics                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│              CONFIGURATION LAYER (Runtime - No Deployment)                    │
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
│  When user configures interface in UI:                                     │
│                                                                             │
│  1. Source Adapter Instance Created                                        │
│     ├─> Container App created automatically (ca-{guid})                   │
│     ├─> Blob storage created for adapter instance                         │
│     ├─> Adapter config stored in blob (adapter-config.json)               │
│     ├─> Service Bus Topic created (if not exists)                         │
│     └─> Container App starts processing                                    │
│                                                                             │
│  2. Destination Adapter Instance Created                                   │
│     ├─> Container App created automatically (ca-{guid})                    │
│     ├─> Blob storage created for adapter instance                         │
│     ├─> Adapter config stored in blob (adapter-config.json)               │
│     ├─> Service Bus Subscription created                                   │
│     └─> Container App starts processing                                    │
│                                                                             │
│  ✅ No Deployment Required:                                                │
│  • Container Apps created dynamically via Azure Resource Manager API       │
│  • No Logic Apps needed - Container Apps handle processing                │
│  • Configuration changes update Container Apps automatically                │
│  • Each adapter instance isolated in own Container App                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key Dataflow Steps

1. **Source Adapter Instance Creation** (On Configuration):
   - User configures source adapter in UI
   - **Container App created automatically** via Azure Resource Manager API
   - Blob storage created for adapter instance
   - Adapter configuration stored in blob (`adapter-config.json`)
   - Service Bus Topic created (if not exists)
   - Container App starts processing
   - **No deployment needed** - created dynamically

2. **Source Adapter Processing** (Container App):
   - Container App reads adapter configuration from blob storage
   - Instantiates source adapter based on configuration
   - Calls `ReadAsync()` to read data from source system
   - **Debatches** data: Each record becomes a separate message
   - Publishes messages to Service Bus Topic
   - Messages persist in Service Bus until processed

3. **Service Bus Messaging**:
   - Messages published to Topic named after InterfaceName
   - Each message contains JSON data with headers and record values
   - Service Bus ensures guaranteed delivery
   - Multiple subscriptions can receive same message
   - Dead-letter queue for failed messages
   - Auto-scaling handles high throughput

4. **Destination Adapter Instance Creation** (On Configuration):
   - User configures destination adapter in UI
   - **Container App created automatically** via Azure Resource Manager API
   - Blob storage created for adapter instance
   - Adapter configuration stored in blob (`adapter-config.json`)
   - Service Bus Subscription created for this adapter instance
   - Container App starts processing
   - **No deployment needed** - created dynamically

5. **Destination Adapter Processing** (Container App):
   - Container App reads adapter configuration from blob storage
   - Subscribes to Service Bus Topic via Subscription
   - Receives messages from Service Bus Subscription
   - Processes each message:
     - Extracts record from JSON
     - Validates and transforms data
     - Ensures destination structure exists
     - Writes to destination system
   - Completes message after successful processing
   - Abandons or dead-letters message on error

6. **Guaranteed Delivery** (Service Bus):
   - Service Bus ensures messages persist until processed
   - Each subscription processes messages independently
   - Failed messages moved to dead-letter queue
   - Messages can be reprocessed from dead-letter queue
   - Complete audit trail via Service Bus metrics

7. **Multiple Destinations Support**:
   - One source can feed multiple destinations
   - Each destination has its own Subscription
   - Each destination runs in its own Container App
   - Messages delivered to all subscriptions independently
   - Failed destinations don't block successful ones
   - Complete process isolation between adapter instances

### Configuration-Based Architecture with Dynamic Container Apps

The system uses a **configuration-based approach** where interfaces are defined by **what you want to connect**, not by writing custom code. **Container Apps are created dynamically** when you configure an interface:

```
┌─────────────────────────────────────────────────────────────┐
│                    Configuration Layer                       │
│  "Connect CSV → SQL Server"  (Just tell it what to do)       │
│                                                              │
│  User clicks "Save" in UI → Container Apps created          │
│  automatically via Azure Resource Manager API                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Source Container App Created                   │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Container App: ca-{source-guid}                       │  │
│  │ • Created automatically (no deployment)              │  │
│  │ • Own blob storage for adapter instance                │  │
│  │ • Adapter config: adapter-config.json                 │  │
│  │ • Reads from source → Publishes to Service Bus        │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    Azure Service Bus                         │
│  • Topic: {InterfaceName}                                    │
│  • Debatching: Each record = separate message                │
│  • Guaranteed delivery: Messages persist until processed    │
│  • Multiple subscriptions: One topic, many subscribers      │
│  • Dead-letter queue: Failed messages automatically moved   │
└──────────────┬──────────────────────────┬───────────────────┘
               │                          │
        ┌──────▼──────┐          ┌────────▼────────┐
        │  Container  │          │   Container     │
        │  App: ca-   │          │   App: ca-      │
        │  {guid-1}   │          │   {guid-2}      │
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
        
✅ Benefits:
• No Logic Apps needed - Container Apps handle processing
• Clean separation: Send and receive processes isolated
• Fault isolation: Errors don't affect other adapter instances
• Performance isolation: Slow adapter doesn't block others
• Dynamic creation: No deployment needed - created on configuration
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

### Azure Service Bus: Guaranteed Delivery Pattern

**Azure Service Bus** acts as the messaging hub (replacing MessageBox) ensuring **guaranteed delivery** with enterprise-grade features:

```
┌─────────────────────────────────────────────────────────────┐
│                    Service Bus Flow                          │
└─────────────────────────────────────────────────────────────┘

1. Source Container App Reads Data
   └─> Debatches into individual records
   └─> Each record = separate message
   └─> Publishes to Service Bus Topic

2. Service Bus Topic
   └─> Messages persisted in Service Bus
   └─> Multiple subscriptions can receive same message
   └─> Automatic message routing

3. Destination Container App Subscribes
   └─> Creates Service Bus Subscription
   └─> Receives messages from subscription
   └─> Processes record
   └─> Completes message after successful processing

4. Error Handling
   └─> If processing fails: Abandon() or DeadLetter()
   └─> Message moved to dead-letter queue
   └─> Can be reprocessed later

5. Message Completion
   └─> After successful processing: Complete() called
   └─> Message removed from subscription
   └─> Other subscriptions still have access
   └─> Guaranteed delivery confirmed
```

### Dynamic Container App Creation

**Container Apps are created automatically** when you configure an adapter instance - no deployment needed:

```
┌─────────────────────────────────────────────────────────────┐
│         Container App Creation Flow                          │
└─────────────────────────────────────────────────────────────┘

1. User Configures Adapter in UI
   └─> Clicks "Save" button
   └─> Backend receives configuration

2. Container App Creation (Automatic)
   ├─> Azure Resource Manager API called
   ├─> Container App created: ca-{adapter-instance-guid}
   ├─> Blob storage created for adapter instance
   ├─> Adapter config stored: adapter-config.json
   ├─> Environment variables configured
   └─> Container App starts processing

3. Service Bus Setup (Automatic)
   ├─> Topic created: {InterfaceName}
   ├─> Subscription created: {DestinationAdapterGuid}
   └─> Connection string configured

✅ No Deployment Required:
• Container Apps created dynamically
• No Logic Apps needed
• Configuration changes update Container Apps automatically
• Each adapter instance isolated in own Container App
```

### Detailed Architecture Flow

Here's the complete end-to-end flow of how data moves through the system:

#### Step 1: Container App Creation & Source Processing

```
┌─────────────────────────────────────────────────────────────┐
│      Step 1: Container App Creation & Source Processing     │
└─────────────────────────────────────────────────────────────┘

User Configures Source Adapter in UI
    │
    └─> Backend creates Container App automatically
        │
        ├─> Container App: ca-{source-adapter-guid}
        │   • Created via Azure Resource Manager API
        │   • Blob storage created for adapter instance
        │   • Adapter config stored: adapter-config.json
        │   • Environment variables configured
        │   • Container App starts processing
        │
        └─> Source Container App Processing
            │
            ├─> Reads adapter configuration from blob
            │   • Loads adapter-config.json
            │   • Configures adapter instance
            │
            ├─> Reads data from source (CSV file, SQL table, etc.)
            │
            ├─> Debatches: Splits batch into individual records
            │   Example: 100 rows → 100 separate messages
            │
            └─> For each record:
                │
                └─> Publishes message to Service Bus Topic
                    • Topic: {InterfaceName}
                    • MessageId (unique GUID)
                    • MessageData (JSON: {"headers": [...], "record": {...}})
                    • EnqueuedTime timestamp
                    • Message persisted in Service Bus
```

#### Step 2: Service Bus Topic & Subscription

```
┌─────────────────────────────────────────────────────────────┐
│         Step 2: Service Bus Topic & Subscription            │
└─────────────────────────────────────────────────────────────┘

Service Bus Topic: {InterfaceName}
    │
    ├─> Messages published by source adapter
    │   • Each message persisted in Service Bus
    │   • Multiple subscriptions can receive same message
    │   • Automatic message routing
    │
    └─> Service Bus Subscriptions Created
        │
        └─> For each destination adapter instance:
            │
            ├─> Subscription created: {DestinationAdapterGuid}
            │   • Filters messages by InterfaceName
            │   • Each destination has own subscription
            │   • Messages delivered independently
            │
            └─> Container App subscribes to messages
                • Receives messages from subscription
                • Processes messages independently
```

#### Step 3: Destination Container App Processing

```
┌─────────────────────────────────────────────────────────────┐
│     Step 3: Destination Container App Processing            │
└─────────────────────────────────────────────────────────────┘

User Configures Destination Adapter in UI
    │
    └─> Backend creates Container App automatically
        │
        ├─> Container App: ca-{dest-adapter-guid}
        │   • Created via Azure Resource Manager API
        │   • Blob storage created for adapter instance
        │   • Adapter config stored: adapter-config.json
        │   • Service Bus Subscription created
        │   • Container App starts processing
        │
        └─> Destination Container App Processing
            │
            ├─> Reads adapter configuration from blob
            │   • Loads adapter-config.json
            │   • Configures adapter instance
            │
            ├─> Subscribes to Service Bus Topic
            │   • Receives messages from subscription
            │   • Messages delivered independently
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
            │   └─> Completes message after successful processing
            │       • Complete() called on Service Bus receiver
            │       • Message removed from subscription
            │       • Other subscriptions still have access
            │
            └─> If processing fails:
                └─> Abandons or dead-letters message
                    • Abandon() or DeadLetter() called
                    • Message moved to dead-letter queue
                    • Can be reprocessed later
```

#### Step 4: Guaranteed Delivery (Service Bus)

```
┌─────────────────────────────────────────────────────────────┐
│      Step 4: Guaranteed Delivery (Service Bus)              │
└─────────────────────────────────────────────────────────────┘

Service Bus ensures guaranteed delivery:
    │
    ├─> Messages persist in Service Bus until processed
    │   • Messages survive system restarts
    │   • At-least-once delivery guaranteed
    │
    ├─> Multiple subscriptions process independently
    │   • Each destination has own subscription
    │   • Messages delivered to all subscriptions
    │   • Failed destinations don't block successful ones
    │
    ├─> Message completion
    │   • After successful processing: Complete() called
    │   • Message removed from subscription
    │   • Other subscriptions still have access
    │
    └─> Error handling
        • Failed messages moved to dead-letter queue
        • Can be reprocessed from dead-letter queue
        • Complete audit trail via Service Bus metrics
```

#### Complete Flow Example: CSV → SQL Server

```
┌─────────────────────────────────────────────────────────────┐
│      Example: CSV → SQL Server Integration                  │
│         (With Dynamic Container Apps)                        │
└─────────────────────────────────────────────────────────────┘

1. User Configures Interface in UI
   │
   ├─> Source: CSV Adapter
   │   └─> Container App created: ca-{csv-source-guid}
   │       • Blob storage created
   │       • Adapter config stored
   │       • Service Bus Topic created: "FromCsvToSqlServerExample"
   │       • Container App starts processing
   │
   └─> Destination: SQL Server Adapter
       └─> Container App created: ca-{sql-dest-guid}
           • Blob storage created
           • Adapter config stored
           • Service Bus Subscription created
           • Container App starts processing

2. CSV file uploaded to Blob Storage
   │
   └─> CSV Source Container App processes file
       │
       ├─> Reads CSV file (100 rows)
       │
       └─> Debatches: Publishes 100 messages to Service Bus Topic
           │
           └─> Each message:
               • Topic: "FromCsvToSqlServerExample"
               • MessageId: {unique-guid}
               • MessageData: {"headers": ["Name", "Age"], "record": {"Name": "John", "Age": "30"}}
               • EnqueuedTime timestamp
               • Message persisted in Service Bus

3. SQL Server Destination Container App subscribes
   │
   ├─> Receives 100 messages from Service Bus Subscription
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
   │   └─> Completes message after successful insert
   │       • Complete() called on Service Bus receiver
   │       • Message removed from subscription
   │
   └─> After all 100 messages processed:
       │
       └─> All messages completed successfully
           • Guaranteed delivery confirmed
           • No data loss
           • Complete audit trail via Service Bus metrics
```

#### Multiple Destinations Example

```
┌─────────────────────────────────────────────────────────────┐
│      Example: One Source → Multiple Destinations            │
│              (With Container App Isolation)                │
└─────────────────────────────────────────────────────────────┘

Scenario: CSV → SQL Server AND CSV → JSON File

1. CSV Source Container App reads CSV (100 rows)
   └─> Publishes 100 messages to Service Bus Topic
       • Topic: "FromCsvToSqlServerExample"
       • Messages persisted in Service Bus

2. SQL Server Destination Container App (ca-{sql-guid})
   ├─> Subscribes to Service Bus Topic
   ├─> Receives 100 messages from subscription
   ├─> Processes all 100 messages
   └─> Completes all 100 messages after successful processing
       • Messages removed from SQL Server subscription
       • Other subscriptions still have access

3. CSV Destination Container App (ca-{csv-guid})
   ├─> Subscribes to same Service Bus Topic
   ├─> Receives 100 messages from subscription
   ├─> Processes all 100 messages
   └─> Completes all 100 messages after successful processing
       • Messages removed from CSV subscription
       • SQL Server subscription already processed

4. Service Bus ensures delivery:
   ├─> Each subscription processes independently
   ├─> Messages delivered to all subscriptions
   └─> All messages processed successfully ✅

5. If SQL Server Container App fails for Message 50:
   ├─> Message 50: SQL Server abandons/dead-letters message
   ├─> CSV Container App still processes Message 50 successfully
   ├─> Message moved to dead-letter queue for SQL Server
   ├─> CSV destination already processed (no data loss)
   └─> SQL Server Container App can reprocess from dead-letter queue
```

**Benefits of Service Bus:**

- ✅ **Guaranteed Delivery**: Messages persist until processed
- ✅ **At-Least-Once Delivery**: Messages delivered reliably
- ✅ **Multiple Destinations**: One topic, many subscriptions
- ✅ **Error Isolation**: Failed destinations don't block successful ones
- ✅ **Dead-Letter Queue**: Failed messages automatically moved
- ✅ **Message Ordering**: FIFO ordering per subscription
- ✅ **Auto-Scaling**: Handles high throughput automatically
- ✅ **Durability**: Messages survive system restarts
- ✅ **Complete Audit Trail**: Service Bus metrics track everything

**Benefits of Dynamic Container Apps:**

- ✅ **No Logic Apps Needed**: Container Apps handle processing
- ✅ **Clean Separation**: Send and receive processes isolated
- ✅ **Fault Isolation**: Errors don't affect other adapter instances
- ✅ **Performance Isolation**: Slow adapter doesn't block others
- ✅ **Dynamic Creation**: No deployment needed - created on configuration
- ✅ **Independent Scaling**: Each Container App scales independently
- ✅ **Resource Isolation**: Each adapter has own resources

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

Azure Functions are deployed using the **"Run from Package"** method (Microsoft's recommended approach).

**Deployment:**
- Deploy manually using Azure Functions Core Tools or Azure Portal
- Configure deployment via your CI/CD pipeline
- See [Deployment Checklist](./DEPLOYMENT_CHECKLIST.md) for step-by-step instructions

**Documentation:**
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
- **Function App Deployment**: See [Deployment Checklist](./DEPLOYMENT_CHECKLIST.md) for deployment instructions

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

[![Email](https://img.shields.io/badge/Email-D14836?style=for-the-badge&logo=gmail&logoColor=white)](mailto:mariomuja@mariomujagmail508.onmicrosoft.com)
[![GitHub](https://img.shields.io/badge/GitHub-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/mariomuja)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/mario-muja-016782347)

</div>

---

<div align="center">

*This project demonstrates a revolutionary **configuration-based integration approach** where interfaces are defined by configuration, not implementation. The same code works for all interfaces—you simply configure what you want to connect, and it just works. The MessageBox pattern ensures guaranteed delivery, and universal adapters enable true plug-and-play integration.*

*Modern cloud-native integration patterns • Infrastructure as Code • Configuration over Implementation*

Made with ❤️ using Azure, Terraform, Angular, and Vercel

</div>
