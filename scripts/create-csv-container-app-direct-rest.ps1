# Create CSV Adapter Container App directly using Azure REST API
# This bypasses the Function App and creates the container app directly

param(
    [string]$ResourceGroup = "rg-interface-configurator",
    [string]$ContainerAppEnvironment = "cae-adapter-instances",
    [string]$Location = "centralus",
    [string]$RegistryServer = "acrinterfaceconfig.azurecr.io",
    [string]$InterfaceName = "TestInterface-CSV-Direct",
    [string]$InstanceName = "CSV-Source-Test"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Create CSV Adapter Container App (Direct REST API) ===" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Environment: $ContainerAppEnvironment" -ForegroundColor White
Write-Host ""

# Generate GUID for adapter instance
$adapterInstanceGuid = [System.Guid]::NewGuid()
$guidNoDashes = $adapterInstanceGuid.ToString("N")
$containerAppName = "ca-$($guidNoDashes.Substring(0, [Math]::Min(24, $guidNoDashes.Length)))"
Write-Host "Generated Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
Write-Host "Container App Name: $containerAppName" -ForegroundColor White
Write-Host ""

# Get access token
Write-Host "[1] Getting Azure access token..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$tokenResult = az account get-access-token --query "{token:accessToken}" -o json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = "Stop"

if (-not $tokenResult -or -not $tokenResult.token) {
    Write-Host "❌ Failed to get access token" -ForegroundColor Red
    exit 1
}

$accessToken = $tokenResult.token
Write-Host "✅ Access token retrieved" -ForegroundColor Green
Write-Host ""

# Get ACR credentials
Write-Host "[2] Getting ACR credentials..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$acrCredentials = az acr credential show --name "acrinterfaceconfig" --resource-group $ResourceGroup --query "{username:username, password:passwords[0].value}" -o json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = "Stop"

if (-not $acrCredentials -or -not $acrCredentials.username) {
    Write-Host "❌ Failed to get ACR credentials" -ForegroundColor Red
    exit 1
}

Write-Host "✅ ACR credentials retrieved" -ForegroundColor Green
Write-Host ""

# Get storage account connection string
Write-Host "[3] Getting storage account..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$storageAccount = az storage account list --resource-group $ResourceGroup --query "[0].{Name:name}" -o json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = "Stop"

if (-not $storageAccount -or -not $storageAccount.Name) {
    Write-Host "❌ Failed to get storage account" -ForegroundColor Red
    exit 1
}

$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $storageAccount.Name --query "[0].value" -o tsv 2>&1
$blobConnectionString = "DefaultEndpointsProtocol=https;AccountName=$($storageAccount.Name);AccountKey=$storageKey;EndpointSuffix=core.windows.net"
Write-Host "✅ Storage connection string retrieved" -ForegroundColor Green
Write-Host ""

# Get Service Bus connection string
Write-Host "[4] Getting Service Bus connection string..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$serviceBusNamespace = az servicebus namespace list --resource-group $ResourceGroup --query "[0].{Name:name}" -o json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = "Stop"

if ($serviceBusNamespace) {
    $serviceBusKey = az servicebus namespace authorization-rule keys list --resource-group $ResourceGroup --namespace-name $serviceBusNamespace.Name --name "RootManageSharedAccessKey" --query "primaryConnectionString" -o tsv 2>&1
    $serviceBusConnectionString = $serviceBusKey
} else {
    $serviceBusConnectionString = ""
    Write-Host "⚠️  Service Bus namespace not found, using empty connection string" -ForegroundColor Yellow
}
Write-Host "✅ Service Bus connection string retrieved" -ForegroundColor Green
Write-Host ""

# Get environment resource ID
$subscriptionId = "f1e8e2a3-2bf1-43f0-8f19-37abd624205c"
$environmentResourceId = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/managedEnvironments/$ContainerAppEnvironment"

# Create container app using Azure REST API
Write-Host "[5] Creating Container App via Azure REST API..." -ForegroundColor Yellow
Write-Host "⏱️  Timer started..." -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$containerAppUrl = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/containerApps/$containerAppName`?api-version=2023-05-01"

# Container app body
$containerAppBody = @{
    location = $Location
    properties = @{
        managedEnvironmentId = $environmentResourceId
        configuration = @{
            ingress = @{
                external = $false
                targetPort = 8080
                transport = "http"
            }
            registries = @(
                @{
                    server = $RegistryServer
                    username = $acrCredentials.username
                    passwordSecretRef = "registry-password"
                }
            )
            secrets = @(
                @{
                    name = "registry-password"
                    value = $acrCredentials.password
                }
                @{
                    name = "blob-connection-string"
                    value = $blobConnectionString
                }
                @{
                    name = "servicebus-connection-string"
                    value = $serviceBusConnectionString
                }
            )
        }
        template = @{
            containers = @(
                @{
                    name = $containerAppName
                    image = "$RegistryServer/csv-adapter:latest"
                    env = @(
                        @{
                            name = "ADAPTER_INSTANCE_GUID"
                            value = $adapterInstanceGuid.ToString()
                        }
                        @{
                            name = "ADAPTER_NAME"
                            value = "CSV"
                        }
                        @{
                            name = "ADAPTER_TYPE"
                            value = "Source"
                        }
                        @{
                            name = "INTERFACE_NAME"
                            value = $InterfaceName
                        }
                        @{
                            name = "INSTANCE_NAME"
                            value = $InstanceName
                        }
                        @{
                            name = "BLOB_CONNECTION_STRING"
                            secretRef = "blob-connection-string"
                        }
                        @{
                            name = "BLOB_CONTAINER_NAME"
                            value = "adapter-$($adapterInstanceGuid.ToString('N').Substring(0, 8))"
                        }
                        @{
                            name = "ADAPTER_CONFIG_PATH"
                            value = "adapter-config.json"
                        }
                        @{
                            name = "AZURE_SERVICEBUS_CONNECTION_STRING"
                            secretRef = "servicebus-connection-string"
                        }
                    )
                    resources = @{
                        cpu = 0.25
                        memory = "0.5Gi"
                    }
                }
            )
            scale = @{
                minReplicas = 1
                maxReplicas = 1
            }
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type" = "application/json"
    }
    
    $response = Invoke-RestMethod `
        -Uri $containerAppUrl `
        -Method Put `
        -Body $containerAppBody `
        -Headers $headers `
        -ErrorAction Stop
    
    $stopwatch.Stop()
    $endTime = Get-Date
    $duration = $stopwatch.Elapsed
    
    Write-Host "✅ Container App Created Successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== Timing Results ===" -ForegroundColor Cyan
    Write-Host "Start Time:    $($startTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))" -ForegroundColor White
    Write-Host "End Time:      $($endTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))" -ForegroundColor White
    Write-Host "Duration:      $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host "               $([math]::Round($duration.TotalMinutes, 2)) minutes" -ForegroundColor White
    Write-Host ""
    
    Write-Host "=== Container App Details ===" -ForegroundColor Cyan
    Write-Host "Container App Name: $containerAppName" -ForegroundColor White
    Write-Host "Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
    Write-Host "Adapter Name: CSV" -ForegroundColor White
    Write-Host "Adapter Type: Source" -ForegroundColor White
    Write-Host "Interface Name: $InterfaceName" -ForegroundColor White
    Write-Host "Instance Name: $InstanceName" -ForegroundColor White
    Write-Host "Provisioning State: $($response.properties.provisioningState)" -ForegroundColor $(if ($response.properties.provisioningState -eq "Succeeded") { "Green" } else { "Yellow" })
    Write-Host ""
    
    Write-Host "=== Summary ===" -ForegroundColor Cyan
    Write-Host "Container App creation took: $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: The container app is now being provisioned." -ForegroundColor Gray
    Write-Host "You can check the status in Azure Portal:" -ForegroundColor White
    Write-Host "https://portal.azure.com/#@mariomujagmail508.onmicrosoft.com/resource/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.App/containerApps/$containerAppName" -ForegroundColor Cyan
    
} catch {
    $stopwatch.Stop()
    $duration = $stopwatch.Elapsed
    
    Write-Host "❌ Failed to create Container App" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host ""
            Write-Host "Response Body:" -ForegroundColor Yellow
            Write-Host $responseBody -ForegroundColor Gray
        } catch {
            # Ignore errors reading response stream
        }
    }
    
    if ($_.ErrorDetails) {
        Write-Host ""
        Write-Host "Error Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Duration before failure: $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Yellow
    exit 1
}








