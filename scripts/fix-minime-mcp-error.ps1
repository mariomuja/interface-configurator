# Quick Fix Script for minime MCP Server ECONNREFUSED Error
# This script diagnoses and fixes the connection issue

$ErrorActionPreference = "Continue"

Write-Host "`n=== Fixing minime MCP Server Connection Error ===" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
Write-Host "[1] Checking Docker Desktop..." -ForegroundColor Yellow
try {
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Docker Desktop is running" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Docker Desktop is NOT running" -ForegroundColor Red
        Write-Host ""
        Write-Host "  SOLUTION: Start Docker Desktop first!" -ForegroundColor Yellow
        Write-Host "  1. Open Docker Desktop application" -ForegroundColor White
        Write-Host "  2. Wait until Docker is fully started (whale icon in system tray)" -ForegroundColor White
        Write-Host "  3. Run this script again" -ForegroundColor White
        Write-Host ""
        exit 1
    }
} catch {
    Write-Host "  ❌ Docker Desktop is NOT running" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  SOLUTION: Start Docker Desktop first!" -ForegroundColor Yellow
    exit 1
}

# Check if container exists
Write-Host "`n[2] Checking minime Docker container..." -ForegroundColor Yellow
$containerExists = docker ps -a --filter "name=minimemcp" --format "{{.Names}}" 2>&1
if ($containerExists -match "minimemcp") {
    Write-Host "  ✅ Container exists" -ForegroundColor Green
    
    # Check if running
    $containerRunning = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
    if ($containerRunning -match "minimemcp") {
        Write-Host "  ✅ Container is running" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Container exists but is not running" -ForegroundColor Yellow
        Write-Host "  Starting container..." -ForegroundColor White
        docker start minimemcp 2>&1 | Out-Null
        Start-Sleep -Seconds 3
        
        $containerRunning = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
        if ($containerRunning -match "minimemcp") {
            Write-Host "  ✅ Container started successfully" -ForegroundColor Green
        } else {
            Write-Host "  ❌ Failed to start container" -ForegroundColor Red
            Write-Host "  Check logs: docker logs minimemcp --tail 20" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  ❌ Container does not exist" -ForegroundColor Red
    Write-Host ""
    Write-Host "  SOLUTION: Create the container first" -ForegroundColor Yellow
    Write-Host "  The container needs to be created with:" -ForegroundColor White
    Write-Host "  docker run -d --name minimemcp -p 8000:8000 <minime-image>" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Or check the minime MCP installation documentation" -ForegroundColor White
    exit 1
}

# Check if port 8000 is accessible
Write-Host "`n[3] Checking if MCP Server responds on port 8000..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop 2>&1
    Write-Host "  ✅ MCP Server is responding on port 8000" -ForegroundColor Green
    Write-Host "  Response: $($response.Content)" -ForegroundColor Gray
} catch {
    Write-Host "  ⚠️  MCP Server not responding on port 8000" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Checking container logs..." -ForegroundColor Yellow
    docker logs minimemcp --tail 10 2>&1
    Write-Host ""
    Write-Host "  NOTE: The server might need a few seconds to start" -ForegroundColor Yellow
}

# Check proxy
Write-Host "`n[4] Checking MCP Proxy (port 8001)..." -ForegroundColor Yellow
try {
    $proxyResponse = Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
    Write-Host "  ✅ MCP Proxy is running" -ForegroundColor Green
    Write-Host "  Response: $($proxyResponse.Content)" -ForegroundColor Gray
} catch {
    Write-Host "  ❌ MCP Proxy is not running" -ForegroundColor Red
    Write-Host ""
    Write-Host "  SOLUTION: Start the MCP Proxy" -ForegroundColor Yellow
    Write-Host "  cd C:\Users\mario\minime-mcp\install" -ForegroundColor White
    Write-Host "  .\start-mcp-proxy.ps1" -ForegroundColor White
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "If Docker is running and container is started, the error should be resolved." -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Wait 5-10 seconds for the container to fully start" -ForegroundColor White
Write-Host "2. Restart Cursor IDE completely (close all windows, wait 5 seconds, reopen)" -ForegroundColor White
Write-Host "3. Check Cursor Settings > MCP Servers - minime should show as green" -ForegroundColor White
Write-Host ""

