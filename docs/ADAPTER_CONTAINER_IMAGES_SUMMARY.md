# Adapter Container Images - Content Summary

## Overview

The adapter container images are Docker images that contain a generic adapter runtime capable of running any of the 7 supported adapter types. All images share the same codebase but are tagged separately for clarity and version management.

**Registry:** `acrinterfaceconfig.azurecr.io`  
**Image Pattern:** `{adapter-type}-adapter:latest`  
**Base Image:** `mcr.microsoft.com/dotnet/aspnet:8.0` (Linux)  
**Runtime:** .NET 8.0  
**Image Size:** ~349 MB (all images)

---

## Supported Adapter Types

Each container image can run one of the following 7 adapter types:

### 1. CSV Adapter (`csv-adapter`)
- **Purpose:** Read and write CSV files from/to Azure Blob Storage
- **Source Mode:**
  - Reads CSV files from specified blob storage folders
  - Supports auto-detection of field separators
  - Handles multiple CSV files in a folder
  - Debatching: Converts each CSV row into individual Service Bus messages
- **Destination Mode:**
  - Writes data from Service Bus messages to CSV files
  - Creates files in blob storage with configurable naming patterns
  - Supports dynamic column mapping

### 2. SQL Server Adapter (`sqlserver-adapter`)
- **Purpose:** Read from and write to Azure SQL Database / SQL Server
- **Source Mode:**
  - Executes SQL queries to read data from tables
  - Supports parameterized queries
  - Debatching: Converts each row into individual Service Bus messages
- **Destination Mode:**
  - Writes records to SQL tables
  - Auto-creates tables if they don't exist
  - Dynamic schema generation based on data types
  - Batch inserts for performance
  - Transactional writes with rollback on error

### 3. SAP Adapter (`sap-adapter`)
- **Purpose:** Integration with SAP systems via multiple protocols
- **Source Mode:** (Planned)
- **Destination Mode:**
  - **OData:** Writes to SAP OData endpoints
  - **REST API:** HTTP-based integration with SAP systems
  - **RFC:** Remote Function Call integration
  - **IDOC:** Intermediate Document processing
- **Base Class:** Extends `HttpClientAdapterBase` for HTTP-based communication

### 4. Dynamics 365 Adapter (`dynamics365-adapter`)
- **Purpose:** Integration with Microsoft Dynamics 365
- **Source Mode:** (Planned)
- **Destination Mode:**
  - OData API integration
  - Authentication via OAuth 2.0 / Azure AD
  - CRUD operations on Dynamics 365 entities
- **Base Class:** Extends `HttpClientAdapterBase`

### 5. CRM Adapter (`crm-adapter`)
- **Purpose:** Integration with Microsoft CRM systems
- **Source Mode:** (Planned)
- **Destination Mode:**
  - Web API integration
  - Entity relationship management
- **Base Class:** Extends `HttpClientAdapterBase`

### 6. File Adapter (`file-adapter`)
- **Purpose:** Generic file operations in Azure Blob Storage
- **Source Mode:**
  - List files in blob storage folders
  - Read file contents
  - Supports file patterns and filters
  - CSV file reading with delimiter detection
- **Destination Mode:**
  - Write files to blob storage
  - Supports configurable file naming patterns
- **Integration:** Used internally by CSV adapter for blob storage operations

### 7. SFTP Adapter (`sftp-adapter`)
- **Purpose:** Secure File Transfer Protocol integration
- **Source Mode:**
  - Connect to SFTP servers
  - Download files from remote SFTP directories
  - Move files after processing
- **Destination Mode:**
  - Upload files to SFTP servers
  - Secure authentication via username/password or SSH keys
- **Features:**
  - Connection pooling
  - File buffer size configuration
  - Support for custom ports

---

## Container Contents

### Core Components

#### 1. Adapter Runtime (`docker/adapter-runtime/`)
- **Entry Point:** `adapter-runtime.dll` (main executable)
- **Program.cs:** Container startup logic
  - Reads configuration from environment variables
  - Loads adapter configuration from Azure Blob Storage (`adapter-config.json`)
  - Initializes the appropriate adapter instance based on `ADAPTER_NAME`
  - Hosts as ASP.NET Core web application (port 8080)

#### 2. Adapter Implementations (`adapters/`)
- **Base Classes:**
  - `AdapterBase`: Abstract base for all adapters
  - `HttpClientAdapterBase`: Base for HTTP-based adapters (SAP, Dynamics365, CRM)
- **Concrete Adapters:**
  - `CsvAdapter.cs`
  - `SqlServerAdapter.cs`
  - `SapAdapter.cs`
  - `Dynamics365Adapter.cs`
  - `CrmAdapter.cs`
  - `FileAdapter.cs`
  - `SftpAdapter.cs`

#### 3. Core Services (`main.Core/`)
- **Interfaces:**
  - `IAdapter`: Contract for all adapters
  - `IServiceBusService`: Azure Service Bus integration
  - `IStatisticsService`: Processing statistics tracking
- **Services:**
  - `ServiceBusService`: Message queue operations
  - `CsvProcessingService`: CSV parsing and validation
  - `CsvValidationService`: Delimiter detection, schema validation
  - `JQTransformationService`: JSON transformation using jq
  - `ProcessingStatisticsService`: Performance metrics and tracking
- **Models:**
  - `CsvColumnAnalyzer`: Data type detection
  - `ServiceBusMessage`: Message structure

---

## Key Dependencies

### .NET Packages (NuGet)
- **Azure SDK:**
  - `Azure.Storage.Blobs` (12.22.0) - Blob storage operations
  - `Azure.Messaging.ServiceBus` (7.18.1) - Message queue integration
- **Microsoft Extensions:**
  - `Microsoft.Extensions.Hosting` (8.0.0) - Hosting infrastructure
  - `Microsoft.Extensions.Logging` (8.0.1) - Logging framework
  - `Microsoft.Extensions.Logging.Abstractions` (8.0.2)
  - `Microsoft.Extensions.Logging.Console` (8.0.0)
  - `Microsoft.Extensions.Logging.Debug` (8.0.0)
- **Entity Framework:**
  - `Microsoft.EntityFrameworkCore.SqlServer` (8.0.11) - SQL Server data access
  - `Microsoft.Data.SqlClient` (5.1.6) - SQL connection management
- **Utilities:**
  - `System.Text.Json` (8.0.5) - JSON serialization
  - `SSH.NET` (2024.0.0) - SFTP connectivity

### Base Images
- **Build Stage:** `mcr.microsoft.com/dotnet/sdk:8.0` (includes compiler and tools)
- **Runtime Stage:** `mcr.microsoft.com/dotnet/aspnet:8.0` (minimal runtime, ~349 MB)

---

## Configuration & Environment Variables

The container expects the following environment variables to be set by Azure Container Apps:

### Required Configuration
- `ADAPTER_INSTANCE_GUID` - Unique identifier for this adapter instance
- `ADAPTER_NAME` - Name of adapter (CSV, SqlServer, SAP, Dynamics365, CRM, FILE, SFTP)
- `ADAPTER_TYPE` - Role: "Source" or "Destination"
- `INTERFACE_NAME` - Name of the interface this adapter belongs to
- `INSTANCE_NAME` - Display name for the adapter instance

### Azure Integration
- `BLOB_CONNECTION_STRING` - Azure Storage account connection string
- `BLOB_CONTAINER_NAME` - Container name where `adapter-config.json` is stored
- `ADAPTER_CONFIG_PATH` - Path to config file (default: `adapter-config.json`)
- `AZURE_SERVICEBUS_CONNECTION_STRING` - Service Bus connection string

### Configuration File (`adapter-config.json`)
Stored in Azure Blob Storage, contains adapter-specific settings:
- Connection strings
- Query configurations
- Field mappings
- Transformation rules
- Polling intervals
- Batch sizes

---

## Architecture & Design Patterns

### 1. Universal Adapter Pattern
- Each adapter can function as both **Source** and **Destination**
- Single implementation supports bidirectional data flow
- Role determined by `ADAPTER_TYPE` environment variable

### 2. Service Bus Integration
- **Source Adapters:** Read from data source → Debatch → Publish to Service Bus
- **Destination Adapters:** Subscribe to Service Bus → Process → Write to destination
- Guaranteed delivery via Azure Service Bus
- Message-based architecture enables scalability and fault tolerance

### 3. Process Isolation
- Each adapter instance runs in its own isolated container
- Dedicated blob storage container per instance
- Independent scaling and resource allocation
- Fault isolation: One adapter failure doesn't affect others

### 4. Configuration-Driven
- Adapters read configuration from blob storage at startup
- Runtime configuration updates possible (via blob storage updates)
- No code changes required for new interfaces

### 5. Batch Processing
- Configurable batch sizes (default: 1000 records)
- Efficient processing of large datasets
- Memory-efficient streaming for large files

---

## Runtime Behavior

### Startup Sequence
1. Container starts and loads environment variables
2. Adapter runtime initializes logging
3. Connects to Azure Blob Storage
4. Downloads and parses `adapter-config.json`
5. Instantiates the appropriate adapter class based on `ADAPTER_NAME`
6. Configures adapter with settings from config file
7. Starts processing based on adapter role:
   - **Source:** Polls data source → Reads → Publishes to Service Bus
   - **Destination:** Subscribes to Service Bus → Processes → Writes to destination

### Health & Monitoring
- **Health Endpoint:** Available on port 8080 (ASP.NET Core)
- **Logging:** Structured logging to console (captured by Container Apps)
- **Metrics:** Processing statistics tracked via `ProcessingStatisticsService`

---

## Build Process

### Multi-Stage Docker Build
1. **Build Stage:**
   - Uses .NET SDK image
   - Restores NuGet packages
   - Compiles all projects (adapter-runtime, adapters, main.Core)
   - Publishes optimized output

2. **Runtime Stage:**
   - Uses minimal ASP.NET runtime image
   - Copies only compiled binaries and dependencies
   - Results in smaller image size (~349 MB)

### Build Context
- Build context: Project root directory
- Includes: `docker/adapter-runtime/`, `adapters/`, `main.Core/`
- All 7 adapter types share the same codebase and image

---

## Deployment

### Image Tags
- **Format:** `{adapter-type}-adapter:latest`
- **Examples:**
  - `acrinterfaceconfig.azurecr.io/csv-adapter:latest`
  - `acrinterfaceconfig.azurecr.io/sqlserver-adapter:latest`
  - `acrinterfaceconfig.azurecr.io/sap-adapter:latest`
  - etc.

### Usage in Azure Container Apps
- Images are deployed as Azure Container Apps
- Created dynamically by `ContainerAppService` in Azure Functions
- Each container app runs one adapter instance
- Managed lifecycle: Create → Configure → Start → Monitor → Delete

---

## Data Flow

### Source Adapter Flow
```
Data Source (CSV/SQL/etc.)
    ↓
Adapter.ReadAsync()
    ↓
Debatching (1 record = 1 message)
    ↓
Service Bus Topic/Queue
    ↓
Destination Adapters (multiple subscribers possible)
```

### Destination Adapter Flow
```
Service Bus Topic/Queue
    ↓
Adapter receives message
    ↓
Optional: JQ Transformation
    ↓
Adapter.WriteAsync()
    ↓
Data Destination (CSV/SQL/etc.)
```

---

## Features & Capabilities

### Data Transformation
- **JQ Support:** JSON transformation using jq scripts
- **Field Mapping:** Dynamic column mapping between source and destination
- **Type Conversion:** Automatic data type detection and conversion

### Error Handling
- **Retry Logic:** Built-in retry mechanisms for transient failures
- **Error Logging:** Comprehensive error tracking and logging
- **Dead Letter Queue:** Failed messages moved to DLQ for manual review

### Performance
- **Batch Processing:** Configurable batch sizes for optimal throughput
- **Parallel Processing:** Multiple container instances can run in parallel
- **Streaming:** Memory-efficient processing of large files
- **Connection Pooling:** Reusable connections (SFTP, SQL)

---

## Security

- **Authentication:**
  - Azure AD integration (for Dynamics 365, CRM)
  - SQL authentication or Integrated Security
  - SFTP key-based or password authentication
- **Storage:**
  - Connection strings stored securely in Azure Blob Storage
  - Environment variables set by Container App Service
  - No hardcoded credentials in images
- **Network:**
  - All communication over HTTPS/TLS
  - Isolated container networks

---

## Maintenance & Updates

### Image Updates
- Rebuild and push to ACR when:
  - Code changes in adapters or core services
  - Package version updates
  - Bug fixes or new features
- Container apps automatically pull latest `:latest` tag on restart
- Zero-downtime updates possible with blue-green deployment

### Version Management
- All images currently use `:latest` tag
- Future: Version tags (e.g., `:v1.2.3`) for production stability
- Image digests tracked for reproducibility

---

## Summary

All 7 adapter container images contain:
- ✅ **Same Codebase:** All adapters compiled into every image
- ✅ **Runtime Selection:** Adapter type determined at runtime via environment variables
- ✅ **Isolated Execution:** Each container runs one adapter instance
- ✅ **Azure Integration:** Native Azure Storage, Service Bus, and SQL support
- ✅ **Configuration-Driven:** Dynamic configuration via blob storage
- ✅ **Scalable Architecture:** Message-based, horizontally scalable design

**Last Updated:** November 27, 2025  
**Total Images:** 7 (all contain identical code, tagged separately for clarity)  
**Image Size:** ~349 MB each  
**Runtime:** .NET 8.0 on Linux

