# Script to manually trigger a GitLab CI/CD pipeline
# Usage: .\scripts\trigger-gitlab-pipeline.ps1 -BranchName "ready/test-pipeline-deployment" -GitLabToken "your-token"

param(
    [Parameter(Mandatory=$false)]
    [string]$BranchName = "ready/test-pipeline-deployment",
    
    [Parameter(Mandatory=$false)]
    [string]$GitLabToken = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectId = "mariomuja/interface-configurator"
)

# GitLab API endpoint
$GitLabApiUrl = "https://gitlab.com/api/v4"
$ProjectPath = $ProjectId -replace "/", "%2F"
$TriggerUrl = "$GitLabApiUrl/projects/$ProjectPath/pipeline"

Write-Host "Triggering GitLab pipeline for branch: $BranchName" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrEmpty($GitLabToken)) {
    Write-Host "⚠️  GitLab token not provided. You can:" -ForegroundColor Yellow
    Write-Host "   1. Get your token from: https://gitlab.com/-/user_settings/personal_access_tokens" -ForegroundColor Yellow
    Write-Host "   2. Create a token with 'api' scope" -ForegroundColor Yellow
    Write-Host "   3. Run this script with: -GitLabToken 'your-token'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternatively, trigger manually via GitLab UI:" -ForegroundColor Yellow
    Write-Host "   https://gitlab.com/$ProjectId/-/pipelines/new" -ForegroundColor Yellow
    exit 1
}

# Prepare the request
$headers = @{
    "PRIVATE-TOKEN" = $GitLabToken
    "Content-Type" = "application/json"
}

$body = @{
    ref = $BranchName
} | ConvertTo-Json

try {
    Write-Host "Sending request to GitLab API..." -ForegroundColor Cyan
    $response = Invoke-RestMethod -Uri $TriggerUrl -Method Post -Headers $headers -Body $body
    
    Write-Host "✅ Pipeline triggered successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Pipeline ID: $($response.id)" -ForegroundColor Green
    Write-Host "Status: $($response.status)" -ForegroundColor Green
    Write-Host "Branch: $($response.ref)" -ForegroundColor Green
    Write-Host ""
    Write-Host "View pipeline at:" -ForegroundColor Cyan
    Write-Host "   https://gitlab.com/$ProjectId/-/pipelines/$($response.id)" -ForegroundColor Yellow
    
} catch {
    Write-Host "❌ Error triggering pipeline:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
    
    exit 1
}

