# Script to configure GitLab CI/CD variables for GitHub sync
# This script sets up GITHUB_REPO_URL and optionally GITHUB_TOKEN in GitLab CI/CD variables

param(
    [Parameter(Mandatory=$true)]
    [string]$GitLabToken,
    
    [Parameter(Mandatory=$true)]
    [string]$GitLabProjectId = "mariomuja/interface-configurator",
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubRepoUrl = "https://github.com/mariomuja/interface-configurator.git",
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubToken = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$UseSSH = $false
)

Write-Host "Configuring GitLab CI/CD variables for GitHub sync..." -ForegroundColor Cyan
Write-Host ""

# Convert project ID to URL-encoded format
$ProjectPath = $GitLabProjectId -replace "/", "%2F"
$BaseUrl = "https://gitlab.com/api/v4/projects/$ProjectPath/variables"

$Headers = @{
    "PRIVATE-TOKEN" = $GitLabToken
    "Content-Type" = "application/json"
}

# Function to set or update a variable
function Set-GitLabVariable {
    param(
        [string]$Key,
        [string]$Value,
        [bool]$Protected = $false,
        [bool]$Masked = $false
    )
    
    Write-Host "Setting variable: $Key" -ForegroundColor Yellow
    
    # Check if variable exists
    $CheckUrl = "$BaseUrl/$Key"
    $CheckResponse = Invoke-RestMethod -Uri $CheckUrl -Method Get -Headers $Headers -ErrorAction SilentlyContinue
    
    if ($CheckResponse) {
        Write-Host "  Variable exists, updating..." -ForegroundColor Gray
        $Method = "PUT"
        $Url = $CheckUrl
    } else {
        Write-Host "  Creating new variable..." -ForegroundColor Gray
        $Method = "POST"
        $Url = $BaseUrl
    }
    
    $Body = @{
        key = $Key
        value = $Value
        protected = $Protected
        masked = $Masked
    } | ConvertTo-Json
    
    try {
        $Response = Invoke-RestMethod -Uri $Url -Method $Method -Headers $Headers -Body $Body -ContentType "application/json"
        Write-Host "  ✅ Successfully set $Key" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "  ❌ Failed to set $Key" -ForegroundColor Red
        Write-Host "     Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "     Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
        }
        return $false
    }
}

# Set GITHUB_REPO_URL
if ($UseSSH) {
    $GitHubUrl = $GitHubRepoUrl -replace "https://github.com/", "git@github.com:"
    $GitHubUrl = $GitHubUrl -replace "\.git$", ".git"
} else {
    $GitHubUrl = $GitHubRepoUrl
}

Write-Host "Setting GITHUB_REPO_URL to: $GitHubUrl" -ForegroundColor Cyan
$Success1 = Set-GitLabVariable -Key "GITHUB_REPO_URL" -Value $GitHubUrl -Protected $false -Masked $false

Write-Host ""

# Set GITHUB_TOKEN if provided
if ($GitHubToken -and -not $UseSSH) {
    Write-Host "Setting GITHUB_TOKEN (masked)..." -ForegroundColor Cyan
    $Success2 = Set-GitLabVariable -Key "GITHUB_TOKEN" -Value $GitHubToken -Protected $false -Masked $true
} elseif ($UseSSH) {
    Write-Host "Using SSH authentication - GITHUB_TOKEN not needed" -ForegroundColor Yellow
    $Success2 = $true
} else {
    Write-Host "⚠️  GITHUB_TOKEN not provided. You may need to:" -ForegroundColor Yellow
    Write-Host "   1. Set it manually in GitLab UI, or" -ForegroundColor Yellow
    Write-Host "   2. Configure SSH keys for authentication" -ForegroundColor Yellow
    $Success2 = $true
}

Write-Host ""
Write-Host "=== Configuration Summary ===" -ForegroundColor Cyan
Write-Host "GITHUB_REPO_URL: $GitHubUrl" -ForegroundColor $(if ($Success1) { "Green" } else { "Red" })
if ($GitHubToken -and -not $UseSSH) {
    Write-Host "GITHUB_TOKEN: ✅ Set (masked)" -ForegroundColor $(if ($Success2) { "Green" } else { "Red" })
} elseif ($UseSSH) {
    Write-Host "Authentication: SSH (configure SSH keys separately)" -ForegroundColor Yellow
} else {
    Write-Host "GITHUB_TOKEN: ⚠️  Not set" -ForegroundColor Yellow
}

Write-Host ""
if ($Success1 -and $Success2) {
    Write-Host "✅ Configuration complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Push a commit to GitLab" -ForegroundColor White
    Write-Host "  2. Check the 'sync:push-to-github' job in the pipeline" -ForegroundColor White
    Write-Host "  3. Verify the commit appears in GitHub" -ForegroundColor White
} else {
    Write-Host "⚠️  Some variables failed to set. Please check the errors above." -ForegroundColor Yellow
}

