# Troubleshooting SQL Errors

## Common Error: "SQL query failed. Check if tables exist."

This error can occur for several reasons. Follow these steps to diagnose and fix:

### Step 1: Check SQL Configuration

Run the **Diagnose** button in the UI or call:
```bash
curl https://your-app.vercel.app/api/diagnose
```

Verify that:
- ✅ SQL Configuration: OK
- ✅ SQL Database Connection: OK

### Step 2: Check if Tables Exist

#### Option A: Azure Portal Query Editor

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **SQL Server** → Your SQL Server
3. Click **Query editor (preview)**
4. Login with SQL admin credentials
5. Select your database
6. Run:
```sql
SELECT 
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportData')
        THEN 'TransportData EXISTS'
        ELSE 'TransportData DOES NOT EXIST'
    END AS TransportDataStatus;
```

#### Option B: Azure CLI

```bash
az sql db execute-query \
  --server <your-sql-server> \
  --database <your-database> \
  --admin-user <admin-user> \
  --admin-password <admin-password> \
  --query-text "SELECT COUNT(*) AS TableCount FROM sys.tables WHERE name = 'TransportData'"
```

### Step 3: Initialize Database (if tables don't exist)

#### Option A: Use API Endpoint

Call the initialization endpoint:
```bash
curl -X POST https://your-app.vercel.app/api/init-database
```

This will create the `TransportData` table with the correct structure.

#### Option B: Azure Portal Query Editor

1. Open Query Editor (see Step 2)
2. Run this SQL:

```sql
-- Create TransportData table with dynamic columns support
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TransportData] (
        [PrimaryKey] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);
    
    PRINT 'TransportData table created successfully';
END
GO
```

#### Option C: Use init-database.sql Script

```bash
az sql db execute-query \
  --server <your-sql-server> \
  --database <your-database> \
  --admin-user <admin-user> \
  --admin-password <admin-password> \
  --file-path terraform/init-database.sql
```

### Step 4: Check Table Structure

If the table exists but has the wrong structure (old structure with `Id` instead of `PrimaryKey`):

```sql
-- Check table structure
SELECT c.name AS ColumnName, t.name AS TypeName
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.name = 'TransportData'
ORDER BY c.column_id;
```

**Expected columns:**
- `PrimaryKey` (UNIQUEIDENTIFIER) - Primary key
- `datetime_created` (DATETIME2) - Timestamp
- Plus dynamic CSV columns (created automatically when CSV is processed)

**Old structure (needs migration):**
- `Id` (UNIQUEIDENTIFIER) - Old primary key
- `CsvDataJson` (NVARCHAR(MAX)) - Old JSON storage
- `datetime_created` (DATETIME2)

### Step 5: Migrate Old Table Structure (if needed)

If you have the old structure, you can migrate:

```sql
-- Drop old table (WARNING: This deletes all data!)
DROP TABLE IF EXISTS [dbo].[TransportData];

-- Create new table structure
CREATE TABLE [dbo].[TransportData] (
    [PrimaryKey] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);
```

**Note:** The new structure uses dynamic columns (created automatically based on CSV structure), so you don't need to define CSV columns upfront.

### Step 6: Verify Fix

After initialization:

1. **Check UI**: The Destination table should show empty (no error)
2. **Run Diagnostics**: All SQL checks should pass
3. **Test Transport**: Upload a CSV file - it should process successfully

### Common Issues

#### Issue: "Invalid column name 'PrimaryKey'"

**Cause**: Table has old structure with `Id` column instead of `PrimaryKey`.

**Solution**: 
- The code now handles both structures automatically
- If error persists, migrate table (see Step 5)

#### Issue: "Invalid object name 'TransportData'"

**Cause**: Table doesn't exist.

**Solution**: 
- Initialize database (Step 3)
- Or wait for first CSV upload (table will be created automatically)

#### Issue: Connection timeout

**Cause**: Firewall rules blocking connection or incorrect credentials.

**Solution**:
1. Check SQL Server firewall rules in Azure Portal
2. Ensure "Allow Azure services and resources to access this server" is enabled
3. Verify credentials in Vercel Environment Variables

### Automatic Table Creation

The Azure Function will automatically create the `TransportData` table when processing the first CSV file. However, if you want to test the UI before uploading a CSV, you need to initialize the table manually (see Step 3).

### API Endpoints

- `GET /api/sql-data` - Returns empty array `[]` if table doesn't exist (no error)
- `POST /api/init-database` - Creates TransportData table if it doesn't exist
- `GET /api/diagnose` - Checks all configurations including SQL connection

