# Check Status of Adapter Container Images in Azure Container Registry
# Shows which images exist and when they were last updated

param(
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistryName,
    
    [string]$ResourceGroup = "rg-interface-configuration",
    
    [string[]]$AdapterTypes = @("csv", "sqlserver", "sap", "dynamics365", "crm", "file", "sftp")
)

$ErrorActionPreference = "Continue"

Write-Host "`n=== Adapter Container Images Status Check ===" -ForegroundColor Cyan
Write-Host "Container Registry: $ContainerRegistryName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host ""

# Step 1: Verify Container Registry exists
Write-Host "[1] Verifying Container Registry..." -ForegroundColor Yellow
$acr = az acr show --name $ContainerRegistryName --resource-group $ResourceGroup --query "{Name:name, LoginServer:loginServer}" -o json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Container Registry not found: $ContainerRegistryName" -ForegroundColor Red
    Write-Host "Error: $acr" -ForegroundColor Red
    exit 1
}

$acrInfo = $acr | ConvertFrom-Json
$loginServer = $acrInfo.loginServer
Write-Host "✅ Container Registry found: $loginServer" -ForegroundColor Green
Write-Host ""

# Step 2: Check each adapter image
Write-Host "[2] Checking adapter images..." -ForegroundColor Yellow
Write-Host ""

$allImagesExist = $true
$imageStatus = @()

foreach ($adapterType in $AdapterTypes) {
    $imageName = "${adapterType}-adapter"
    $repositoryName = $imageName
    
    Write-Host "  Checking: $imageName" -ForegroundColor Cyan
    
    # Check if repository exists
    $repoExists = az acr repository show --name $ContainerRegistryName --repository $repositoryName --query "name" -o tsv 2>&1
    if ($LASTEXITCODE -eq 0 -and $repoExists) {
        # Get image tags with timestamps
        $tags = az acr repository show-tags --name $ContainerRegistryName --repository $repositoryName --orderby time_desc --top 1 --query "[0].{Tag:name, Timestamp:timestamp}" -o json 2>&1
        
        if ($LASTEXITCODE -eq 0 -and $tags) {
            $tagInfo = $tags | ConvertFrom-Json
            
            if ($tagInfo) {
                $timestamp = [DateTimeOffset]::FromUnixTimeSeconds($tagInfo.timestamp).LocalDateTime
                $age = (Get-Date) - $timestamp
                
                $status = @{
                    AdapterType = $adapterType
                    ImageName = $imageName
                    Exists = $true
                    LatestTag = $tagInfo.Tag
                    LastUpdated = $timestamp
                    Age = $age
                    AgeDays = [math]::Round($age.TotalDays, 1)
                    Status = if ($age.TotalDays -lt 7) { "✅ Recent" } elseif ($age.TotalDays -lt 30) { "⚠️  Old" } else { "❌ Very Old" }
                }
                
                Write-Host "    ✅ Image exists" -ForegroundColor Green
                Write-Host "    Latest tag: $($tagInfo.Tag)" -ForegroundColor White
                Write-Host "    Last updated: $($timestamp.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
                Write-Host "    Age: $([math]::Round($age.TotalDays, 1)) days ($($status.Status))" -ForegroundColor $(if ($age.TotalDays -lt 7) { "Green" } elseif ($age.TotalDays -lt 30) { "Yellow" } else { "Red" })
                
                if ($age.TotalDays -ge 30) {
                    $allImagesExist = $false
                }
            } else {
                $status = @{
                    AdapterType = $adapterType
                    ImageName = $imageName
                    Exists = $false
                    Status = "❌ No tags found"
                }
                Write-Host "    ⚠️  Repository exists but no tags found" -ForegroundColor Yellow
                $allImagesExist = $false
            }
        } else {
            $status = @{
                AdapterType = $adapterType
                ImageName = $imageName
                Exists = $false
                Status = "❌ Error checking tags"
            }
            Write-Host "    ❌ Error checking tags: $tags" -ForegroundColor Red
            $allImagesExist = $false
        }
    } else {
        $status = @{
            AdapterType = $adapterType
            ImageName = $imageName
            Exists = $false
            Status = "❌ Image not found"
        }
        Write-Host "    ❌ Image not found in registry" -ForegroundColor Red
        $allImagesExist = $false
    }
    
    $imageStatus += $status
    Write-Host ""
}

# Step 3: Summary
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host ""

$existingCount = ($imageStatus | Where-Object { $_.Exists }).Count
$missingCount = ($imageStatus | Where-Object { -not $_.Exists }).Count
$oldCount = ($imageStatus | Where-Object { $_.Exists -and $_.AgeDays -ge 30 }).Count
$recentCount = ($imageStatus | Where-Object { $_.Exists -and $_.AgeDays -lt 7 }).Count

Write-Host "Total adapter types: $($AdapterTypes.Count)" -ForegroundColor White
Write-Host "Images found: $existingCount" -ForegroundColor $(if ($existingCount -eq $AdapterTypes.Count) { "Green" } else { "Yellow" })
Write-Host "Images missing: $missingCount" -ForegroundColor $(if ($missingCount -eq 0) { "Green" } else { "Red" })
Write-Host "Recent images (< 7 days): $recentCount" -ForegroundColor $(if ($recentCount -eq $existingCount) { "Green" } else { "Yellow" })
Write-Host "Old images (>= 30 days): $oldCount" -ForegroundColor $(if ($oldCount -eq 0) { "Green" } else { "Red" })
Write-Host ""

# Step 4: Detailed table
Write-Host "=== Detailed Status ===" -ForegroundColor Cyan
Write-Host ""

$table = $imageStatus | ForEach-Object {
    [PSCustomObject]@{
        Adapter = $_.AdapterType
        Image = $_.ImageName
        Status = if ($_.Exists) { 
            if ($_.AgeDays -lt 7) { "✅ Recent" } 
            elseif ($_.AgeDays -lt 30) { "⚠️  Old ($($_.AgeDays) days)" } 
            else { "❌ Very Old ($($_.AgeDays) days)" }
        } else {
            "❌ Missing"
        }
        LastUpdated = if ($_.Exists -and $_.LastUpdated) { $_.LastUpdated.ToString('yyyy-MM-dd') } else { "N/A" }
        LatestTag = if ($_.Exists -and $_.LatestTag) { $_.LatestTag } else { "N/A" }
    }
}

$table | Format-Table -AutoSize

# Step 5: Recommendations
Write-Host "=== Recommendations ===" -ForegroundColor Cyan
Write-Host ""

if ($missingCount -gt 0) {
    $missing = $imageStatus | Where-Object { -not $_.Exists }
    Write-Host "❌ Missing images that need to be built:" -ForegroundColor Red
    foreach ($img in $missing) {
        Write-Host "  - $($img.ImageName)" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "To build missing images, run:" -ForegroundColor Yellow
    Write-Host "  .\scripts\build-and-push-adapter-images-acr.ps1 -ContainerRegistryName $ContainerRegistryName" -ForegroundColor White
    Write-Host ""
}

if ($oldCount -gt 0) {
    $old = $imageStatus | Where-Object { $_.Exists -and $_.AgeDays -ge 30 }
    Write-Host "⚠️  Old images that should be updated (>= 30 days old):" -ForegroundColor Yellow
    foreach ($img in $old) {
        Write-Host "  - $($img.ImageName) (last updated: $($img.AgeDays) days ago)" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "To update old images, run:" -ForegroundColor Yellow
    Write-Host "  .\scripts\build-and-push-adapter-images-acr.ps1 -ContainerRegistryName $ContainerRegistryName" -ForegroundColor White
    Write-Host ""
}

if ($missingCount -eq 0 -and $oldCount -eq 0) {
    Write-Host "✅ All adapter images are up to date!" -ForegroundColor Green
    Write-Host ""
}

Write-Host "=== Check Complete ===" -ForegroundColor Green








