# Minime MCP diagnostics – pure ASCII for PowerShell 5 compatibility
$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "=== Minime MCP Server Diagnostics ===" -ForegroundColor Cyan
Write-Host ""

# ---------- Helper functions ----------
function Write-Step($text) {
    Write-Host ""
    Write-Host $text -ForegroundColor Yellow
}

function Write-Info($text)  { Write-Host ("  " + $text) -ForegroundColor White }
function Write-Ok($text)    { Write-Host ("  [OK] " + $text) -ForegroundColor Green }
function Write-Warn($text)  { Write-Host ("  [WARN] " + $text) -ForegroundColor Yellow }
function Write-Err($text)   { Write-Host ("  [ERR] " + $text) -ForegroundColor Red }

# ---------- Step 1: Docker ----------
Write-Step "[1] Checking Docker Desktop"
$dockerRunning = $false
try {
    docker info 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Docker Desktop is running"
        $dockerRunning = $true
    } else {
        Write-Err "Docker command returned exit code $LASTEXITCODE"
    }
} catch {
    Write-Err "Docker command failed: $_"
}

if (-not $dockerRunning) {
    Write-Warn "Please start Docker Desktop, wait until it is ready, then rerun this script."
    exit 1
}

# ---------- Step 2: Container existence ----------
Write-Step "[2] Checking minime container"
$containerExists = $false
$containerRunning = $false

try {
    $allContainers = docker ps -a --filter "name=minimemcp" --format "{{.Names}}|{{.Status}}" 2>&1
    if ($allContainers -match "minimemcp") {
        $containerExists = $true
        Write-Ok "Container 'minimemcp' exists"

        $running = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
        if ($running -match "minimemcp") {
            $containerRunning = $true
            Write-Ok "Container is running"
        } else {
            Write-Warn "Container exists but is stopped"
            docker ps -a --filter "name=minimemcp" --format "    Status: {{.Status}}" 2>&1 | Write-Host
        }
    } else {
        Write-Err "Container 'minimemcp' does not exist"
    }
} catch {
    Write-Err "Failed to enumerate containers: $_"
}

if (-not $containerExists) {
    Write-Warn "Create the container first (run setup script or docker run ...)."
    exit 1
}

# ---------- Step 3: Start container if required ----------
if ($containerExists -and -not $containerRunning) {
    Write-Step "[3] Starting container"
    try {
        docker start minimemcp 2>&1 | Out-Null
        Start-Sleep -Seconds 5
        $running = docker ps --filter "name=minimemcp" --format "{{.Names}}" 2>&1
        if ($running -match "minimemcp") {
            Write-Ok "Container started successfully"
            $containerRunning = $true
        } else {
            Write-Err "Container failed to start; dumping recent logs"
            docker logs minimemcp --tail 20 2>&1 | Write-Host
        }
    } catch {
        Write-Err "Failed to start container: $_"
    }
}

# ---------- Step 4: Health check on port 8000 ----------
Write-Step "[4] Checking MCP server (port 8000)"
$port8000Responding = $false

if ($containerRunning) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Ok "MCP server responded with status $($response.StatusCode)"
        $port8000Responding = $true
    } catch {
        Write-Warn "Health check failed: $($_.Exception.Message)"
        $portInfo = netstat -ano | findstr ":8000"
        if ($portInfo) {
            Write-Info "Port 8000 is listening but not responding yet."
        } else {
            Write-Warn "Port 8000 is not listening; showing container logs."
            docker logs minimemcp --tail 30 2>&1 | Write-Host
        }
    }
} else {
    Write-Warn "Cannot test port 8000 because the container is not running."
}

# ---------- Step 5: Proxy health ----------
Write-Step "[5] Checking MCP proxy (port 8001)"
$proxyRunning = $false
try {
    $proxyResponse = Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
    Write-Ok "Proxy responded with status $($proxyResponse.StatusCode)"
    $proxyRunning = $true
} catch {
    Write-Warn "Proxy health check failed: $($_.Exception.Message)"
}

# ---------- Step 6: Recent container logs ----------
if ($containerRunning) {
    Write-Step "[6] Recent container log tail"
    docker logs minimemcp --tail 15 2>&1 | Write-Host
}

# ---------- Summary ----------
Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan

if ($dockerRunning -and $containerRunning -and $port8000Responding) {
    Write-Ok "All checks passed. Restart Cursor and it should load tools."
} elseif ($dockerRunning -and $containerRunning) {
    Write-Warn "Container is running but health check failed. Wait a few seconds or inspect the logs above."
} elseif ($dockerRunning) {
    Write-Warn "Docker is running but the container is stopped."
} else {
    Write-Err "Docker Desktop is not available."
}

if (-not $proxyRunning) {
    Write-Warn "Proxy on port 8001 is not responding. Start it via install\\start-mcp-proxy.ps1 if Cursor relies on it."
}

Write-Host ""

