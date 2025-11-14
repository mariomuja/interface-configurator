# Azure Bicep Installation Guide

## Installation

Azure Bicep CLI has been installed on your system using winget:

```powershell
winget install --id Microsoft.Bicep --accept-source-agreements --accept-package-agreements
```

## Verification

After installation, restart your PowerShell session or refresh the PATH environment variable:

```powershell
# Refresh PATH
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

# Verify installation
bicep --version
```

Expected output:
```
Bicep CLI version 0.38.33 (6bb5d5f859)
```

## Alternative: Using Azure CLI's Built-in Bicep

If the standalone Bicep CLI is not available, Azure CLI includes built-in Bicep support. No separate installation is needed - just use `az deployment group create` with `.bicep` files directly.

## Next Steps

1. Review the `README.md` in this directory for deployment instructions
2. Configure parameters in `parameters.json`
3. Validate your template: `.\validate.ps1`
4. Deploy: `.\deploy.ps1`

## Troubleshooting

### Bicep Command Not Found

If `bicep` command is not found after installation:

1. **Restart PowerShell** - PATH changes require a new session
2. **Refresh PATH manually**:
   ```powershell
   $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
   ```
3. **Use Azure CLI instead** - Azure CLI has built-in Bicep support:
   ```powershell
   az deployment group create --resource-group <rg-name> --template-file main.bicep --parameters parameters.json
   ```

### Installation Issues

If winget installation fails:

1. **Use Azure CLI to install Bicep**:
   ```powershell
   az bicep install
   ```

2. **Manual installation**:
   - Download from: https://github.com/Azure/bicep/releases
   - Add to PATH manually

## Resources

- [Azure Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Bicep GitHub Repository](https://github.com/Azure/bicep)
- [Bicep Language Specification](https://github.com/Azure/bicep/blob/main/docs/spec/bicep.md)

