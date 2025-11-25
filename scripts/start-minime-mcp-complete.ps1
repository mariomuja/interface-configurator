# Complete minime MCP Server Startup Script
# This script ensures Docker Desktop is running and starts minime MCP Server

$ErrorActionPreference = "Continue"

Write-Host "`n=== Starting minime MCP Server ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if Docker Desktop process is running
Write-Host "[1] Checking Docker Desktop process..." -ForegroundColor Yellow
$dockerProcess = Get-Process -Name "Docker Desktop" -ErrorAction SilentlyContinue
if ($dockerProcess) {
    Write-Host "  ✅ Docker Desktop process is running (PID: $($dockerProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  Docker Desktop process not found" -ForegroundColor Yellow
    Write-Host "  Attempting to start Docker Desktop..." -ForegroundColor White
    
    # Try to start Docker Desktop
    $dockerDesktopPath = "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe"
    if (Test-Path $dockerDesktopPath) {
        Start-Process $dockerDesktopPath
        Write-Host "  ✅ Docker Desktop start command executed" -ForegroundColor Green
        Write-Host "  ⏳ Waiting for Docker Desktop to start (this may take 30-60 seconds)..." -ForegroundColor Yellow
        
        $maxWait = 60
        $waited = 0
        $dockerReady = $false
        
        while ($waited -lt $maxWait -and -not $dockerReady) {
            Start-Sleep -Seconds 2
            $waited += 2
            
            try {
                docker info 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    $dockerReady = $true
                }
            } catch {
                # Continue waiting
            }
            
            if ($waited % 10 -eq 0) {
                Write-Host "  Still waiting... ($waited seconds)" -ForegroundColor Gray
            }
        }
        
        if ($dockerReady) {
            Write-Host "  ✅ Docker Desktop is ready!" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  Docker Desktop is starting but not ready yet" -ForegroundColor Yellow
            Write-Host "  Please wait for Docker Desktop to fully start, then run this script again" -ForegroundColor White
            exit 1
        }
    } else {
        Write-Host "  ❌ Docker Desktop not found at: $dockerDesktopPath" -ForegroundColor Red
        Write-Host "  Please install Docker Desktop or start it manually" -ForegroundColor White
        exit 1
    }
}

# Step 2: Verify Docker is responding
Write-Host "`n[2] Verifying Docker is responding..." -ForegroundColor Yellow
try {
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Docker is responding" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Docker is not responding" -ForegroundColor Red
        Write-Host "  Error: $dockerInfo" -ForegroundColor Gray
        Write-Host "  Please wait for Docker Desktop to fully start" -ForegroundColor White
        exit 1
    }
} catch {
    Write-Host "  ❌ Error checking Docker: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Check/Start minime container
Write-Host "`n[3] Checking minime Docker container..." -ForegroundColor Yellow
$containerExists = $false
$containerRunning = $false

try {
    $allContainers = docker ps -a --filter "name=minimemcp" --format "{{.Names}}|{{.Status}}" 2>&1
    if ($allContainers -match "minimemcp") {
        $containerExists = $true
        Write-Host "  ✅ Container 'minimemcp' exists" -ForegroundColor Green
        
        $runningContainers = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
        if ($runningContainers -match "minimemcp") {
            $containerRunning = $true
            Write-Host "  ✅ Container is running" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  Container exists but is not running" -ForegroundColor Yellow
            Write-Host "  Starting container..." -ForegroundColor White
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
        }
    } else {
        Write-Host "  ❌ Container 'minimemcp' does NOT exist" -ForegroundColor Red
        Write-Host ""
        Write-Host "  The container needs to be created first." -ForegroundColor Yellow
        Write-Host "  Run: .\scripts\setup-minime-mcp-server.ps1 -Setup" -ForegroundColor White
        exit 1
    }
} catch {
    Write-Host "  ❌ Error checking containers: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Check if port 8000 is responding
Write-Host "`n[4] Checking if MCP Server responds on port 8000..." -ForegroundColor Yellow
if ($containerRunning) {
    Start-Sleep -Seconds 3  # Give container time to start
    
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop 2>&1
        Write-Host "  ✅ MCP Server is responding on port 8000" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  MCP Server not responding yet (may need more time to start)" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        Write-Host "  Checking container logs..." -ForegroundColor Yellow
        docker logs minimemcp --tail 20 2>&1
    }
}

# Step 5: Check MCP Proxy
Write-Host "`n[5] Checking MCP Proxy (port 8001)..." -ForegroundColor Yellow
$proxyRunning = $false

try {
    $proxyResponse = Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop 2>&1
    Write-Host "  ✅ MCP Proxy is running on port 8001" -ForegroundColor Green
    $proxyRunning = $true
} catch {
    Write-Host "  ⚠️  MCP Proxy is not running on port 8001" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    
    # Check if proxy script exists
    $minimeDir = "$env:USERPROFILE\minime-mcp"
    $proxyScript = "$minimeDir\install\start-mcp-proxy.ps1"
    
    if (Test-Path $proxyScript) {
        Write-Host "  Starting MCP Proxy..." -ForegroundColor White
        Start-Process powershell -ArgumentList "-NoExit", "-File", "`"$proxyScript`"" -WindowStyle Minimized
        Start-Sleep -Seconds 3
        
        try {
            $proxyCheck = Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop 2>&1
            Write-Host "  ✅ MCP Proxy started successfully" -ForegroundColor Green
            $proxyRunning = $true
        } catch {
            Write-Host "  ⚠️  MCP Proxy may need more time to start" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ℹ️  Proxy script not found at: $proxyScript" -ForegroundColor Cyan
        Write-Host "  The proxy may not be required if connecting directly to port 8000" -ForegroundColor White
    }
}

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host ""

if ($containerRunning) {
    Write-Host "✅ minime MCP Server container is running" -ForegroundColor Green
} else {
    Write-Host "❌ minime MCP Server container is not running" -ForegroundColor Red
}

if ($proxyRunning) {
    Write-Host "✅ MCP Proxy is running" -ForegroundColor Green
} else {
    Write-Host "⚠️  MCP Proxy is not running (may not be required)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Wait 10-30 seconds for the server to fully start" -ForegroundColor White
Write-Host "2. Restart Cursor IDE completely (close all windows, wait 5 seconds, reopen)" -ForegroundColor White
Write-Host "3. Check Cursor Settings > MCP Servers - minime should show as green" -ForegroundColor White
Write-Host ""

