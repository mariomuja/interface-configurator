# Script to update Vercel environment variables and deploy
# Usage: .\update-vercel-env.ps1

Write-Host "Updating Vercel Environment Variables..." -ForegroundColor Cyan
Write-Host ""

# Get Azure resources
Write-Host "1. Getting Azure SQL Server details..." -ForegroundColor Yellow
$sqlServer = az sql server show --name sql-main-database --resource-group rg-interface-configuration --query "fullyQualifiedDomainName" -o tsv
Write-Host "   SQL Server: $sqlServer" -ForegroundColor Gray

Write-Host "2. Getting Storage Account connection string..." -ForegroundColor Yellow
$storageConn = az storage account show-connection-string --name stappgeneral --resource-group rg-interface-configuration --query "connectionString" -o tsv
Write-Host "   Storage Connection String retrieved" -ForegroundColor Gray

# Database details
$sqlDatabase = "app-database"
$sqlUser = "sqladmin"
$sqlPassword = "InfrastructureAsCode2024!Secure"

Write-Host ""
Write-Host "Environment Variables to set:" -ForegroundColor Cyan
Write-Host "  AZURE_SQL_SERVER = $sqlServer" -ForegroundColor Gray
Write-Host "  AZURE_SQL_DATABASE = $sqlDatabase" -ForegroundColor Gray
Write-Host "  AZURE_SQL_USER = $sqlUser" -ForegroundColor Gray
Write-Host "  AZURE_SQL_PASSWORD = [HIDDEN]" -ForegroundColor Gray
Write-Host "  AZURE_STORAGE_CONNECTION_STRING = [HIDDEN]" -ForegroundColor Gray
Write-Host ""

# Check if Vercel CLI is available
$vercelCmd = Get-Command vercel -ErrorAction SilentlyContinue
if (-not $vercelCmd) {
    Write-Host "❌ Vercel CLI not found. Please install it:" -ForegroundColor Red
    Write-Host "   npm install -g vercel" -ForegroundColor Yellow
    exit 1
}

Write-Host "Setting environment variables in Vercel..." -ForegroundColor Yellow
Write-Host "Note: You'll be prompted to enter each value interactively." -ForegroundColor Cyan
Write-Host ""

# Set environment variables (production environment)
Write-Host "Setting AZURE_SQL_SERVER..." -ForegroundColor Yellow
echo $sqlServer | vercel env add AZURE_SQL_SERVER production

Write-Host "Setting AZURE_SQL_DATABASE..." -ForegroundColor Yellow
echo $sqlDatabase | vercel env add AZURE_SQL_DATABASE production

Write-Host "Setting AZURE_SQL_USER..." -ForegroundColor Yellow
echo $sqlUser | vercel env add AZURE_SQL_USER production

Write-Host "Setting AZURE_SQL_PASSWORD..." -ForegroundColor Yellow
echo $sqlPassword | vercel env add AZURE_SQL_PASSWORD production

Write-Host "Setting AZURE_STORAGE_CONNECTION_STRING..." -ForegroundColor Yellow
echo $storageConn | vercel env add AZURE_STORAGE_CONNECTION_STRING production

Write-Host ""
Write-Host "✅ Environment variables updated!" -ForegroundColor Green
Write-Host ""
Write-Host "Deploying to Vercel..." -ForegroundColor Cyan
vercel deploy --prod --yes

Write-Host ""
Write-Host "✅ Deployment completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Check Vercel dashboard for deployment status" -ForegroundColor Gray
Write-Host "  2. Test the application endpoints" -ForegroundColor Gray
Write-Host "  3. Verify database connection in Vercel logs" -ForegroundColor Gray




