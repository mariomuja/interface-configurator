# Setting Up Cursor for Azure Functions Development

## Quick Setup Guide

Since Cursor uses a different extension marketplace, follow these steps:

### Step 1: Open Extensions Panel

1. Press `Ctrl+Shift+X` (or `Cmd+Shift+X` on Mac)
2. Or click the Extensions icon in the sidebar

### Step 2: Search and Install Extensions

Search for and install these extensions:

#### Essential for C# Development:

1. **C#** 
   - Search: `C#` or `csharp`
   - Publisher: Microsoft (ms-dotnettools)
   - Provides: IntelliSense, debugging, code navigation

2. **C# Dev Kit** (Optional but recommended)
   - Search: `C# Dev Kit`
   - Publisher: Microsoft (ms-dotnettools)
   - Provides: Enhanced C# development experience

#### Azure Functions (if available):

3. **Azure Functions**
   - Search: `Azure Functions`
   - Publisher: Microsoft (ms-azuretools)
   - Provides: Function templates, deployment tools

### Step 3: Verify Installation

After installing, you should see:
- ✅ Syntax highlighting for `.cs` files
- ✅ IntelliSense when typing code
- ✅ Error squiggles for compilation errors
- ✅ Code navigation (Go to Definition, etc.)

## Manual Installation Alternative

If extensions aren't found in Cursor's marketplace:

### Option A: Use VS Code for Azure Functions Development

1. Install Visual Studio Code: https://code.visualstudio.com/
2. Install the Azure Functions extension
3. Open your project folder in VS Code

### Option B: Install Extensions via VSIX

1. Download extensions from VS Code marketplace:
   - C#: https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp
   - Azure Functions: https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions
2. In Cursor: `Ctrl+Shift+P` → "Extensions: Install from VSIX..."
3. Select the downloaded `.vsix` file

## Current Project Structure

Your Azure Function files:
```
azure-functions/main/
├── SimpleTestFunction.cs      ← Edit this HTTP trigger function
├── main.cs                     ← Blob trigger (currently disabled)
├── Program.cs                  ← Entry point & DI configuration
├── host.json                   ← Function app config
└── main.csproj ← Project file
```

## Basic Editing Without Extensions

Even without extensions, you can still edit:
- ✅ Edit `.cs` files (basic syntax highlighting may work)
- ✅ Use terminal for `dotnet build`, `dotnet run`
- ✅ Edit `host.json` (JSON syntax highlighting)
- ✅ Use Git integration

## Recommended Workflow

1. **Edit code** in Cursor (or VS Code with extensions)
2. **Build locally**: `dotnet build` in terminal
3. **Test locally**: `func start` (if Azure Functions Core Tools installed)
4. **Deploy**: Push to GitHub → GitHub Actions deploys automatically

## Troubleshooting

**No IntelliSense?**
- Check if C# extension is installed
- Restart Cursor
- Run `dotnet restore` in terminal

**Can't find extensions?**
- Cursor may have limited extension support
- Consider using VS Code for Azure Functions development
- Or use Cursor for editing and VS Code for debugging

**Extensions installed but not working?**
- Check extension status in Extensions panel
- Reload window: `Ctrl+Shift+P` → "Developer: Reload Window"
- Check Output panel for extension errors

