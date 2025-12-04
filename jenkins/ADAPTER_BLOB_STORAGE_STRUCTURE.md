# Adapter Blob Storage Structure

## Overview

Each adapter instance that handles files gets its own isolated folder structure within the `adapter-data` container in Azure Blob Storage.

## Which Adapters Need Blob Storage?

### ✅ File-Based Adapters (Need Blob Storage):
- **CSV Adapter** - Receives CSV files
- **SFTP Adapter** - Receives/sends files via SFTP
- **File Adapter** - Direct file system access
- **SAP Adapter** - Receives IDoc files

### ❌ Non-File Adapters (No Blob Storage):
- **SQL Server Adapter** - Direct database connection
- **REST API Adapter** - HTTP requests only
- **Database Adapter** - Direct connection

## Folder Structure

```
adapter-data/                        ← Single container for all adapter instances
├── {adapter-instance-guid-1}/       ← CSV adapter instance
│   ├── incoming/                    ← New files arrive here
│   ├── error/                       ← Failed processing files moved here
│   └── processed/                   ← Successfully processed files moved here
│
├── {adapter-instance-guid-2}/       ← SFTP adapter instance
│   ├── incoming/
│   ├── error/
│   └── processed/
│
└── {adapter-instance-guid-3}/       ← SAP adapter instance
    ├── incoming/
    ├── error/
    └── processed/
```

## Dynamic Provisioning

**Adapter instance folders are created automatically when:**
1. A new file-based adapter instance is created via the API
2. The adapter instance configuration includes `requiresBlobStorage: true`
3. The provisioning service creates the folder structure:
   - `POST /api/adapter-instances` → Creates blob folders
   - Azure Function automatically provisions folders
   - Integration tests use pre-created sample folders

## Naming Convention

**Old (deprecated):**
```
csv-incoming/     ❌ Too specific to CSV adapter
csv-error/        ❌ Not reusable for other adapters
csv-processed/    ❌ Hard-coded names
```

**New (current):**
```
adapter-data/{guid}/incoming/   ✅ Generic, works for all file adapters
adapter-data/{guid}/error/      ✅ Isolated per instance
adapter-data/{guid}/processed/  ✅ Scalable architecture
```

## Benefits

1. **Isolation**: Each adapter instance has its own namespace
2. **Scalability**: Unlimited adapter instances without naming conflicts
3. **Multi-tenancy**: Different customers can have separate adapter instances
4. **Flexibility**: Works for CSV, SFTP, File, SAP, and future file-based adapters
5. **Security**: Per-instance access control via SAS tokens or folder-level permissions

## Integration Tests

For integration tests, we pre-create sample adapter instance folders:
- `csv-adapter-test-001/`
- `sftp-adapter-test-001/`
- `file-adapter-test-001/`
- `sap-adapter-test-001/`

These allow tests to verify:
- ✅ Folder access
- ✅ File upload/download
- ✅ Error handling (moving files to error/)
- ✅ Archive (moving files to processed/)

## Production Setup

In production, the `InterfaceConfigurationService` automatically:
1. Generates a new GUID for the adapter instance
2. Checks if adapter type requires blob storage
3. Creates the folder structure: `{guid}/incoming`, `{guid}/error`, `{guid}/processed`
4. Stores the GUID in the adapter instance configuration
5. Returns the GUID to the client for tracking

Example adapter instance configuration:
```json
{
  "adapterInstanceGuid": "550e8400-e29b-41d4-a716-446655440000",
  "adapterType": "csv",
  "blobStoragePath": "adapter-data/550e8400-e29b-41d4-a716-446655440000",
  "requiresBlobStorage": true,
  "folders": {
    "incoming": "adapter-data/550e8400-e29b-41d4-a716-446655440000/incoming",
    "error": "adapter-data/550e8400-e29b-41d4-a716-446655440000/error",
    "processed": "adapter-data/550e8400-e29b-41d4-a716-446655440000/processed"
  }
}
```

