# Transport Component Refactoring Guide

## Overview
The `transport.component.ts` file has grown to over 5000 lines and needs to be refactored into smaller, more maintainable services. This guide outlines the refactoring strategy and the new service structure.

## New Services Created

### 1. InterfaceManagementService
**Location:** `frontend/src/app/services/interface-management.service.ts`

**Responsibilities:**
- Loading and managing interface configurations
- Creating, updating, and deleting interfaces
- Managing current interface selection
- Validating interface configurations

**Key Methods:**
- `loadInterfaces()` - Load all interfaces
- `createInterface(interfaceName)` - Create new interface
- `deleteInterface(interfaceName)` - Delete interface
- `updateInterfaceName(oldName, newName)` - Rename interface
- `validateInterface(interfaceName)` - Validate interface configuration
- `getInterface(interfaceName)` - Get specific interface
- `getCurrentInterface()` - Get currently selected interface
- `setCurrentInterface(interfaceName)` - Set current interface

**Usage Example:**
```typescript
constructor(private interfaceService: InterfaceManagementService) {}

ngOnInit() {
  this.interfaceService.loadInterfaces().subscribe(interfaces => {
    this.interfaces = interfaces;
  });
  
  this.interfaceService.currentInterface$.subscribe(name => {
    this.currentInterfaceName = name;
  });
}
```

### 2. CsvDataService
**Location:** `frontend/src/app/services/csv-data.service.ts`

**Responsibilities:**
- CSV data parsing and formatting
- Converting between CSV text and records
- HTML formatting for CSV display
- Generating sample CSV data

**Key Methods:**
- `parseCsvText(csvText, separator?)` - Parse CSV text to records
- `convertRecordsToCsvText(records, separator?)` - Convert records to CSV text
- `formatCsvAsText(records, separator?)` - Format records as text
- `formatCsvAsHtml(csvText, fieldSeparator?)` - Format CSV as HTML with colors
- `generateSampleCsvData()` - Generate sample data
- `parseCsvLine(line, separator?)` - Parse single CSV line

**Usage Example:**
```typescript
constructor(private csvService: CsvDataService) {}

formatCsv() {
  const text = this.csvService.formatCsvAsText(this.csvData);
  const html = this.csvService.formatCsvAsHtml(text, this.fieldSeparator);
}
```

### 3. BlobContainerService
**Location:** `frontend/src/app/services/blob-container.service.ts`

**Responsibilities:**
- Managing blob container folders and files
- File selection management
- Pagination for large file lists
- File deletion operations

**Key Methods:**
- `loadFolders(containerName, sortBy?, sortOrder?)` - Load blob folders
- `loadMoreFiles(folderPath, containerName)` - Load more files (pagination)
- `toggleFileSelection(fullPath)` - Toggle file selection
- `selectAllFiles(folder)` - Select all files in folder
- `deselectAllFiles()` - Clear selection
- `deleteSelectedFiles(containerName)` - Delete selected files
- `formatFileSize(bytes)` - Format file size string

**Usage Example:**
```typescript
constructor(private blobService: BlobContainerService) {}

loadBlobs() {
  this.blobService.loadFolders('csv-files', 'date', 'desc').subscribe();
  this.blobService.selectedFiles$.subscribe(selected => {
    this.selectedCount = selected.size;
  });
}
```

### 4. DataLoadingService
**Location:** `frontend/src/app/services/data-loading.service.ts`

**Responsibilities:**
- Loading SQL data
- Loading process logs
- Loading Service Bus messages
- Managing auto-refresh intervals

**Key Methods:**
- `loadSqlData(interfaceName?)` - Load SQL data
- `loadProcessLogs(interfaceName?, component?)` - Load process logs
- `loadServiceBusMessages(interfaceName, maxMessages?)` - Load Service Bus messages
- `startAutoRefresh(interval?)` - Start auto-refresh
- `stopAutoRefresh()` - Stop auto-refresh
- `extractComponent(message, details?)` - Extract component from log message

**Usage Example:**
```typescript
constructor(private dataService: DataLoadingService) {}

ngOnInit() {
  this.dataService.loadSqlData().subscribe();
  this.dataService.startAutoRefresh(3000);
}

ngOnDestroy() {
  this.dataService.stopAllAutoRefresh();
}
```

### 5. AdapterConfigurationService
**Location:** `frontend/src/app/services/adapter-configuration.service.ts`

**Responsibilities:**
- Updating adapter configuration properties
- Managing source and destination adapter settings
- Handling adapter enable/disable operations

**Key Methods:**
- `updateReceiveFolder(interfaceName, receiveFolder)` - Update receive folder
- `updateFileMask(interfaceName, fileMask)` - Update file mask
- `updateBatchSize(interfaceName, batchSize)` - Update batch size
- `updateSqlConnectionProperties(...)` - Update SQL connection
- `updateSourceAdapterInstance(...)` - Update source adapter
- `updateDestinationAdapterInstance(...)` - Update destination adapter
- `restartAdapter(interfaceName, adapterType)` - Restart adapter

**Usage Example:**
```typescript
constructor(private adapterService: AdapterConfigurationService) {}

updateFolder(folder: string) {
  this.adapterService.updateReceiveFolder(this.interfaceName, folder)
    .subscribe(() => {
      this.snackBar.open('Folder updated', 'OK');
    });
}
```

### 6. TransportControlService
**Location:** `frontend/src/app/services/transport-control.service.ts`

**Responsibilities:**
- Starting and stopping transport operations
- Restarting adapters
- Database operations (drop table, clear logs)

**Key Methods:**
- `startTransport(interfaceName)` - Start transport
- `restartAdapter(interfaceName, adapterType)` - Restart adapter
- `dropTable(interfaceName)` - Drop database table
- `clearLogs()` - Clear process logs

**Usage Example:**
```typescript
constructor(private transportControl: TransportControlService) {}

start() {
  this.transportControl.startTransport(this.interfaceName)
    .subscribe(() => {
      this.snackBar.open('Transport started', 'OK');
    });
}
```

## Refactoring Steps

### Step 1: Inject New Services
Replace direct `TransportService` calls with appropriate service calls:

```typescript
// Before
constructor(
  private transportService: TransportService,
  private snackBar: MatSnackBar
) {}

// After
constructor(
  private transportService: TransportService,
  private interfaceService: InterfaceManagementService,
  private csvService: CsvDataService,
  private blobService: BlobContainerService,
  private dataService: DataLoadingService,
  private adapterService: AdapterConfigurationService,
  private transportControl: TransportControlService,
  private snackBar: MatSnackBar
) {}
```

### Step 2: Replace Interface Management Methods
Move interface-related methods to use `InterfaceManagementService`:

```typescript
// Before
loadInterfaceConfigurations(): void {
  this.transportService.getInterfaceConfigurations().subscribe(...);
}

// After
loadInterfaceConfigurations(): void {
  this.interfaceService.loadInterfaces().subscribe(interfaces => {
    this.interfaceConfigurations = interfaces;
  });
}
```

### Step 3: Replace CSV Data Methods
Move CSV formatting/parsing to `CsvDataService`:

```typescript
// Before
formatCsvAsHtml(): string {
  // 50+ lines of formatting code
}

// After
formatCsvAsHtml(): string {
  return this.csvService.formatCsvAsHtml(this.editableCsvText, this.sourceFieldSeparator);
}
```

### Step 4: Replace Blob Container Methods
Move blob operations to `BlobContainerService`:

```typescript
// Before
loadBlobContainerFolders(): void {
  this.transportService.getBlobContainerFolders(...).subscribe(...);
}

// After
loadBlobContainerFolders(): void {
  this.blobService.loadFolders('csv-files', this.sortBy, this.sortOrder)
    .subscribe(folders => {
      this.blobContainerFolders = folders;
    });
}
```

### Step 5: Replace Data Loading Methods
Move data loading to `DataLoadingService`:

```typescript
// Before
loadSqlData(): void {
  this.transportService.getSqlData().subscribe(...);
}

// After
loadSqlData(): void {
  this.dataService.loadSqlData(this.currentInterfaceName)
    .subscribe(data => {
      this.sqlData = data;
    });
}
```

### Step 6: Replace Adapter Configuration Methods
Move adapter updates to `AdapterConfigurationService`:

```typescript
// Before
updateReceiveFolder(folder?: string): void {
  this.transportService.updateReceiveFolder(...).subscribe(...);
}

// After
updateReceiveFolder(folder?: string): void {
  this.adapterService.updateReceiveFolder(this.interfaceName, folder)
    .subscribe(...);
}
```

### Step 7: Replace Transport Control Methods
Move transport operations to `TransportControlService`:

```typescript
// Before
startTransport(): void {
  this.transportService.startTransport(...).subscribe(...);
}

// After
startTransport(): void {
  this.transportControl.startTransport(this.currentInterfaceName)
    .subscribe(...);
}
```

## Benefits of Refactoring

1. **Separation of Concerns**: Each service has a single, well-defined responsibility
2. **Reusability**: Services can be used by other components
3. **Testability**: Services can be easily unit tested in isolation
4. **Maintainability**: Smaller, focused files are easier to understand and modify
5. **Performance**: Services can be optimized independently
6. **Code Organization**: Related functionality is grouped together

## Migration Checklist

- [ ] Inject all new services in component constructor
- [ ] Replace `loadInterfaceConfigurations()` with `InterfaceManagementService`
- [ ] Replace CSV formatting methods with `CsvDataService`
- [ ] Replace blob container methods with `BlobContainerService`
- [ ] Replace data loading methods with `DataLoadingService`
- [ ] Replace adapter configuration methods with `AdapterConfigurationService`
- [ ] Replace transport control methods with `TransportControlService`
- [ ] Remove unused private methods from component
- [ ] Update component to use service observables where appropriate
- [ ] Test all functionality after refactoring
- [ ] Update component template if needed
- [ ] Remove duplicate code

## Estimated Size Reduction

- **Before**: ~5000 lines in `transport.component.ts`
- **After**: ~2000-2500 lines in `transport.component.ts` + ~1500 lines across 6 services
- **Reduction**: ~50% reduction in component size

## Next Steps

1. Start with one service at a time (recommend starting with `CsvDataService` as it's most isolated)
2. Test thoroughly after each service migration
3. Keep the old code commented out initially for reference
4. Remove old code once migration is verified
5. Consider creating additional helper services if needed (e.g., `ErrorHandlingService`, `ValidationService`)















