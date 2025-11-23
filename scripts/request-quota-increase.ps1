# Request quota increase for Dynamic VMs in North Europe
# This creates a support request for quota increase

param(
    [string]$Location = "North Europe",
    [string]$QuotaType = "DynamicVMs",
    [int]$RequestedLimit = 10
)

Write-Host "`n=== Requesting Quota Increase ===" -ForegroundColor Cyan
Write-Host "Location: $Location" -ForegroundColor White
Write-Host "Quota Type: $QuotaType" -ForegroundColor White
Write-Host "Requested Limit: $RequestedLimit" -ForegroundColor White

Write-Host "`n⚠️  Note: Quota increases must be requested through Azure Portal:" -ForegroundColor Yellow
Write-Host "1. Go to: https://portal.azure.com" -ForegroundColor White
Write-Host "2. Navigate to: Subscriptions > Your Subscription > Usage + quotas" -ForegroundColor White
Write-Host "3. Search for: 'Dynamic VMs' in 'North Europe'" -ForegroundColor White
Write-Host "4. Click 'Request increase'" -ForegroundColor White
Write-Host "5. Request limit: $RequestedLimit" -ForegroundColor White

Write-Host "`nOR use Azure Support Center:" -ForegroundColor Yellow
Write-Host "https://portal.azure.com/#blade/Microsoft_Azure_Support/HelpAndSupportBlade/newsupportrequest" -ForegroundColor Cyan

Write-Host "`nIssue Type: Service and subscription limits (quotas)" -ForegroundColor White
Write-Host "Subscription: Your subscription" -ForegroundColor White
Write-Host "Quota Type: Compute-VM (cores-vCPUs) subscription limit increases" -ForegroundColor White
Write-Host "Region: North Europe" -ForegroundColor White
Write-Host "SKU Family: Dynamic" -ForegroundColor White
Write-Host "New Limit: $RequestedLimit" -ForegroundColor White

