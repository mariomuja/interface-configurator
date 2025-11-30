# Script to sync a GitHub branch to GitLab using GitLab API
# This creates/updates the branch in GitLab so pipelines can run
# Usage: .\scripts\sync-branch-to-gitlab.ps1 -BranchName "ready/test-pipeline-deployment" -GitLabToken "your-token"

param(
    [Parameter(Mandatory=$true)]
    [string]$BranchName,
    
    [Parameter(Mandatory=$false)]
    [string]$GitLabToken = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectId = "mariomuja/interface-configurator",
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubRepo = "mariomuja/interface-configurator"
)

# GitLab API endpoint
$GitLabApiUrl = "https://gitlab.com/api/v4"
$ProjectPath = $ProjectId -replace "/", "%2F"

Write-Host "Syncing branch '$BranchName' from GitHub to GitLab..." -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrEmpty($GitLabToken)) {
    Write-Host "⚠️  GitLab token not provided. You can:" -ForegroundColor Yellow
    Write-Host "   1. Get your token from: https://gitlab.com/-/user_settings/personal_access_tokens" -ForegroundColor Yellow
    Write-Host "   2. Create a token with 'api' and 'write_repository' scope" -ForegroundColor Yellow
    Write-Host "   3. Run this script with: -GitLabToken 'your-token'" -ForegroundColor Yellow
    exit 1
}

# Get the latest commit SHA from GitHub for this branch
Write-Host "Getting latest commit from GitHub..." -ForegroundColor Cyan
try {
    $githubApiUrl = "https://api.github.com/repos/$GitHubRepo/git/refs/heads/$BranchName"
    $githubHeaders = @{
        "Accept" = "application/vnd.github.v3+json"
        "User-Agent" = "GitLab-Sync-Script"
    }
    
    $githubRef = Invoke-RestMethod -Uri $githubApiUrl -Method Get -Headers $githubHeaders
    $commitSha = $githubRef.object.sha
    Write-Host "✅ Found commit: $commitSha" -ForegroundColor Green
} catch {
    Write-Host "❌ Error getting commit from GitHub: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Create or update branch in GitLab
Write-Host "Creating/updating branch in GitLab..." -ForegroundColor Cyan
$headers = @{
    "PRIVATE-TOKEN" = $GitLabToken
    "Content-Type" = "application/json"
}

$branchUrl = "$GitLabApiUrl/projects/$ProjectPath/repository/branches"
$body = @{
    branch = $BranchName
    ref = $commitSha
} | ConvertTo-Json

try {
    # Try to create the branch
    $response = Invoke-RestMethod -Uri $branchUrl -Method Post -Headers $headers -Body $body
    
    Write-Host "✅ Branch '$BranchName' created/updated successfully in GitLab!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Branch: $($response.name)" -ForegroundColor Green
    Write-Host "Commit: $($response.commit.id)" -ForegroundColor Green
    Write-Host ""
    Write-Host "View branch at:" -ForegroundColor Cyan
    Write-Host "   https://gitlab.com/$ProjectId/-/tree/$BranchName" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Pipeline should be available at:" -ForegroundColor Cyan
    Write-Host "   https://gitlab.com/$ProjectId/-/pipelines?ref=$BranchName" -ForegroundColor Yellow
    
} catch {
    $errorResponse = $_.Exception.Response
    if ($errorResponse) {
        $reader = New-Object System.IO.StreamReader($errorResponse.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        
        # If branch already exists, try to update it
        if ($responseBody -like "*already exists*" -or $errorResponse.StatusCode -eq 400) {
            Write-Host "Branch already exists, updating..." -ForegroundColor Yellow
            
            # Update branch by creating it again (GitLab will update if it exists with same ref)
            try {
                $response = Invoke-RestMethod -Uri "$branchUrl/$($BranchName -replace '/', '%2F')" -Method Put -Headers $headers -Body $body
                Write-Host "✅ Branch updated successfully!" -ForegroundColor Green
            } catch {
                Write-Host "⚠️  Branch exists but couldn't update. It may already be in sync." -ForegroundColor Yellow
            }
        } else {
            Write-Host "❌ Error syncing branch:" -ForegroundColor Red
            Write-Host $responseBody -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

