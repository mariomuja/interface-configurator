# Monitor Application Insights for Retry Patterns
# Queries Application Insights for database retry events and patterns

param(
    [string]$AppInsightsName = "func-integration-main-insights",
    [string]$ResourceGroup = "rg-infrastructure-as-code",
    [int]$Hours = 24
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Application Insights Retry Pattern Monitor" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Get Application Insights connection details
Write-Host "Getting Application Insights details..." -ForegroundColor Yellow
$appInsights = az monitor app-insights component show `
    --app $AppInsightsName `
    --resource-group $ResourceGroup `
    --query "{appId:appId, instrumentationKey:instrumentationKey}" `
    --output json 2>&1 | ConvertFrom-Json

if (-not $appInsights -or -not $appInsights.appId) {
    Write-Host "❌ ERROR: Could not retrieve Application Insights information" -ForegroundColor Red
    Write-Host "Make sure Application Insights is configured for the Function App" -ForegroundColor Yellow
    exit 1
}

Write-Host "Application Insights App ID: $($appInsights.appId)" -ForegroundColor Cyan
Write-Host ""

# Note: To query Application Insights, you need to use the REST API or Azure CLI
# For now, we'll provide instructions and a query template

Write-Host "Application Insights Query Instructions:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Go to Azure Portal > Application Insights > $AppInsightsName" -ForegroundColor White
Write-Host "2. Navigate to 'Logs' section" -ForegroundColor White
Write-Host "3. Run the following queries:" -ForegroundColor White
Write-Host ""

Write-Host "=== Query 1: Database Retry Events ===" -ForegroundColor Cyan
$query1 = @"
traces
| where message contains "retry" or message contains "Retry"
| where timestamp > ago($Hours hours)
| summarize count() by bin(timestamp, 1h), message
| order by timestamp desc
"@
Write-Host $query1 -ForegroundColor Gray
Write-Host ""

Write-Host "=== Query 2: SQL Exceptions ===" -ForegroundColor Cyan
$query2 = @"
exceptions
| where type contains "SqlException" or type contains "TimeoutException"
| where timestamp > ago($Hours hours)
| summarize count() by bin(timestamp, 1h), type, outerMessage
| order by timestamp desc
"@
Write-Host $query2 -ForegroundColor Gray
Write-Host ""

Write-Host "=== Query 3: Database Connection Failures ===" -ForegroundColor Cyan
$query3 = @"
traces
| where message contains "database" or message contains "Database"
| where severityLevel >= 3  // Warning or Error
| where timestamp > ago($Hours hours)
| summarize count() by bin(timestamp, 1h), severityLevel, message
| order by timestamp desc
"@
Write-Host $query3 -ForegroundColor Gray
Write-Host ""

Write-Host "=== Query 4: Health Check Status ===" -ForegroundColor Cyan
$query4 = @"
requests
| where url contains "/api/health"
| where timestamp > ago($Hours hours)
| summarize 
    TotalRequests = count(),
    SuccessCount = countif(success == true),
    FailureCount = countif(success == false),
    AvgDuration = avg(duration)
    by bin(timestamp, 1h)
| order by timestamp desc
"@
Write-Host $query4 -ForegroundColor Gray
Write-Host ""

Write-Host "=== Query 5: Message Retry Patterns ===" -ForegroundColor Cyan
$query5 = @"
traces
| where message contains "retry" or message contains "RetryCount"
| where timestamp > ago($Hours hours)
| extend RetryCount = extract(@"retry (\d+)/", 1, message, typeof(int))
| summarize 
    TotalRetries = count(),
    AvgRetryCount = avg(RetryCount),
    MaxRetryCount = max(RetryCount)
    by bin(timestamp, 1h)
| order by timestamp desc
"@
Write-Host $query5 -ForegroundColor Gray
Write-Host ""

Write-Host "Direct Link to Application Insights:" -ForegroundColor Yellow
Write-Host "https://portal.azure.com/#@/resource/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroup/providers/Microsoft.Insights/components/$AppInsightsName" -ForegroundColor Cyan
Write-Host ""

