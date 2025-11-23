# Azure Resource Naming Plan

## Current Resources → New Names

### Function App Resources
- **Current**: `func-apprigklebtsay2o`
- **New**: `func-integration`
- **Reason**: Clearly indicates CSV to SQL Server processing functionality

- **Current**: `plan-funcs-apprigklebtsay2o`
- **New**: `plan-func-csv-processor`
- **Reason**: App Service Plan for CSV processor function

### SQL Server Resources
- **Current**: `sql-infrastructurerigklebtsay2o`
- **New**: `sql-main-database`
- **Reason**: Main SQL database server

- **Current**: `sql-infrastructurerigklebtsay2o/app_database`
- **New**: `sql-main-database/app-database`
- **Reason**: Application database

### Storage Account Resources
- **Current**: `stfuncsapprigklebtsay2o`
- **New**: `stfunccsvprocessor`
- **Reason**: Storage for CSV processor function app (no hyphens - Azure Storage Account requirement)

### Resource Group
- **Current**: `rg-infrastructure-as-code`
- **New**: `rg-interface-configuration`
- **Reason**: Already descriptive

## Naming Convention Rules Applied

1. ✅ **Descriptive**: Names explain what the resource does
2. ✅ **No random suffixes**: Removed `rigklebtsay2o` type suffixes
3. ✅ **No numbers**: No digits in names
4. ✅ **Hyphens only**: Using hyphens as separators
5. ✅ **Lowercase**: All lowercase (Azure requirement)
6. ✅ **Length limits**: All names within Azure limits

## Migration Strategy

1. Update Terraform variables and configuration
2. Update Bicep parameters and configuration
3. Delete old resources
4. Create new resources with new names
5. Update GitHub Actions workflow if needed

