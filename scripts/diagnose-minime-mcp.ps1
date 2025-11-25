# Comprehensive minime MCP Server Diagnostics and Fix Script
# This script diagnoses all common issues and provides solutions

$ErrorActionPreference = "Continue"

Write-Host "`n=== minime MCP Server Diagnostics ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check Docker Desktop
Write-Host "[1] Checking Docker Desktop..." -ForegroundColor Yellow
$dockerRunning = $false
try {
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Docker Desktop is running" -ForegroundColor Green
        $dockerRunning = $true
    } else {
        Write-Host "  ❌ Docker Desktop is NOT running" -ForegroundColor Red
        Write-Host "  Error output: $dockerInfo" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ❌ Docker Desktop is NOT running" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Gray
}

if (-not $dockerRunning) {
    Write-Host ""
    Write-Host "  🔧 SOLUTION:" -ForegroundColor Yellow
    Write-Host "  1. Open Docker Desktop application" -ForegroundColor White
    Write-Host "  2. Wait until Docker is fully started (whale icon in system tray should be steady)" -ForegroundColor White
    Write-Host "  3. Verify Docker is running: docker info" -ForegroundColor White
    Write-Host "  4. Run this script again" -ForegroundColor White
    Write-Host ""
    Write-Host "  If Docker Desktop won't start:" -ForegroundColor Yellow
    Write-Host "  - Check Windows WSL 2 is installed and updated" -ForegroundColor White
    Write-Host "  - Restart your computer" -ForegroundColor White
    Write-Host "  - Check Docker Desktop logs" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Step 2: Check if minime container exists
Write-Host "`n[2] Checking minime Docker container..." -ForegroundColor Yellow
$containerExists = $false
$containerRunning = $false

try {
    $allContainers = docker ps -a --filter "name=minimemcp" --format "{{.Names}}|{{.Status}}" 2>&1
    if ($allContainers -match "minimemcp") {
        $containerExists = $true
        Write-Host "  ✅ Container 'minimemcp' exists" -ForegroundColor Green
        
        # Check if running
        $runningContainers = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
        if ($runningContainers -match "minimemcp") {
            $containerRunning = $true
            Write-Host "  ✅ Container is running" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  Container exists but is NOT running" -ForegroundColor Yellow
            Write-Host "  Current status:" -ForegroundColor White
            docker ps -a --filter "name=minimemcp" --format "  {{.Status}}" 2>&1
        }
    } else {
        Write-Host "  ❌ Container 'minimemcp' does NOT exist" -ForegroundColor Red
    }
} catch {
    Write-Host "  ❌ Error checking containers: $_" -ForegroundColor Red
}

# Step 3: Start container if needed
if ($containerExists -and -not $containerRunning) {
    Write-Host "`n[3] Starting minime container..." -ForegroundColor Yellow
    try {
        docker start minimemcp 2>&1 | Out-Null
        Start-Sleep -Seconds 5
        
        $runningCheck = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
        if ($runningCheck -match "minimemcp") {
            Write-Host "  ✅ Container started successfully" -ForegroundColor Green
            $containerRunning = $true
        } else {
            Write-Host "  ❌ Failed to start container" -ForegroundColor Red
            Write-Host "  Checking logs..." -ForegroundColor Yellow
            docker logs minimemcp --tail 20 2>&1
        }
    } catch {
        Write-Host "  ❌ Error starting container: $_" -ForegroundColor Red
    }
}

# Step 4: Create container if it doesn't exist
if (-not $containerExists) {
    Write-Host "`n[3] Container does not exist - needs to be created" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  🔧 SOLUTION:" -ForegroundColor Yellow
    Write-Host "  The minime MCP container needs to be created first." -ForegroundColor White
    Write-Host ""
    Write-Host "  Option 1: Use setup script (if available)" -ForegroundColor Cyan
    Write-Host "  .\scripts\setup-minime-mcp-server.ps1" -ForegroundColor White
    Write-Host ""
    Write-Host "  Option 2: Create manually" -ForegroundColor Cyan
    Write-Host "  docker run -d --name minimemcp -p 8000:8000 [minime-image]" -ForegroundColor White
    Write-Host ""
    Write-Host "  Check minime MCP documentation for the correct image name" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Step 5: Check port 8000
Write-Host "`n[4] Checking if MCP Server responds on port 8000..." -ForegroundColor Yellow
$port8000Responding = $false

if ($containerRunning) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop 2>&1
        Write-Host "  ✅ MCP Server is responding on port 8000" -ForegroundColor Green
        Write-Host "  Status Code: $($response.StatusCode)" -ForegroundColor Gray
        $port8000Responding = $true
    } catch {
        Write-Host "  ⚠️  MCP Server not responding on port 8000" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        
        # Check if port is listening
        $portCheck = netstat -ano | findstr ":8000"
        if ($portCheck) {
            Write-Host "  ℹ️  Port 8000 is listening, but server might not be ready yet" -ForegroundColor Cyan
            Write-Host "  Wait a few seconds and check again" -ForegroundColor White
        } else {
            Write-Host "  ⚠️  Port 8000 is not listening" -ForegroundColor Yellow
            Write-Host "  Checking container logs..." -ForegroundColor Yellow
            docker logs minimemcp --tail 30 2>&1
        }
    }
} else {
    Write-Host "  ⚠️  Cannot check port 8000 - container is not running" -ForegroundColor Yellow
}

# Step 6: Check MCP Proxy (port 8001)
Write-Host "`n[5] Checking MCP Proxy (port 8001)..." -ForegroundColor Yellow
$proxyRunning = $false

try {
    $proxyResponse = Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop 2>&1
    Write-Host "  ✅ MCP Proxy is running on port 8001" -ForegroundColor Green
    $proxyRunning = $true
} catch {
    Write-Host "  ⚠️  MCP Proxy is not running on port 8001" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  ℹ️  The proxy might not be required if connecting directly to port 8000" -ForegroundColor Cyan
}

# Step 7: Check container logs
if ($containerRunning) {
    Write-Host "`n[6] Recent container logs..." -ForegroundColor Yellow
    docker logs minimemcp --tail 15 2>&1
}

# Summary and Recommendations
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host ""

if ($dockerRunning -and $containerRunning -and $port8000Responding) {
    Write-Host "✅ All checks passed! minime MCP Server should be working." -ForegroundColor Green
    Write-Host ""
    Write-Host "If Cursor still shows errors:" -ForegroundColor Yellow
    Write-Host "1. Restart Cursor IDE completely (close all windows, wait 5 seconds, reopen)" -ForegroundColor White
    Write-Host "2. Check Cursor Settings > MCP Servers - minime should show as green" -ForegroundColor White
    Write-Host "3. Check Cursor logs for MCP connection errors" -ForegroundColor White
} elseif ($dockerRunning -and $containerRunning) {
    Write-Host "⚠️  Docker and container are running, but server is not responding" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Possible issues:" -ForegroundColor Yellow
    Write-Host "- Server is still starting (wait 10-30 seconds)" -ForegroundColor White
    Write-Host "- Port mapping issue (check: docker ps --filter name=minimemcp)" -ForegroundColor White
    Write-Host "- Container crashed (check logs above)" -ForegroundColor White
    Write-Host ""
    Write-Host "Try:" -ForegroundColor Cyan
    Write-Host "1. Wait 30 seconds and run this script again" -ForegroundColor White
    Write-Host "2. Check container logs: docker logs minimemcp --tail 50" -ForegroundColor White
    Write-Host "3. Restart container: docker restart minimemcp" -ForegroundColor White
} elseif ($dockerRunning) {
    Write-Host "⚠️  Docker is running but container is not" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Try:" -ForegroundColor Cyan
    Write-Host "1. Start container: docker start minimemcp" -ForegroundColor White
    Write-Host "2. If that fails, check logs: docker logs minimemcp --tail 50" -ForegroundColor White
    Write-Host "3. Recreate container if needed" -ForegroundColor White
} else {
    Write-Host "❌ Docker Desktop is not running" -ForegroundColor Red
    Write-Host ""
    Write-Host "This is required for minime MCP Server to work." -ForegroundColor White
}

Write-Host ""

