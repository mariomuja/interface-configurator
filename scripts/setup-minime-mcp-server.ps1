# Setup Script for minime MCP Server
# Analyzes MCP configuration issues and sets up automatic startup
# Handles both Docker container and MCP Proxy

param(
    [switch]$Diagnose,
    [switch]$Setup,
    [switch]$Start,
    [switch]$InstallService,
    [switch]$FixConfig
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== minime MCP Server Setup ===" -ForegroundColor Cyan

# Paths
$minimeDir = "$env:USERPROFILE\minime-mcp"
$installDir = "$minimeDir\install"
$proxyScript = "$installDir\start-mcp-proxy.ps1"
$proxyJs = "$installDir\mcp-proxy-minimal.js"
$cursorMcpConfig = "$env:USERPROFILE\.cursor\mcp.json"

function Test-MinimeDocker {
    Write-Host "`n[1] Checking minime Docker container..." -ForegroundColor Yellow
    
    try {
        $container = docker ps --filter "name=minimemcp" --format "{{.Names}} {{.Status}}" 2>$null
        if ($container -match "minimemcp") {
            Write-Host "  ✅ Docker container is running" -ForegroundColor Green
            Write-Host "  Status: $container" -ForegroundColor White
            return $true
        } else {
            Write-Host "  ❌ Docker container is not running" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "  ⚠️  Docker not available or error: $_" -ForegroundColor Yellow
        return $false
    }
}

function Test-McpProxy {
    Write-Host "`n[2] Checking MCP Proxy (port 8001)..." -ForegroundColor Yellow
    
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        Write-Host "  ✅ MCP Proxy is running" -ForegroundColor Green
        Write-Host "  Response: $($response.Content)" -ForegroundColor Gray
        return $true
    } catch {
        Write-Host "  ❌ MCP Proxy is not running" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        
        # Check if process is running but not responding
        $nodeProcesses = Get-Process -Name node -ErrorAction SilentlyContinue
        $proxyProcess = $nodeProcesses | Where-Object {
            try {
                $cmdLine = (Get-WmiObject Win32_Process -Filter "ProcessId = $($_.Id)").CommandLine
                $cmdLine -like "*mcp-proxy*"
            } catch {
                $false
            }
        }
        
        if ($proxyProcess) {
            Write-Host "  ⚠️  Node process found but proxy not responding (PID: $($proxyProcess.Id))" -ForegroundColor Yellow
        }
        
        return $false
    }
}

function Test-McpConfig {
    Write-Host "`n[3] Checking MCP configuration..." -ForegroundColor Yellow
    
    if (-not (Test-Path $cursorMcpConfig)) {
        Write-Host "  ❌ MCP configuration file not found: $cursorMcpConfig" -ForegroundColor Red
        return $null
    }
    
    try {
        $config = Get-Content $cursorMcpConfig -Raw | ConvertFrom-Json
        Write-Host "  ✅ MCP configuration file found" -ForegroundColor Green
        
        if ($config.mcpServers -and $config.mcpServers.minime) {
            $minimeConfig = $config.mcpServers.minime
            Write-Host "  ✅ minime server configured" -ForegroundColor Green
            Write-Host "  URL: $($minimeConfig.url)" -ForegroundColor White
            Write-Host "  Transport: $($minimeConfig.transport)" -ForegroundColor White
            
            # Validate configuration
            $issues = @()
            if ($minimeConfig.url -ne "http://localhost:8001/mcp") {
                $issues += "URL should be 'http://localhost:8001/mcp'"
            }
            if ($minimeConfig.transport -ne "http") {
                $issues += "Transport should be 'http' (not SSE)"
            }
            
            if ($issues.Count -gt 0) {
                Write-Host "  ⚠️  Configuration issues found:" -ForegroundColor Yellow
                foreach ($issue in $issues) {
                    Write-Host "    - $issue" -ForegroundColor Yellow
                }
                return @{ Valid = $false; Config = $config; Issues = $issues }
            }
            
            return @{ Valid = $true; Config = $config }
        } else {
            Write-Host "  ❌ minime server not configured in mcp.json" -ForegroundColor Red
            return @{ Valid = $false; Config = $config }
        }
    } catch {
        Write-Host "  ❌ Error reading MCP configuration: $_" -ForegroundColor Red
        return $null
    }
}

function Start-MinimeDocker {
    Write-Host "`n[4] Starting minime Docker container..." -ForegroundColor Yellow
    
    try {
        # Check if container exists but is stopped
        $stopped = docker ps -a --filter "name=minimemcp" --format "{{.Names}} {{.Status}}" 2>$null
        if ($stopped -match "minimemcp" -and $stopped -notmatch "Up") {
            Write-Host "  Starting stopped container..." -ForegroundColor White
            docker start minimemcp 2>&1 | Out-Null
            Start-Sleep -Seconds 3
        } else {
            Write-Host "  Container is already running or doesn't exist" -ForegroundColor Gray
        }
        
        $running = Test-MinimeDocker
        if ($running) {
            Write-Host "  ✅ Docker container started successfully" -ForegroundColor Green
            return $true
        } else {
            Write-Host "  ❌ Failed to start Docker container" -ForegroundColor Red
            Write-Host "  Note: Docker container must be created first. Run: docker run ..." -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "  ❌ Error starting Docker: $_" -ForegroundColor Red
        return $false
    }
}

function Start-McpProxy {
    Write-Host "`n[5] Starting MCP Proxy..." -ForegroundColor Yellow
    
    if (-not (Test-Path $proxyScript)) {
        Write-Host "  ❌ Proxy script not found: $proxyScript" -ForegroundColor Red
        return $false
    }
    
    if (-not (Test-Path $proxyJs)) {
        Write-Host "  ❌ Proxy JavaScript file not found: $proxyJs" -ForegroundColor Red
        return $false
    }
    
    # Stop existing proxy processes
    $nodeProcesses = Get-Process -Name node -ErrorAction SilentlyContinue
    foreach ($proc in $nodeProcesses) {
        try {
            $cmdLine = (Get-WmiObject Win32_Process -Filter "ProcessId = $($proc.Id)").CommandLine
            if ($cmdLine -like "*mcp-proxy*") {
                Write-Host "  Stopping existing proxy process (PID: $($proc.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 1
            }
        } catch {
            # Ignore errors
        }
    }
    
    try {
        # Start proxy using the existing script
        & $proxyScript
        
        Start-Sleep -Seconds 3
        
        $running = Test-McpProxy
        if ($running) {
            Write-Host "  ✅ MCP Proxy started successfully" -ForegroundColor Green
            return $true
        } else {
            Write-Host "  ⚠️  Proxy script executed but not responding yet" -ForegroundColor Yellow
            return $true # Assume it will start
        }
    } catch {
        Write-Host "  ❌ Error starting proxy: $_" -ForegroundColor Red
        return $false
    }
}

function Fix-McpConfig {
    Write-Host "`n[6] Fixing MCP configuration..." -ForegroundColor Yellow
    
    # Ensure .cursor directory exists
    $cursorDir = Split-Path $cursorMcpConfig -Parent
    if (-not (Test-Path $cursorDir)) {
        New-Item -ItemType Directory -Path $cursorDir -Force | Out-Null
        Write-Host "  Created directory: $cursorDir" -ForegroundColor Green
    }
    
    # Read or create config
    $config = @{}
    if (Test-Path $cursorMcpConfig) {
        try {
            $config = Get-Content $cursorMcpConfig -Raw | ConvertFrom-Json | ConvertTo-Hashtable
        } catch {
            Write-Host "  Warning: Could not parse existing config, creating new one" -ForegroundColor Yellow
            $config = @{}
        }
    }
    
    # Ensure mcpServers exists
    if (-not $config.mcpServers) {
        $config.mcpServers = @{}
    }
    
    # Set correct minime configuration
    $config.mcpServers.minime = @{
        url = "http://localhost:8001/mcp"
        transport = "http"
    }
    
    # Convert back to JSON and write
    try {
        $json = $config | ConvertTo-Json -Depth 10
        $json | Set-Content $cursorMcpConfig -Encoding UTF8
        Write-Host "  ✅ MCP configuration updated" -ForegroundColor Green
        Write-Host "  File: $cursorMcpConfig" -ForegroundColor White
        return $true
    } catch {
        Write-Host "  ❌ Error writing config: $_" -ForegroundColor Red
        return $false
    }
}

function Install-StartupTask {
    Write-Host "`n[7] Installing Windows Startup Task..." -ForegroundColor Yellow
    
    $taskName = "minime-mcp-server-startup"
    $taskDescription = "Starts minime MCP Server (Docker + Proxy) on system startup for Cursor IDE"
    
    # Create startup script
    $startupScript = "$installDir\start-minime-on-boot.ps1"
    $startupScriptContent = @"
# Auto-generated startup script for minime MCP Server
# Ensures Docker Desktop, the minime container, and the MCP Proxy are ready after logon

`$ErrorActionPreference = "Stop"

`$installDir = "$installDir"
`$proxyScript = "$installDir\start-mcp-proxy.ps1"
`$logDir = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "minime-mcp"
`$logFile = Join-Path `$logDir "startup.log"

if (-not (Test-Path `$logDir)) {
    New-Item -ItemType Directory -Path `$logDir -Force | Out-Null
}

function Write-Log {
    param(
        [string]`$Message
    )
    `$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    `$entry = "[`$timestamp] `$Message"
    `$entry | Out-File -FilePath `$logFile -Encoding UTF8 -Append
}

function Ensure-DockerDesktop {
    `$service = Get-Service -Name "com.docker.service" -ErrorAction SilentlyContinue
    if (`$service) {
        if (`$service.Status -ne "Running") {
            Write-Log "Starting Docker Windows service..."
            try {
                Start-Service -Name `$service.Name -ErrorAction Stop
                `$service.WaitForStatus("Running","00:02:00")
                Write-Log "Docker service is running."
            } catch {
                Write-Log "Failed to start Docker service: $($_.Exception.Message)"
            }
        } else {
            Write-Log "Docker service already running."
        }
    } else {
        `$dockerDesktop = Join-Path `$env:ProgramFiles "Docker\Docker\Docker Desktop.exe"
        if (Test-Path `$dockerDesktop) {
            Write-Log "Launching Docker Desktop..."
            Start-Process -FilePath `$dockerDesktop | Out-Null
        } else {
            Write-Log "Docker Desktop executable not found."
        }
    }
}

function Wait-ForDocker {
    param([int]`$TimeoutSeconds = 120)
    `$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while (`$stopwatch.Elapsed.TotalSeconds -lt `$TimeoutSeconds) {
        try {
            docker version --format '{{.Server.Version}}' 2>$null | Out-Null
            Write-Log "Docker engine is available."
            return $true
        } catch {
            Start-Sleep -Seconds 3
        }
    }

    Write-Log "Docker engine did not become ready within `$TimeoutSeconds seconds."
    return $false
}

function Ensure-MinimeContainer {
    Write-Log "Ensuring minimemcp container is running..."
    try {
        `$status = docker inspect -f '{{.State.Status}}' minimemcp 2>$null
    } catch {
        `$status = $null
    }

    if (`$status -eq "running") {
        Write-Log "minimemcp container already running."
        return $true
    }

    Write-Log "Starting minimemcp container..."
    try {
        docker start minimemcp 2>&1 | Out-Null
    } catch {
        Write-Log "Failed to start minimemcp container: $($_.Exception.Message)"
        return $false
    }

    Start-Sleep -Seconds 3

    try {
        `$status = docker inspect -f '{{.State.Status}}' minimemcp 2>$null
    } catch {
        `$status = $null
    }

    if (`$status -eq "running") {
        Write-Log "minimemcp container is now running."
        return $true
    }

    Write-Log "Container status after start attempt: `$status"
    return $false
}

function Test-ProxyHealth {
    try {
        Invoke-WebRequest -Uri "http://localhost:8001/health" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop | Out-Null
        return $true
    } catch {
        return $false
    }
}

function Ensure-McpProxy {
    if (Test-ProxyHealth) {
        Write-Log "MCP Proxy already healthy."
        return $true
    }

    if (-not (Test-Path `$proxyScript)) {
        Write-Log "Proxy script not found at `$proxyScript"
        return $false
    }

    Write-Log "Starting MCP Proxy..."
    try {
        & `$proxyScript 2>&1 | Out-Null
    } catch {
        Write-Log "Failed to start MCP Proxy: $($_.Exception.Message)"
        return $false
    }

    Start-Sleep -Seconds 3

    if (Test-ProxyHealth) {
        Write-Log "MCP Proxy is healthy."
        return $true
    }

    Write-Log "MCP Proxy failed health check after start."
    return $false
}

Write-Log "=== Starting minime MCP bootstrap ==="

Ensure-DockerDesktop
`$dockerReady = Wait-ForDocker
`$containerReady = $false

if (`$dockerReady) {
    `$containerReady = Ensure-MinimeContainer
} else {
    Write-Log "Skipping container start because Docker is unavailable."
}

`$proxyReady = Ensure-McpProxy

Write-Log ("Bootstrap summary -> DockerReady=`$dockerReady ContainerReady=`$containerReady ProxyReady=`$proxyReady")

if (-not (`$dockerReady -and `$containerReady -and `$proxyReady)) {
    Write-Log "minime MCP startup completed with warnings."
} else {
    Write-Log "minime MCP startup completed successfully."
}
"@
    
    try {
        $startupScriptContent | Set-Content $startupScript -Encoding UTF8
        Write-Host "  Created startup script: $startupScript" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Error creating startup script: $_" -ForegroundColor Red
        return $false
    }
    
    # Remove existing task if it exists
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "  Removing existing task..." -ForegroundColor Yellow
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    }
    
    # Try to create scheduled task first, fallback to startup folder shortcut
    $taskCreated = $false
    
    # Method 1: Try Scheduled Task (requires admin rights for some configurations)
    try {
        $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$startupScript`""
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
        
        Register-ScheduledTask -TaskName $taskName -Description $taskDescription -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force -ErrorAction Stop | Out-Null
        
        Write-Host "  ✅ Startup task installed successfully" -ForegroundColor Green
        Write-Host "  Task Name: $taskName" -ForegroundColor White
        Write-Host "  Will start: On user logon" -ForegroundColor White
        Write-Host "  Script: $startupScript" -ForegroundColor White
        
        $taskCreated = $true
    } catch {
        Write-Host "  ⚠️  Could not create scheduled task (may need admin rights): $_" -ForegroundColor Yellow
        Write-Host "  Using alternative: Startup folder shortcut..." -ForegroundColor Yellow
        $taskCreated = $false
    }
    
    # Method 2: Create shortcut in Startup folder (works without admin rights)
    if (-not $taskCreated) {
        try {
            $startupFolder = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
            if (-not (Test-Path $startupFolder)) {
                New-Item -ItemType Directory -Path $startupFolder -Force | Out-Null
            }
            
            $shortcutPath = "$startupFolder\minime-mcp-server.lnk"
            
            # Remove existing shortcut if it exists
            if (Test-Path $shortcutPath) {
                Remove-Item $shortcutPath -Force -ErrorAction SilentlyContinue
            }
            
            $WScriptShell = New-Object -ComObject WScript.Shell
            $shortcut = $WScriptShell.CreateShortcut($shortcutPath)
            $shortcut.TargetPath = "powershell.exe"
            $shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$startupScript`""
            $shortcut.WorkingDirectory = $installDir
            $shortcut.Description = $taskDescription
            $shortcut.Save()
            
            Write-Host "  ✅ Startup shortcut created successfully" -ForegroundColor Green
            Write-Host "  Shortcut: $shortcutPath" -ForegroundColor White
            Write-Host "  Will start: On user logon" -ForegroundColor White
            Write-Host "  Script: $startupScript" -ForegroundColor White
            
            $taskCreated = $true
        } catch {
            Write-Host "  ❌ Error creating startup shortcut: $_" -ForegroundColor Red
            Write-Host "  Manual setup required:" -ForegroundColor Yellow
            Write-Host "    1. Open: $startupFolder" -ForegroundColor White
            Write-Host "    2. Create shortcut to: $startupScript" -ForegroundColor White
            Write-Host "    3. Set target: powershell.exe" -ForegroundColor White
            Write-Host "    4. Set arguments: -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$startupScript`"" -ForegroundColor White
        }
    }
    
    return $taskCreated
}

function Diagnose-MinimeMCP {
    Write-Host "`n=== Diagnosing minime MCP Server Issues ===" -ForegroundColor Cyan
    
    $dockerRunning = Test-MinimeDocker
    $proxyRunning = Test-McpProxy
    $config = Test-McpConfig
    
    Write-Host "`n=== Diagnosis Summary ===" -ForegroundColor Cyan
    Write-Host "Docker Container: $(if ($dockerRunning) { "✅ RUNNING" } else { "❌ NOT RUNNING" })" -ForegroundColor $(if ($dockerRunning) { "Green" } else { "Red" })
    Write-Host "MCP Proxy (8001): $(if ($proxyRunning) { "✅ RUNNING" } else { "❌ NOT RUNNING" })" -ForegroundColor $(if ($proxyRunning) { "Green" } else { "Red" })
    Write-Host "MCP Config: $(if ($config -and $config.Valid) { "✅ VALID" } elseif ($config) { "⚠️  INVALID" } else { "❌ NOT FOUND" })" -ForegroundColor $(if ($config -and $config.Valid) { "Green" } elseif ($config) { "Yellow" } else { "Red" })
    
    # Common issues
    Write-Host "`n=== Issues Found ===" -ForegroundColor Yellow
    
    if (-not $dockerRunning) {
        Write-Host "❌ Docker container is not running" -ForegroundColor Red
        Write-Host "   Solution: Start Docker container or create it if it doesn't exist" -ForegroundColor White
    }
    
    if (-not $proxyRunning) {
        Write-Host "❌ MCP Proxy is not running on port 8001" -ForegroundColor Red
        Write-Host "   Solution: Start MCP Proxy using start-mcp-proxy.ps1" -ForegroundColor White
    }
    
    if (-not $config -or -not $config.Valid) {
        Write-Host "❌ MCP configuration is missing or invalid" -ForegroundColor Red
        if ($config -and $config.Issues) {
            foreach ($issue in $config.Issues) {
                Write-Host "   - $issue" -ForegroundColor Yellow
            }
        }
        Write-Host "   Solution: Fix MCP configuration in $cursorMcpConfig" -ForegroundColor White
    }
    
    if ($dockerRunning -and $proxyRunning -and $config -and $config.Valid) {
        Write-Host "✅ All components are running correctly!" -ForegroundColor Green
        Write-Host "   If Cursor still shows errors, try restarting Cursor completely" -ForegroundColor White
    }
    
    return @{
        DockerRunning = $dockerRunning
        ProxyRunning = $proxyRunning
        Config = $config
    }
}

# Helper function to convert PSCustomObject to Hashtable
function ConvertTo-Hashtable {
    param([Parameter(ValueFromPipeline)]$InputObject)
    
    if ($null -eq $InputObject) { return @{} }
    
    if ($InputObject -is [hashtable]) {
        return $InputObject
    }
    
    if ($InputObject -is [PSCustomObject]) {
        $hash = @{}
        $InputObject.PSObject.Properties | ForEach-Object {
            if ($_.Value -is [PSCustomObject]) {
                $hash[$_.Name] = ConvertTo-Hashtable $_.Value
            } elseif ($_.Value -is [Array]) {
                $hash[$_.Name] = $_.Value | ForEach-Object { ConvertTo-Hashtable $_ }
            } else {
                $hash[$_.Name] = $_.Value
            }
        }
        return $hash
    }
    
    return $InputObject
}

# Main execution
if ($Diagnose) {
    $diagnosis = Diagnose-MinimeMCP
    exit 0
}

if ($FixConfig) {
    Fix-McpConfig
    exit 0
}

if ($Setup) {
    Write-Host "`n=== Setting up minime MCP Server ===" -ForegroundColor Cyan
    
    # Diagnose first
    $diagnosis = Diagnose-MinimeMCP
    
    # Fix configuration
    Write-Host "`n--- Fixing Configuration ---" -ForegroundColor Cyan
    Fix-McpConfig
    
    # Start Docker
    Write-Host "`n--- Starting Docker Container ---" -ForegroundColor Cyan
    Start-MinimeDocker
    
    # Start Proxy
    Write-Host "`n--- Starting MCP Proxy ---" -ForegroundColor Cyan
    Start-McpProxy
    
    # Install startup task
    Write-Host "`n--- Installing Startup Task ---" -ForegroundColor Cyan
    Install-StartupTask
    
    # Final diagnosis
    Write-Host "`n--- Final Status ---" -ForegroundColor Cyan
    Start-Sleep -Seconds 2
    Diagnose-MinimeMCP
    
    Write-Host "`n✅ Setup completed!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "1. Restart Cursor IDE completely (close all windows, wait 5 seconds, reopen)" -ForegroundColor White
    Write-Host "2. Check Cursor Settings > MCP Servers - minime should show as green" -ForegroundColor White
    Write-Host "3. The server will now start automatically on system startup" -ForegroundColor White
    exit 0
}

if ($Start) {
    Write-Host "`n=== Starting minime MCP Server ===" -ForegroundColor Cyan
    Start-MinimeDocker
    Start-McpProxy
    Start-Sleep -Seconds 2
    Diagnose-MinimeMCP
    exit 0
}

if ($InstallService) {
    Install-StartupTask
    exit 0
}

# If no parameters, show help
Write-Host "`nUsage:" -ForegroundColor Cyan
Write-Host "  .\setup-minime-mcp-server.ps1 -Diagnose      # Analyze current configuration" -ForegroundColor White
Write-Host "  .\setup-minime-mcp-server.ps1 -Setup        # Full setup (config + start + startup)" -ForegroundColor White
Write-Host "  .\setup-minime-mcp-server.ps1 -Start        # Start minime server now" -ForegroundColor White
Write-Host "  .\setup-minime-mcp-server.ps1 -FixConfig    # Fix MCP configuration only" -ForegroundColor White
Write-Host "  .\setup-minime-mcp-server.ps1 -InstallService  # Install startup task only" -ForegroundColor White
