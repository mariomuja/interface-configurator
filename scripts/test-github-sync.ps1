# Script to test GitHub sync by pushing a test commit to GitLab
# This will trigger the sync:push-to-github job

param(
    [Parameter(Mandatory=$false)]
    [string]$BranchName = "ready/test-pipeline-deployment",
    
    [Parameter(Mandatory=$false)]
    [string]$TestMessage = "test: verify GitHub sync from GitLab"
)

Write-Host "Testing GitHub sync from GitLab..." -ForegroundColor Cyan
Write-Host ""

# Check if we're on the right branch
$CurrentBranch = git branch --show-current
if ($CurrentBranch -ne $BranchName) {
    Write-Host "⚠️  Current branch is '$CurrentBranch', but testing branch is '$BranchName'" -ForegroundColor Yellow
    Write-Host "   Switching to branch '$BranchName'..." -ForegroundColor Yellow
    git checkout $BranchName 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Could not switch to branch '$BranchName'" -ForegroundColor Red
        exit 1
    }
}

# Create a test file
$TestFile = ".github-sync-test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$TestContent = @"
GitHub Sync Test File
Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Branch: $BranchName
This file is used to test the GitLab → GitHub sync functionality.
"@

Write-Host "Creating test file: $TestFile" -ForegroundColor Yellow
Set-Content -Path $TestFile -Value $TestContent

# Add and commit
Write-Host "Staging test file..." -ForegroundColor Yellow
git add $TestFile

Write-Host "Committing test file..." -ForegroundColor Yellow
git commit -m $TestMessage

Write-Host ""
Write-Host "Pushing to GitLab (origin)..." -ForegroundColor Cyan
git push origin $BranchName

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Test commit pushed to GitLab successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Go to GitLab pipeline: https://gitlab.com/mariomuja/interface-configurator/-/pipelines" -ForegroundColor White
    Write-Host "  2. Check the 'sync:push-to-github' job" -ForegroundColor White
    Write-Host "  3. Wait for the job to complete" -ForegroundColor White
    Write-Host "  4. Verify the commit appears in GitHub:" -ForegroundColor White
    Write-Host "     https://github.com/mariomuja/interface-configurator/commits/$BranchName" -ForegroundColor White
    Write-Host ""
    Write-Host "Test file: $TestFile" -ForegroundColor Gray
    Write-Host "You can delete this file after verifying the sync works." -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "❌ Failed to push to GitLab" -ForegroundColor Red
    Write-Host "   Please check your GitLab remote configuration" -ForegroundColor Red
}

