# Service Bus Migration Guide

## Overview
This document describes the migration from MessageBox database to Azure Service Bus for message communication between adapters.

## Architecture Changes

### Before (MessageBox Database)
- Source adapters write messages to SQL Server MessageBox database
- Destination adapters read messages from MessageBox database
- Messages stored in `Messages` table with status tracking
- Subscriptions tracked in `MessageSubscriptions` and `AdapterSubscriptions` tables

### After (Azure Service Bus)
- Source adapters send messages to Service Bus topics (one topic per interface)
- Destination adapters subscribe to Service Bus subscriptions (one subscription per destination instance)
- Messages flow through Service Bus queues/topics
- File adapters (CSV, File, SFTP) forward files to blob containers in parallel

## Implementation Details

### Service Bus Structure
- **Topic naming**: `interface-{interfaceName}` (lowercase)
- **Subscription naming**: `destination-{destinationAdapterInstanceGuid}` (lowercase)
- Each interface has one topic
- Each destination adapter instance has one subscription

### File Forwarding
- CSV/File/SFTP adapters forward received files to blob containers:
  - Container: `csv-files`
  - Folder structure: `{interfaceName}/{adapterInstanceGuid}/{timestamp}/`
  - Files are stored in parallel with Service Bus message sending

### Message Flow
1. Source adapter reads data (CSV, SQL, etc.)
2. Source adapter sends messages to Service Bus topic
3. Source adapter forwards files to blob containers (if applicable)
4. Destination adapters receive messages from their subscriptions
5. Destination adapters process messages and complete them

## Code Changes Required

### 1. Service Bus Service
- ✅ Created `IServiceBusService` interface
- ✅ Created `ServiceBusService` implementation
- ✅ Added Azure.Messaging.ServiceBus NuGet package

### 2. Adapter Updates
- ✅ Updated `AdapterBase` to support Service Bus
- ⏳ Update `CsvAdapter` to forward files to blob containers
- ⏳ Update `FileAdapter` to forward files to blob containers
- ⏳ Update `SftpAdapter` to forward files to blob containers
- ⏳ Update destination adapters to use Service Bus subscriptions

### 3. UI Updates
- ⏳ Replace MessageBox card with Service Bus messages display
- ⏳ Show messages from Service Bus topic for the interface
- ⏳ Compact display showing message count and recent messages

### 4. Infrastructure
- ⏳ Add Service Bus namespace to Bicep/Terraform
- ⏳ Create topics and subscriptions dynamically
- ⏳ Configure Service Bus connection string

### 5. Database Cleanup
- ⏳ Remove unused MessageBox tables:
  - `Messages` (replaced by Service Bus)
  - `MessageSubscriptions` (replaced by Service Bus subscriptions)
  - `AdapterSubscriptions` (replaced by Service Bus subscriptions)
  - `MessageProcessing` (replaced by Service Bus message handling)
- ⏳ Keep `ProcessLogs` and `ProcessingStatistics` (still needed)

## Configuration

### App Settings
- `ServiceBusConnectionString`: Connection string for Service Bus namespace
- `ServiceBusNamespace`: Service Bus namespace name (optional, for management operations)

### Environment Variables
- `AZURE_SERVICEBUS_CONNECTION_STRING`: Service Bus connection string

## Migration Steps

1. Deploy Service Bus infrastructure
2. Deploy updated code with Service Bus support
3. Run migration script to create topics/subscriptions for existing interfaces
4. Update adapters to use Service Bus
5. Monitor message flow
6. Remove MessageBox database tables after verification

## Rollback Plan

If issues occur:
1. Revert to MessageBox by setting `UseServiceBus=false` in app settings
2. Adapters will fall back to MessageBox automatically
3. Service Bus messages can be manually migrated back if needed

## Testing

- Test message sending from source adapters
- Test message receiving by destination adapters
- Test file forwarding to blob containers
- Test message completion/abandon/dead letter
- Test multiple destination instances subscribing to same source
- Test error handling and retries





