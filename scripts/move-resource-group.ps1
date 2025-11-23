# Move all resources from rg-infrastructure-as-code to rg-interface-configurator

param(
    [string]$SourceResourceGroup = "rg-infrastructure-as-code",
    [string]$TargetResourceGroup = "rg-interface-configurator"
)

Write-Host "`n=== Moving Resources to New Resource Group ===" -ForegroundColor Cyan
Write-Host "Source: $SourceResourceGroup" -ForegroundColor White
Write-Host "Target: $TargetResourceGroup" -ForegroundColor White

# Get all resource IDs
Write-Host "`nGetting list of resources..." -ForegroundColor Yellow
$resourceIds = az resource list --resource-group $SourceResourceGroup --query "[].id" -o tsv

if ([string]::IsNullOrWhiteSpace($resourceIds)) {
    Write-Host "❌ No resources found in source resource group" -ForegroundColor Red
    exit 1
}

$resourceArray = $resourceIds -split "`n" | Where-Object { $_ -and $_.Trim() -ne "" }
Write-Host "Found $($resourceArray.Count) resources to move" -ForegroundColor Green

# Filter out child resources (they move with their parents)
# SQL databases are child resources and will move with sql-main-database server
# Event Grid topics might be child resources
$parentResources = @()
foreach ($resourceId in $resourceArray) {
    # Skip child resources - they move with their parents
    # Child resources have more than 8 segments in the path
    $segments = ($resourceId -split '/').Count
    
    # Top-level resources typically have 9 segments: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
    # Child resources have more segments
    if ($segments -gt 9) {
        $resourceName = ($resourceId -split '/')[-1]
        Write-Host "Skipping child resource: $resourceName (will move with parent)" -ForegroundColor Gray
        continue
    }
    
    # Also skip if it's a database (databases are always child resources)
    if ($resourceId -match '/databases/') {
        $resourceName = ($resourceId -split '/')[-1]
        Write-Host "Skipping database child resource: $resourceName (will move with sql-main-database)" -ForegroundColor Gray
        continue
    }
    
    $parentResources += $resourceId
}

Write-Host "`nMoving $($parentResources.Count) parent resources..." -ForegroundColor Yellow

# Move resources in batches (Azure allows moving multiple resources at once)
$batchSize = 10
$batches = @()
for ($i = 0; $i -lt $parentResources.Count; $i += $batchSize) {
    $batch = $parentResources[$i..([Math]::Min($i + $batchSize - 1, $parentResources.Count - 1))]
    $batches += ,$batch
}

$movedCount = 0
$failedCount = 0

foreach ($batch in $batches) {
    $resourceIdsJson = $batch | ConvertTo-Json
    
    Write-Host "`nMoving batch of $($batch.Count) resources..." -ForegroundColor Yellow
    foreach ($resourceId in $batch) {
        $resourceName = ($resourceId -split '/')[-1]
        Write-Host "  - $resourceName" -ForegroundColor Gray
    }
    
    try {
        # Use az group move command
        $moveResult = az resource move --ids $batch --destination-group $TargetResourceGroup --output json 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Batch moved successfully" -ForegroundColor Green
            $movedCount += $batch.Count
        } else {
            Write-Host "⚠ Warning during move (may still succeed): $moveResult" -ForegroundColor Yellow
            # Check if resources were actually moved
            foreach ($resourceId in $batch) {
                $resourceName = ($resourceId -split '/')[-1]
                $check = az resource show --ids $resourceId --query "resourceGroup" -o tsv 2>&1
                if ($check -eq $TargetResourceGroup) {
                    Write-Host "  ✅ $resourceName was moved successfully" -ForegroundColor Green
                    $movedCount++
                } else {
                    Write-Host "  ❌ $resourceName move failed" -ForegroundColor Red
                    $failedCount++
                }
            }
        }
    }
    catch {
        Write-Host "❌ Error moving batch: $($_.Exception.Message)" -ForegroundColor Red
        $failedCount += $batch.Count
    }
}

Write-Host "`n=== Move Summary ===" -ForegroundColor Cyan
Write-Host "Successfully moved: $movedCount resources" -ForegroundColor Green
if ($failedCount -gt 0) {
    Write-Host "Failed to move: $failedCount resources" -ForegroundColor Red
}

# Verify move
Write-Host "`nVerifying resources in target resource group..." -ForegroundColor Yellow
$targetResources = az resource list --resource-group $TargetResourceGroup --output table
Write-Host $targetResources

Write-Host "`n=== Done ===" -ForegroundColor Cyan

