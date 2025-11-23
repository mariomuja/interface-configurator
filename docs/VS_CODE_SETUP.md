# VS Code Setup for Azure Functions Development

## Required Extensions

For developing Azure Functions with .NET isolated worker process, you need:

### Essential Extensions

1. **C# Extension** (ms-dotnettools.csharp)
   - Provides IntelliSense, debugging, and code navigation for C#
   - Install: `code --install-extension ms-dotnettools.csharp`

2. **C# Dev Kit** (ms-dotnettools.csdevkit) - Optional but recommended
   - Enhanced C# development experience
   - Install: `code --install-extension ms-dotnettools.csdevkit`

### Azure Extensions (if available)

3. **Azure Functions** (ms-azuretools.vscode-azurefunctions)
   - Note: May have compatibility issues in Cursor
   - Provides Azure Functions templates and deployment tools
   - Install: `code --install-extension ms-azuretools.vscode-azurefunctions`

4. **Azure Tools** (ms-azuretools.azure-dev)
   - Azure resource management
   - Install: `code --install-extension ms-azuretools.azure-dev`

## Installation

### Option 1: Install via Command Line

```bash
# Essential for .NET development
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit

# Azure extensions (if compatible)
code --install-extension ms-azuretools.vscode-azurefunctions
code --install-extension ms-azuretools.azure-dev
```

### Option 2: Install via VS Code UI

1. Open VS Code/Cursor
2. Press `Ctrl+Shift+X` (or `Cmd+Shift+X` on Mac) to open Extensions
3. Search for each extension by name
4. Click "Install"

### Option 3: Use Recommended Extensions

VS Code will automatically prompt you to install recommended extensions when you open the workspace, or you can:
1. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
2. Type "Extensions: Show Recommended Extensions"
3. Click "Install All"

## Project Structure

Your Azure Function project is located at:
```
azure-functions/main/
├── main.csproj    # Project file
├── Program.cs                       # Entry point
├── SimpleTestFunction.cs           # HTTP trigger function
├── main.cs                          # Blob trigger (temporarily disabled)
├── host.json                       # Function app configuration
└── local.settings.json             # Local development settings (not committed)
```

## Local Development

### Prerequisites

- .NET 8.0 SDK installed
- Azure Functions Core Tools (optional, for local testing)

### Running Locally

1. Open terminal in VS Code (`Ctrl+`` ` or `View → Terminal`)
2. Navigate to function directory:
   ```bash
   cd azure-functions/main
   ```
3. Restore dependencies:
   ```bash
   dotnet restore
   ```
4. Build:
   ```bash
   dotnet build
   ```
5. Run locally (if Azure Functions Core Tools installed):
   ```bash
   func start
   ```

## Debugging

1. Set breakpoints in your function code
2. Press `F5` to start debugging
3. VS Code will use the `.vscode/launch.json` configuration (if present)

## Editing Functions

- **SimpleTestFunction.cs**: HTTP trigger function (currently active)
- **main.cs**: Blob trigger function (temporarily disabled)
- **Program.cs**: Dependency injection and configuration
- **host.json**: Function app settings (logging, version, etc.)

## Tips

- Use IntelliSense (`Ctrl+Space`) for code completion
- Check the Problems panel (`Ctrl+Shift+M`) for errors
- Use the integrated terminal for running dotnet commands
- The C# extension provides excellent debugging support

## Troubleshooting

If extensions don't install:
1. Check VS Code/Cursor version compatibility
2. Try installing extensions individually
3. Check extension marketplace for alternatives
4. For Cursor-specific issues, some Azure extensions may not be available

