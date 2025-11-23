# Fix Sync Trigger Settings for Azure Function App
# This script sets the required app settings to fix sync trigger failures

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$true)]
    [string]$FunctionAppName
)

Write-Host "`n=== Fix Sync Trigger Settings ===" -ForegroundColor Cyan
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White

# Critical app settings for .NET Isolated Worker functions
Write-Host "`nSetting critical app settings..." -ForegroundColor Yellow

$settings = @{
    FUNCTIONS_EXTENSION_VERSION = "~4"
    SCM_DO_BUILD_DURING_DEPLOYMENT = "false"
    ENABLE_ORYX_BUILD = "false"
    WEBSITE_USE_PLACEHOLDER = "0"
}

# Convert to JSON format for Azure CLI
$settingsJson = $settings | ConvertTo-Json -Compress
$tempSettingsFile = [System.IO.Path]::GetTempFileName()
$settingsJson | Out-File -FilePath $tempSettingsFile -Encoding UTF8 -NoNewline

try {
    Write-Host "Applying settings..." -ForegroundColor Gray
    $result = az functionapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $FunctionAppName `
        --settings "@$tempSettingsFile" `
        --output json 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ App settings applied successfully" -ForegroundColor Green
        
        # Verify settings
        Write-Host "`nVerifying settings..." -ForegroundColor Yellow
        foreach ($key in $settings.Keys) {
            $value = az functionapp config appsettings list `
                --resource-group $ResourceGroup `
                --name $FunctionAppName `
                --query "[?name=='$key'].value" `
                -o tsv 2>$null
            
            if ($value -eq $settings[$key]) {
                Write-Host "  ✅ $key = $value" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️  $key = $value (expected: $($settings[$key]))" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "❌ Failed to apply settings" -ForegroundColor Red
        Write-Host $result
        exit 1
    }
} finally {
    Remove-Item $tempSettingsFile -ErrorAction SilentlyContinue
}

# Sync triggers
Write-Host "`nSynchronizing function triggers..." -ForegroundColor Yellow

# Method 1: Azure CLI
Write-Host "  Trying Azure CLI sync..." -ForegroundColor Gray
$syncResult = az functionapp function sync `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output none 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✅ Azure CLI sync successful" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  Azure CLI sync failed (trying alternative method)" -ForegroundColor Yellow
}

# Method 2: Admin API
Write-Host "  Trying Admin API sync..." -ForegroundColor Gray
try {
    $masterKey = az functionapp keys list `
        --resource-group $ResourceGroup `
        --name $FunctionAppName `
        --query "masterKey" `
        -o tsv 2>$null
    
    if ($masterKey -and $masterKey -ne "None") {
        $syncUrl = "https://$FunctionAppName.azurewebsites.net/admin/host/synctriggers"
        $headers = @{
            "x-functions-key" = $masterKey
            "Content-Type" = "application/json"
        }
        
        $syncResponse = Invoke-RestMethod `
            -Uri $syncUrl `
            -Method Post `
            -Headers $headers `
            -TimeoutSec 60 `
            -ErrorAction Stop
        
        Write-Host "  ✅ Admin API sync successful" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Master key not available" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠️  Admin API sync failed: $_" -ForegroundColor Yellow
}

# Wait for sync to complete
Write-Host "`nWaiting for sync to complete..." -ForegroundColor Yellow
Start-Sleep -Seconds 20

# Check functions
Write-Host "`nChecking functions..." -ForegroundColor Yellow
$functionsOutput = az functionapp function list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    -o json 2>&1

if ($LASTEXITCODE -eq 0) {
    try {
        $functions = $functionsOutput | ConvertFrom-Json
        if ($functions -and $functions.Count -gt 0) {
            Write-Host "✅ Functions discovered: $($functions.Count) functions found" -ForegroundColor Green
            $functions | Select-Object name, language | Format-Table -AutoSize
        } else {
            Write-Host "⚠️  No functions discovered yet" -ForegroundColor Yellow
            Write-Host "   This may be normal if the function app is still initializing." -ForegroundColor Gray
            Write-Host "   Functions should appear after a few minutes." -ForegroundColor Gray
        }
    } catch {
        Write-Host "⚠️  Could not parse functions list" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  Could not retrieve functions list" -ForegroundColor Yellow
}

Write-Host "`n✅ Sync trigger settings fix completed!" -ForegroundColor Green
Write-Host "`nIf functions are still not listed, try:" -ForegroundColor Cyan
Write-Host "  1. Restart the function app: az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor White
Write-Host "  2. Wait 2-3 minutes for initialization" -ForegroundColor White
Write-Host "  3. Check the Azure Portal for function status" -ForegroundColor White

