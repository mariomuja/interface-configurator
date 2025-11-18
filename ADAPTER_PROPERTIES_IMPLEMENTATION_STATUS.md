# Adapter Properties Implementation Status

## Summary

This document provides a comprehensive status check of adapter properties implementation and UI integration.

---

## ✅ SqlServerAdapter Properties - FULLY IMPLEMENTED

All SQL Server adapter properties are **implemented and available in the UI**.

### Connection Properties (Both Source & Destination)

| Property | Backend Model | UI Field | Status |
|----------|--------------|----------|--------|
| **ServerName** | `SqlServerName` | "Server Name" field | ✅ Implemented |
| **DatabaseName** | `SqlDatabaseName` | "Database Name" field | ✅ Implemented |
| **UserName** | `SqlUserName` | "User Name" field | ✅ Implemented |
| **Password** | `SqlPassword` | "Password" field (password type) | ✅ Implemented |
| **IntegratedSecurity** | `SqlIntegratedSecurity` | Toggle "Integrated Security (Windows Authentication)" | ✅ Implemented |

### Source-Specific Properties

| Property | Backend Model | UI Field | Status |
|----------|--------------|----------|--------|
| **PollingStatement** | `SqlPollingStatement` | "Polling Statement" textarea | ✅ Implemented |
| **PollingInterval** | `SqlPollingInterval` | "Polling Interval" number field | ✅ Implemented |

### UI Location
- **File**: `frontend/src/app/components/adapter-properties-dialog/adapter-properties-dialog.component.html`
- **Section**: "SQL Server Connection" (lines 82-177)
- **Polling Section**: "Polling Configuration (Source Only)" (lines 179-202)

### Backend Storage
- **Model**: `azure-functions/main.Core/Models/InterfaceConfiguration.cs`
- **API Endpoints**: 
  - `UpdateSqlConnectionProperties` - Updates connection properties
  - `UpdateSqlPollingProperties` - Updates polling properties

---

## ⚠️ CsvAdapter Properties - PARTIALLY IMPLEMENTED

### ✅ FileMask - IMPLEMENTED (with differences)

| Property | Backend Model | UI Field | Status |
|----------|--------------|----------|--------|
| **FileMask** (Source) | `SourceFileMask` | "File Mask" field | ✅ Implemented |
| **FileMask** (Destination) | `DestinationFileMask` | "Destination File Mask" field | ✅ Implemented |

#### Variable Support Status:
- ✅ **Supports variables**: Yes
- ⚠️ **Variable syntax**: Uses `$datetime` instead of `%date_time%`
- ✅ **String concatenation**: Supported (e.g., `"result_" + $datetime + ".txt"`)
- ✅ **Wildcards**: Supported (`*`, `?`)

#### Current Implementation:
- **Variable**: `$datetime` (replaced with `yyyyMMddHHmmss.fff` format)
- **Example**: `"text_" + $datetime + ".txt"` → `"text_20240101120000.123.txt"`
- **Location**: `azure-functions/main/Adapters/CsvAdapter.cs` (lines 544-576)

#### Required Changes:
- ❌ **Missing**: Support for `%date_time%` syntax (currently only `$datetime`)
- **Note**: The functionality is equivalent, only the syntax differs

### ❌ FileShare (UNC Path) - NOT IMPLEMENTED

| Property | Backend Model | UI Field | Status |
|----------|--------------|----------|--------|
| **FileShare** | ❌ Not found | ❌ Not found | ❌ **NOT IMPLEMENTED** |

#### Current Implementation:
- ✅ **SourceReceiveFolder**: Exists but only supports **blob storage paths**
  - Format: `"container-name/folder-path"` (e.g., `"csv-files/csv-incoming"`)
  - **Does NOT support UNC paths** like `"\\server\share\folder"`

#### Missing Implementation:
- ❌ No `FileShare` or `SourceFileShare` property in `InterfaceConfiguration`
- ❌ No UI field for UNC path configuration
- ❌ No backend logic to handle UNC paths (currently only Azure Blob Storage)

#### Required Changes:
1. Add `SourceFileShare` property to `InterfaceConfiguration` model
2. Add UI field in adapter properties dialog
3. Update `CsvAdapter` to support UNC paths in addition to blob storage paths
4. Add API endpoint to update FileShare property

---

## Implementation Details

### SqlServerAdapter Properties

#### Backend Model (`InterfaceConfiguration.cs`):
```csharp
// Connection properties (lines 125-155)
public string? SqlServerName { get; set; }
public string? SqlDatabaseName { get; set; }
public string? SqlUserName { get; set; }
public string? SqlPassword { get; set; }
public bool SqlIntegratedSecurity { get; set; } = false;
public string? SqlResourceGroup { get; set; }

// Source-specific properties (lines 162-169)
public string? SqlPollingStatement { get; set; }
public int SqlPollingInterval { get; set; } = 60;
```

#### UI Implementation:
- **File**: `frontend/src/app/components/adapter-properties-dialog/adapter-properties-dialog.component.html`
- All fields are properly bound with `[(ngModel)]`
- Conditional display based on adapter type (Source vs Destination)
- Integrated Security toggle shows/hides UserName and Password fields

### CsvAdapter Properties

#### FileMask Implementation:
- **Source FileMask**: `SourceFileMask` (default: `"*.txt"`)
- **Destination FileMask**: `DestinationFileMask` (default: `"*.txt"`)
- **Variable expansion**: Implemented in `ExpandFileNameVariables()` method
- **Current syntax**: `$datetime` (not `%date_time%`)

#### FileShare Status:
- **Current**: Only `SourceReceiveFolder` for blob storage paths
- **Missing**: UNC path support (`\\server\share\folder`)

---

## Recommendations

### High Priority
1. **Add FileShare (UNC Path) Support**:
   - Add `SourceFileShare` property to `InterfaceConfiguration`
   - Update `CsvAdapter` to handle both blob storage and UNC paths
   - Add UI field for FileShare configuration
   - Create API endpoint for updating FileShare

### Low Priority
1. **Add `%date_time%` Variable Support**:
   - Extend `ExpandFileNameVariables()` to support both `$datetime` and `%date_time%`
   - Update UI tooltips to mention both syntaxes
   - **Note**: Current `$datetime` syntax works, this is just for compatibility

---

## Test Coverage

All implemented properties are:
- ✅ Stored in `InterfaceConfiguration` model
- ✅ Persisted via API endpoints
- ✅ Displayed in UI settings dialog
- ✅ Loaded and saved correctly in `TransportComponent`
- ✅ Passed to adapters via `AdapterFactory`

---

## Files to Review

### Backend
- `azure-functions/main.Core/Models/InterfaceConfiguration.cs`
- `azure-functions/main/Adapters/CsvAdapter.cs`
- `azure-functions/main/Services/AdapterFactory.cs`

### Frontend
- `frontend/src/app/components/adapter-properties-dialog/adapter-properties-dialog.component.html`
- `frontend/src/app/components/adapter-properties-dialog/adapter-properties-dialog.component.ts`
- `frontend/src/app/components/transport/transport.component.ts`

### API Endpoints
- `azure-functions/main/UpdateSqlConnectionProperties.cs`
- `azure-functions/main/UpdateSqlPollingProperties.cs`
- `azure-functions/main/UpdateFileMask.cs`



