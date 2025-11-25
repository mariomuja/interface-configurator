# Codebase Restructuring Summary

## Overview
Restructured the codebase to reflect the new architecture where adapters run in container apps, not Azure Functions.

## Changes Made

### 1. New Folder Structure
- **`tests/`** - Created at root level for all non-Azure-Function tests
- **`adapters/`** - Created at root level for adapter implementations (run in container apps)
- **`main.Core/`** - Moved from `azure-functions/main.Core/` to root level (shared library)

### 2. Moved Items

#### Tests
- `azure-functions/main.Core.Tests/` → `tests/main.Core.Tests/`
- Test scripts from `azure-functions/*.ps1` → `tests/scripts/`
- Test scripts from `azure-functions/scripts/*.ps1` → `tests/scripts/`
- Test functions (`Test*.cs`, `UploadTestCsv.cs`, `GetTestReport.cs`) → `tests/`
- `azure-functions/TestResults/` → `tests/TestResults/`
- Test documentation (`TEST_REPORTING.md`, `VS_CODE_TEST_EXPLORER.md`) → `tests/`

#### Adapters
- `azure-functions/main/Adapters/` → `adapters/`
- Created `adapters/adapters.csproj` project file
- Updated namespace: `InterfaceConfigurator.Main.Adapters` → `InterfaceConfigurator.Adapters`

#### Shared Library
- `azure-functions/main.Core/` → `main.Core/` (root level)

### 3. Updated References

#### Project References
- `azure-functions/main/main.csproj` - Updated to reference `..\..\main.Core\main.Core.csproj` and `..\..\adapters\adapters.csproj`
- `tests/main.Core.Tests/main.Core.Tests.csproj` - Updated to reference new locations
- `adapters/adapters.csproj` - References `main.Core` and `azure-functions/main` (for CsvValidationService, ApplicationDbContext)

#### Namespace Updates
- All adapter files: `InterfaceConfigurator.Main.Adapters` → `InterfaceConfigurator.Adapters`
- All using statements updated in:
  - `azure-functions/main/` (SourceAdapterFunction, Program, AdapterFactory, RunTransportPipeline)
  - `tests/` (all test files)

#### Solution File
- `azure-functions/azure-functions.sln` - Updated paths for `main.Core` and `main.Core.Tests`
- Added `adapters` project to solution

### 4. Current Structure

```
interface-configurator/
├── adapters/                    # Adapter implementations (run in container apps)
│   ├── adapters.csproj
│   ├── AdapterBase.cs
│   ├── CsvAdapter.cs
│   ├── SapAdapter.cs
│   ├── Dynamics365Adapter.cs
│   ├── CrmAdapter.cs
│   ├── SftpAdapter.cs
│   ├── SqlServerAdapter.cs
│   ├── FileAdapter.cs
│   └── HttpClientAdapterBase.cs
├── main.Core/                   # Shared library (interfaces, models, services)
│   ├── Interfaces/
│   ├── Models/
│   ├── Services/
│   └── main.Core.csproj
├── tests/                       # All tests
│   ├── main.Core.Tests/
│   ├── scripts/                 # Test scripts
│   ├── TestResults/
│   └── Test*.cs                 # Test functions
├── azure-functions/             # Azure Functions only
│   ├── main/                    # Azure Function implementations
│   └── azure-functions.sln
└── ...
```

### 5. Rationale

- **Adapters in container apps**: Adapters now run in isolated container apps, not Azure Functions
- **Shared library at root**: `main.Core` is used by both Azure Functions and adapters
- **Tests separated**: Tests are not Azure Function-specific, so they belong at root level
- **Clear separation**: Azure Functions folder now only contains Azure Function-specific code

### 6. Dependencies

- `adapters` → `main.Core` (interfaces, models)
- `adapters` → `azure-functions/main` (CsvValidationService, ApplicationDbContext - temporary)
- `azure-functions/main` → `main.Core` (shared library)
- `azure-functions/main` → `adapters` (adapter implementations)
- `tests/main.Core.Tests` → `main.Core`, `azure-functions/main`, `adapters`

### 7. Next Steps

1. Consider moving `CsvValidationService` to `main.Core` to remove adapter dependency on Azure Functions
2. Consider moving `ApplicationDbContext` usage out of adapters (or create adapter-specific context)
3. Update Docker build process to reference new adapter location
4. Update CI/CD pipelines if they reference old paths

