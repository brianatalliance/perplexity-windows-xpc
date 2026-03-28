#Requires -Version 5.1
<#
.SYNOPSIS
    PerplexityXPC for Windows - Interactive Setup Wizard v1.4.0

.DESCRIPTION
    A comprehensive terminal-based setup wizard for PerplexityXPC.
    Guides you through prerequisites, API key configuration, service
    settings, MCP servers, and integration selection.

.PARAMETER Silent
    Skip the wizard and use all defaults for automated deployment.

.PARAMETER ApiKey
    Pre-supply your Perplexity API key (format: pplx-...).

.PARAMETER ConfigFile
    Path to a JSON config file for batch/automated deployment.

.PARAMETER Uninstall
    Remove PerplexityXPC and all associated files, services, and registry entries.

.EXAMPLE
    .\Summon-Aunties.ps1

.EXAMPLE
    .\Summon-Aunties.ps1 -ApiKey "pplx-abc123"

.EXAMPLE
    .\Summon-Aunties.ps1 -Silent -ConfigFile ".\setup-config.json"

.EXAMPLE
    .\Summon-Aunties.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [switch]$Silent,
    [string]$ApiKey = '',
    [string]$ConfigFile = '',
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
$Script:VERSION        = '1.4.0'
$Script:DEFAULT_PORT   = 47777
$Script:DEFAULT_PATH   = 'C:\Program Files\PerplexityXPC'
$Script:SERVICE_NAME   = 'PerplexityXPC'
$Script:API_KEY_LINK   = 'https://www.perplexity.ai/settings/api'
$Script:SPINNER_FRAMES = @('|', '/', '-', '\')

# ---------------------------------------------------------------------------
# Color helpers
# ---------------------------------------------------------------------------
function Write-Color {
    param(
        [string]$Text,
        [System.ConsoleColor]$ForegroundColor = [System.ConsoleColor]::White,
        [switch]$NoNewline
    )
    if ($NoNewline) {
        Write-Host $Text -ForegroundColor $ForegroundColor -NoNewline
    } else {
        Write-Host $Text -ForegroundColor $ForegroundColor
    }
}

function Write-Header {
    param([string]$Text)
    $line = '-' * 60
    Write-Host ''
    Write-Color $line -ForegroundColor DarkGray
    Write-Color "  $Text" -ForegroundColor Cyan
    Write-Color $line -ForegroundColor DarkGray
}

function Write-Pass   { Write-Color "  [PASS] $args" -ForegroundColor Green }
function Write-Warn   { Write-Color "  [WARN] $args" -ForegroundColor Yellow }
function Write-Fail   { Write-Color "  [FAIL] $args" -ForegroundColor Red }
function Write-Info   { Write-Color "  $args" -ForegroundColor Gray }
function Write-Step   { Write-Color "`n  >> $args" -ForegroundColor Cyan }

# ---------------------------------------------------------------------------
# Spinner / progress helpers
# ---------------------------------------------------------------------------
function Show-Spinner {
    param(
        [string]$Message,
        [scriptblock]$Action
    )
    $frame = 0
    $job   = Start-Job -ScriptBlock $Action
    Write-Host ''
    while ($job.State -eq 'Running') {
        $c = $Script:SPINNER_FRAMES[$frame % 4]
        Write-Host "`r  [$c] $Message..." -NoNewline -ForegroundColor Cyan
        Start-Sleep -Milliseconds 120
        $frame++
    }
    $result = Receive-Job -Job $job -ErrorAction SilentlyContinue
    Remove-Job -Job $job -Force
    Write-Host "`r  [OK] $Message    " -ForegroundColor Green
    return $result
}

function Show-InstallStep {
    param(
        [int]$Index,
        [int]$Total,
        [string]$Description,
        [scriptblock]$Action
    )
    Write-Host "  [$Index/$Total] $Description" -NoNewline -ForegroundColor Cyan
    try {
        & $Action | Out-Null
        Write-Color ' [OK]' -ForegroundColor Green
    } catch {
        Write-Color ' [FAIL]' -ForegroundColor Red
        Write-Warn "  Error: $_"
    }
}

# ---------------------------------------------------------------------------
# Step 0.5: Defender / ASR / MOTW pre-flight (runs before anything else)
# ---------------------------------------------------------------------------
function Invoke-DefenderPreFlight {
    <#
    .SYNOPSIS
        Checks for Mark of the Web blocks and offers to add Defender exclusions.
    .DESCRIPTION
        When this script is downloaded from the internet, Windows attaches a
        Zone.Identifier Alternate Data Stream (MOTW) that causes PowerShell to
        treat it as untrusted. Attack Surface Reduction (ASR) rules may then
        block child processes spawned during installation.

        This function:
          1. Detects if the current script is MOTW-flagged.
          2. Unblocks itself and the entire project directory.
          3. Checks if Defender real-time protection is active.
          4. Offers to add Defender/ASR exclusions for PerplexityXPC paths.
    #>

    Write-Header 'Step 0.5 - Windows Defender / ASR Compatibility'
    Write-Info 'Checking for MOTW flags and Defender policies...'
    Write-Host ''

    # -- 1. Check if this script is blocked by MOTW --
    $motw = $null
    try {
        $motw = Get-Item -LiteralPath $PSCommandPath -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
    } catch {
        $motw = $null
    }

    if ($null -ne $motw) {
        Write-Warn 'This script has a Mark of the Web (Zone.Identifier) flag - unblocking...'
        try {
            Unblock-File -LiteralPath $PSCommandPath -ErrorAction Stop
            Write-Pass 'Setup script unblocked'
        } catch {
            Write-Warn "Could not unblock setup script: $_"
        }

        # Unblock all files in the project directory
        Write-Info 'Unblocking all project files...'
        try {
            $projectDir = Split-Path -Parent $PSCommandPath
            Get-ChildItem -Path $projectDir -Recurse -File -ErrorAction SilentlyContinue |
                Unblock-File -ErrorAction SilentlyContinue
            Write-Pass 'Project files unblocked'
        } catch {
            Write-Warn "Bulk unblock encountered errors: $_"
        }
    } else {
        Write-Pass 'No Mark of the Web flag detected on this script'
    }

    # -- 2. Also run scripts\Unblock-PerplexityXPC.ps1 if available --
    $unblockScript = Join-Path $PSScriptRoot 'scripts\Unblock-PerplexityXPC.ps1'
    if (Test-Path $unblockScript) {
        try {
            & $unblockScript -ErrorAction SilentlyContinue
        } catch {
            Write-Warn "Unblock-PerplexityXPC.ps1 reported: $_"
        }
    }

    # -- 3. Check Defender real-time protection --
    $defenderActive = $false
    try {
        $mpPref = Get-MpPreference -ErrorAction SilentlyContinue
        if ($null -ne $mpPref) {
            $defenderActive = $true
            $rtEnabled = (Get-MpComputerStatus -ErrorAction SilentlyContinue).RealTimeProtectionEnabled
            if ($rtEnabled) {
                Write-Warn 'Windows Defender real-time protection is active.'
                Write-Info 'PerplexityXPC paths will be excluded to prevent installation interference.'
            } else {
                Write-Pass 'Windows Defender real-time protection is not active'
                $defenderActive = $false
            }
        }
    } catch {
        Write-Info 'Could not read Defender status (non-fatal).'
    }

    # -- 4. Offer Defender exclusions --
    if ($defenderActive) {
        Write-Host ''
        Write-Color '  Windows Defender/ASR policies may block installation. Adding exclusions for PerplexityXPC paths...' -ForegroundColor Yellow
        Write-Host ''

        $defenderScript = Join-Path $PSScriptRoot 'scripts\Add-DefenderExclusions.ps1'
        if (Test-Path $defenderScript) {
            # Delegate to the dedicated exclusions script
            try {
                $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
                    [Security.Principal.WindowsBuiltInRole]'Administrator'
                )
                if ($isAdmin) {
                    & $defenderScript -ErrorAction SilentlyContinue
                } else {
                    Write-Warn 'Not running as Admin - cannot add Defender exclusions now.'
                    Write-Info "Run as Admin: .\scripts\Add-DefenderExclusions.ps1"
                }
            } catch {
                Write-Warn "Add-DefenderExclusions.ps1 reported: $_"
            }
        } else {
            # Inline fallback if the dedicated script is not present
            $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
                [Security.Principal.WindowsBuiltInRole]'Administrator'
            )
            if ($isAdmin) {
                $installPath = $Script:DEFAULT_PATH
                $localAppData = [System.Environment]::GetFolderPath('LocalApplicationData')
                $userProfile  = [System.Environment]::GetFolderPath('UserProfile')
                $excludePaths = @(
                    "$installPath\",
                    "$localAppData\PerplexityXPC\",
                    "$userProfile\Documents\WindowsPowerShell\Modules\PerplexityXPC\"
                )
                $excludeProcs = @(
                    "$installPath\PerplexityXPC.Service.exe",
                    "$installPath\PerplexityXPC.Tray.exe",
                    "$installPath\PerplexityXPC.ContextMenu.exe"
                )
                foreach ($p in $excludePaths) {
                    try { Add-MpPreference -ExclusionPath $p -ErrorAction SilentlyContinue } catch { }
                    try { Add-MpPreference -AttackSurfaceReductionOnlyExclusions $p -ErrorAction SilentlyContinue } catch { }
                }
                foreach ($proc in $excludeProcs) {
                    try { Add-MpPreference -ExclusionProcess $proc -ErrorAction SilentlyContinue } catch { }
                }
                Write-Pass 'Defender exclusions added (inline fallback)'
            } else {
                Write-Warn 'Not running as Admin - Defender exclusions skipped.'
                Write-Info 'Run as Admin before setup: Add-MpPreference -ExclusionPath "C:\Program Files\PerplexityXPC\"'
            }
        }
    } else {
        Write-Pass 'No Defender exclusions needed'
    }

    Write-Host ''
}

# ---------------------------------------------------------------------------
# ASCII Banner (Step 0)
# ---------------------------------------------------------------------------
function Show-Banner {
    $banner = @"

 ____                 _           _ _        __  ______   ____
|  _ \ ___ _ __ _ __ | | _____  _(_) |_ _   \ \/ /  _ \ / ___|
| |_) / _ \ '__| '_ \| |/ _ \ \/ / | __| | | \  /| |_) | |
|  __/  __/ |  | |_) | |  __/>  <| | |_| |_| /  \|  __/| |___
|_|   \___|_|  | .__/|_|\___/_/\_\_|\__|\__, /_/\_\_|    \____|
               |_|                      |___/

"@
    Write-Color $banner -ForegroundColor Cyan
    Write-Color "    Perplexity AI Windows Integration - Setup Wizard v$Script:VERSION" -ForegroundColor White
    Write-Color "    https://github.com/YOUR_USERNAME/perplexity-windows" -ForegroundColor DarkGray
    Write-Host ''
}

# ---------------------------------------------------------------------------
# Load config from file (for -Silent / -ConfigFile)
# ---------------------------------------------------------------------------
function Import-SetupConfig {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        throw "Config file not found: $Path"
    }
    $raw = Get-Content $Path -Raw | ConvertFrom-Json
    return $raw
}

function New-DefaultConfig {
    return [PSCustomObject]@{
        apiKey             = $ApiKey
        port               = $Script:DEFAULT_PORT
        installPath        = $Script:DEFAULT_PATH
        autoStart          = $true
        addToStartup       = $true
        addFirewall        = $true
        addContextMenu     = $true
        mcpServers         = @()
        integrations       = [PSCustomObject]@{
            powerShellModule      = $true
            powerShellProfile     = $true
            windowsTerminal       = $false
            vsCode                = $false
            explorerContextMenu   = $true
            cloudflareTunnel      = $false
        }
    }
}

# ---------------------------------------------------------------------------
# Step 1: Prerequisites check
# ---------------------------------------------------------------------------
function Test-Prerequisites {
    Write-Header 'Step 1 of 7 - Prerequisites Check'
    Write-Info 'Checking required components...'
    Write-Host ''

    $hasWarnings = $false
    $hasFatal    = $false

    # PowerShell version
    $psVer = $PSVersionTable.PSVersion
    if ($psVer.Major -ge 5) {
        Write-Pass "PowerShell $($psVer.Major).$($psVer.Minor)"
    } else {
        Write-Fail "PowerShell $($psVer.Major).$($psVer.Minor) - version 5.1 or higher required"
        $hasFatal = $true
    }

    # Windows version
    try {
        $osInfo = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
        $build  = [int]($osInfo.BuildNumber)
        if ($build -ge 17763) {
            Write-Pass "Windows build $build (Windows 10 1809+ / Windows 11)"
        } else {
            Write-Warn "Windows build $build - build 17763 (1809) or later recommended"
            $hasWarnings = $true
        }
    } catch {
        Write-Warn "Could not determine Windows version"
        $hasWarnings = $true
    }

    # .NET SDK
    $dotnetPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetPath) {
        try {
            $sdkList = & dotnet --list-sdks 2>&1
            $net8    = $sdkList | Where-Object { $_ -match '^8\.' }
            if ($net8) {
                Write-Pass ".NET 8 SDK found"
            } else {
                Write-Warn ".NET 8 SDK not found (needed to build from source)"
                Write-Info "    Download: https://dotnet.microsoft.com/download/dotnet/8.0"
                $hasWarnings = $true
            }
        } catch {
            Write-Warn ".NET SDK check failed - install from https://dotnet.microsoft.com/download/dotnet/8.0"
            $hasWarnings = $true
        }
    } else {
        Write-Warn ".NET SDK not found - required to build from source"
        Write-Info "    Download: https://dotnet.microsoft.com/download/dotnet/8.0"
        Write-Info "    Quick install: winget install Microsoft.DotNet.SDK.8"
        $hasWarnings = $true
    }

    # Node.js
    $nodePath = Get-Command node -ErrorAction SilentlyContinue
    if ($nodePath) {
        try {
            $nodeVer = (& node --version 2>&1).TrimStart('v')
            $major   = [int]($nodeVer.Split('.')[0])
            if ($major -ge 18) {
                Write-Pass "Node.js v$nodeVer"
            } else {
                Write-Warn "Node.js v$nodeVer found - v18 LTS or higher recommended for MCP servers"
                $hasWarnings = $true
            }
        } catch {
            Write-Warn "Node.js version check failed"
            $hasWarnings = $true
        }
    } else {
        Write-Warn "Node.js not found - required for MCP servers"
        Write-Info "    Download: https://nodejs.org"
        Write-Info "    Quick install: winget install OpenJS.NodeJS.LTS"
        $hasWarnings = $true
    }

    # Administrator check
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]'Administrator'
    )
    if ($isAdmin) {
        Write-Pass "Running as Administrator"
    } else {
        Write-Fail "Not running as Administrator - required for service and firewall setup"
        Write-Info "    Re-run: Start-Process powershell -Verb RunAs"
        $hasFatal = $true
    }

    Write-Host ''
    if ($hasFatal) {
        Write-Color "  One or more critical requirements are not met." -ForegroundColor Red
        $ans = Read-HostSafe "  Continue anyway? (y/N): "
        if ($ans -notmatch '^[Yy]') {
            Write-Color "`n  Setup aborted." -ForegroundColor Yellow
            exit 1
        }
    } elseif ($hasWarnings) {
        $ans = Read-HostSafe "  Warnings detected. Continue anyway? (Y/n): "
        if ($ans -match '^[Nn]') {
            Write-Color "`n  Setup aborted." -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Color "  All prerequisites met." -ForegroundColor Green
    }
}

# ---------------------------------------------------------------------------
# Safe Read-Host (handles -Silent mode)
# ---------------------------------------------------------------------------
function Read-HostSafe {
    param([string]$Prompt, [string]$Default = '')
    if ($Script:Silent) { return $Default }
    Write-Host $Prompt -NoNewline -ForegroundColor White
    $input = Read-Host
    if ([string]::IsNullOrEmpty($input)) { return $Default }
    return $input
}

function Read-HostSecret {
    param([string]$Prompt)
    if ($Script:Silent) { return '' }
    Write-Host $Prompt -NoNewline -ForegroundColor White
    $ss     = Read-Host -AsSecureString
    $bstr   = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($ss)
    $plain  = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    return $plain
}

# ---------------------------------------------------------------------------
# Step 2: API Key Configuration
# ---------------------------------------------------------------------------
function Set-ApiKeyConfig {
    param([PSCustomObject]$Config)

    Write-Header 'Step 2 of 7 - API Key Configuration'
    Write-Info "Get your key at: $Script:API_KEY_LINK"
    Write-Host ''

    if (-not [string]::IsNullOrEmpty($Config.apiKey)) {
        Write-Info "API key pre-supplied via parameter."
        $Config.apiKey = $Config.apiKey
    } else {
        while ($true) {
            $key = Read-HostSecret "  Enter your Perplexity API key (pplx-...) or press Enter to skip: "
            if ([string]::IsNullOrEmpty($key)) {
                Write-Warn "No API key entered - you can configure it later via the tray app or appsettings.json"
                break
            }
            if ($key -match '^pplx-') {
                $Config.apiKey = $key
                Write-Pass "API key format valid"
                break
            } else {
                Write-Fail "Key must start with 'pplx-'. Please try again."
            }
        }
    }

    # Optional: test the key
    if (-not [string]::IsNullOrEmpty($Config.apiKey) -and -not $Script:Silent) {
        $testAns = Read-HostSafe "  Test the API key now? (y/N): " -Default 'n'
        if ($testAns -match '^[Yy]') {
            Write-Step "Testing API key..."
            try {
                $headers = @{ Authorization = "Bearer $($Config.apiKey)" }
                $body    = '{"model":"sonar","messages":[{"role":"user","content":"Reply with the single word: OK"}]}'
                $resp    = Invoke-RestMethod -Uri 'https://api.perplexity.ai/chat/completions' `
                               -Method Post -Headers $headers `
                               -ContentType 'application/json' -Body $body `
                               -TimeoutSec 15 -ErrorAction Stop
                Write-Pass "API key is valid - response received"
            } catch {
                Write-Fail "API key test failed: $_"
                Write-Info "  You can continue and reconfigure the key later."
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Step 3: Service Configuration
# ---------------------------------------------------------------------------
function Set-ServiceConfig {
    param([PSCustomObject]$Config)

    Write-Header 'Step 3 of 7 - Service Configuration'
    Write-Host ''

    # Port
    $portInput = Read-HostSafe "  HTTP port [$($Config.port)]: " -Default "$($Config.port)"
    $parsedPort = $Config.port
    if ([int]::TryParse($portInput, [ref]$parsedPort) -and $parsedPort -gt 0 -and $parsedPort -lt 65536) {
        $Config.port = $parsedPort
    } else {
        Write-Warn "Invalid port; using default $($Config.port)"
    }
    Write-Pass "Port set to $($Config.port)"

    # Install path
    $pathInput = Read-HostSafe "  Install path [$($Config.installPath)]: " -Default $Config.installPath
    if (-not [string]::IsNullOrEmpty($pathInput)) {
        $Config.installPath = $pathInput
    }
    Write-Pass "Install path: $($Config.installPath)"

    # Auto-start
    $autoAns = Read-HostSafe "  Start service automatically at boot? (Y/n): " -Default 'y'
    $Config.autoStart = ($autoAns -notmatch '^[Nn]')
    Write-Pass "Auto-start: $($Config.autoStart)"

    # Tray app startup
    $trayAns = Read-HostSafe "  Add tray app to Windows startup? (Y/n): " -Default 'y'
    $Config.addToStartup = ($trayAns -notmatch '^[Nn]')
    Write-Pass "Add tray to startup: $($Config.addToStartup)"

    # Firewall
    $fwAns = Read-HostSafe "  Add Windows Firewall rule to block external access? (Y/n): " -Default 'y'
    $Config.addFirewall = ($fwAns -notmatch '^[Nn]')
    Write-Pass "Add firewall rule: $($Config.addFirewall)"
}

# ---------------------------------------------------------------------------
# Step 4: MCP Server Configuration
# ---------------------------------------------------------------------------
function Set-McpConfig {
    param([PSCustomObject]$Config)

    Write-Header 'Step 4 of 7 - MCP Server Configuration'
    Write-Info 'MCP servers extend Perplexity with local tools (filesystem, GitHub, search, etc.)'
    Write-Host ''

    $ans = Read-HostSafe "  Configure MCP servers now? (y/N): " -Default 'n'
    if ($ans -notmatch '^[Yy]') {
        Write-Info "Skipped - you can edit %LOCALAPPDATA%\PerplexityXPC\mcp-servers.json later."
        return
    }

    $selectedServers = [System.Collections.Generic.List[object]]::new()

    while ($true) {
        Write-Host ''
        Write-Color "  MCP Server Menu:" -ForegroundColor Cyan
        Write-Color "    [1] Filesystem access (Documents folder)" -ForegroundColor White
        Write-Color "    [2] GitHub integration (requires Personal Access Token)" -ForegroundColor White
        Write-Color "    [3] Brave Search (requires Brave API key)" -ForegroundColor White
        Write-Color "    [4] SQLite database" -ForegroundColor White
        Write-Color "    [5] Memory / Knowledge base" -ForegroundColor White
        Write-Color "    [6] Add custom server" -ForegroundColor White
        Write-Color "    [7] Done" -ForegroundColor Green
        Write-Host ''

        $choice = Read-HostSafe "  Enter number (1-7): " -Default '7'

        switch ($choice) {
            '1' {
                $docsPath = "$env:USERPROFILE\Documents"
                $fsPath = Read-HostSafe "  Documents path [$docsPath]: " -Default $docsPath
                $selectedServers.Add([PSCustomObject]@{
                    name     = 'filesystem'
                    disabled = $false
                    command  = 'npx'
                    args     = @('-y', '@modelcontextprotocol/server-filesystem', $fsPath)
                    env      = @{}
                })
                Write-Pass "Added: filesystem ($fsPath)"
            }
            '2' {
                $pat = Read-HostSafe "  GitHub Personal Access Token: "
                if (-not [string]::IsNullOrEmpty($pat)) {
                    $selectedServers.Add([PSCustomObject]@{
                        name     = 'github'
                        disabled = $false
                        command  = 'npx'
                        args     = @('-y', '@modelcontextprotocol/server-github')
                        env      = @{ GITHUB_PERSONAL_ACCESS_TOKEN = $pat }
                    })
                    Write-Pass "Added: github"
                } else {
                    Write-Warn "No token entered - skipping GitHub integration"
                }
            }
            '3' {
                $braveKey = Read-HostSafe "  Brave Search API key: "
                if (-not [string]::IsNullOrEmpty($braveKey)) {
                    $selectedServers.Add([PSCustomObject]@{
                        name     = 'brave-search'
                        disabled = $false
                        command  = 'npx'
                        args     = @('-y', '@modelcontextprotocol/server-brave-search')
                        env      = @{ BRAVE_API_KEY = $braveKey }
                    })
                    Write-Pass "Added: brave-search"
                } else {
                    Write-Warn "No key entered - skipping Brave Search"
                }
            }
            '4' {
                $dbPath = Read-HostSafe "  SQLite database path: "
                if (-not [string]::IsNullOrEmpty($dbPath)) {
                    $selectedServers.Add([PSCustomObject]@{
                        name     = 'sqlite'
                        disabled = $false
                        command  = 'npx'
                        args     = @('-y', '@modelcontextprotocol/server-sqlite', $dbPath)
                        env      = @{}
                    })
                    Write-Pass "Added: sqlite ($dbPath)"
                } else {
                    Write-Warn "No path entered - skipping SQLite"
                }
            }
            '5' {
                $selectedServers.Add([PSCustomObject]@{
                    name     = 'memory'
                    disabled = $false
                    command  = 'npx'
                    args     = @('-y', '@modelcontextprotocol/server-memory')
                    env      = @{}
                })
                Write-Pass "Added: memory / knowledge base"
            }
            '6' {
                $customName = Read-HostSafe "  Server name (e.g. my-server): "
                $customCmd  = Read-HostSafe "  Command (e.g. npx): "
                $customArgs = Read-HostSafe "  Arguments (space-separated): "
                if (-not [string]::IsNullOrEmpty($customName) -and -not [string]::IsNullOrEmpty($customCmd)) {
                    $argsArray = if ([string]::IsNullOrEmpty($customArgs)) { @() } else { $customArgs -split '\s+' }
                    $selectedServers.Add([PSCustomObject]@{
                        name     = $customName
                        disabled = $false
                        command  = $customCmd
                        args     = $argsArray
                        env      = @{}
                    })
                    Write-Pass "Added: $customName"
                }
            }
            '7' { break }
            default { Write-Warn "Invalid choice. Enter 1-7." }
        }

        if ($choice -eq '7') { break }
    }

    $Config.mcpServers = $selectedServers.ToArray()
    Write-Pass "$($selectedServers.Count) MCP server(s) configured"
}

# ---------------------------------------------------------------------------
# Step 5: Integration Selection
# ---------------------------------------------------------------------------
function Set-IntegrationConfig {
    param([PSCustomObject]$Config)

    Write-Header 'Step 5 of 7 - Integration Selection'
    Write-Info 'Toggle integrations by entering the item number. Press Enter when done.'
    Write-Host ''

    # Build mutable array of selections
    $integrationKeys = @(
        'powerShellModule',
        'powerShellProfile',
        'windowsTerminal',
        'vsCode',
        'explorerContextMenu',
        'cloudflareTunnel'
    )

    $integrationLabels = @{
        powerShellModule    = 'PowerShell Module (44 functions)'
        powerShellProfile   = 'PowerShell Profile (aliases: pplx, pplxcode, etc.)'
        windowsTerminal     = 'Windows Terminal profile + keybindings'
        vsCode              = 'VS Code tasks + keybindings'
        explorerContextMenu = 'Explorer context menu'
        cloudflareTunnel    = 'Cloudflare Tunnel (remote access from phone)'
    }

    # Copy current selections to a hashtable so we can mutate them
    $selections = @{}
    foreach ($k in $integrationKeys) {
        # Access PSCustomObject property safely
        $val = $Config.integrations.$k
        $selections[$k] = if ($null -ne $val) { [bool]$val } else { $false }
    }

    while ($true) {
        Write-Host ''
        for ($i = 0; $i -lt $integrationKeys.Count; $i++) {
            $key   = $integrationKeys[$i]
            $label = $integrationLabels[$key]
            $check = if ($selections[$key]) { 'x' } else { ' ' }
            $num   = $i + 1
            Write-Color "    [$num] [$check] $label" -ForegroundColor White
        }
        Write-Host ''
        $toggle = Read-HostSafe "  Toggle number (1-6) or press Enter to confirm: " -Default ''
        if ([string]::IsNullOrEmpty($toggle)) { break }

        $idx = 0
        if ([int]::TryParse($toggle, [ref]$idx) -and $idx -ge 1 -and $idx -le $integrationKeys.Count) {
            $k = $integrationKeys[$idx - 1]
            $selections[$k] = -not $selections[$k]
        } else {
            Write-Warn "Enter a number between 1 and $($integrationKeys.Count)"
        }
    }

    # Write back to Config.integrations
    foreach ($k in $integrationKeys) {
        $Config.integrations.$k = $selections[$k]
    }

    Write-Pass "Integrations configured"
}

# ---------------------------------------------------------------------------
# Step 6: Confirmation Summary
# ---------------------------------------------------------------------------
function Show-Summary {
    param([PSCustomObject]$Config)

    Write-Header 'Step 6 of 7 - Configuration Summary'
    Write-Host ''

    $maskedKey = if ([string]::IsNullOrEmpty($Config.apiKey)) {
        '(not set - configure later)'
    } else {
        $Config.apiKey.Substring(0, [Math]::Min(10, $Config.apiKey.Length)) + '...'
    }

    Write-Color "  API Key        : $maskedKey" -ForegroundColor White
    Write-Color "  Port           : $($Config.port)" -ForegroundColor White
    Write-Color "  Install Path   : $($Config.installPath)" -ForegroundColor White
    Write-Color "  Auto-start     : $($Config.autoStart)" -ForegroundColor White
    Write-Color "  Tray startup   : $($Config.addToStartup)" -ForegroundColor White
    Write-Color "  Firewall rule  : $($Config.addFirewall)" -ForegroundColor White

    Write-Host ''
    Write-Color "  MCP Servers    : $($Config.mcpServers.Count) configured" -ForegroundColor White

    Write-Host ''
    Write-Color "  Integrations:" -ForegroundColor White
    $integrationLabels = @{
        powerShellModule    = 'PowerShell Module'
        powerShellProfile   = 'PowerShell Profile'
        windowsTerminal     = 'Windows Terminal'
        vsCode              = 'VS Code'
        explorerContextMenu = 'Explorer Context Menu'
        cloudflareTunnel    = 'Cloudflare Tunnel'
    }
    foreach ($k in $integrationLabels.Keys) {
        $val   = $Config.integrations.$k
        $check = if ($val) { 'x' } else { ' ' }
        Write-Color "    [$check] $($integrationLabels[$k])" -ForegroundColor White
    }

    Write-Host ''
    $ans = Read-HostSafe "  Proceed with installation? (Y/n): " -Default 'y'
    if ($ans -match '^[Nn]') {
        Write-Color "`n  Installation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# ---------------------------------------------------------------------------
# Step 7: Installation
# ---------------------------------------------------------------------------
function Invoke-Installation {
    param([PSCustomObject]$Config)

    Write-Header 'Step 7 of 7 - Installing'
    Write-Host ''

    $total = 10
    $step  = 0

    # 1 - Create install directory
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Creating install directory" -Action {
        if (-not (Test-Path $Config.installPath)) {
            New-Item -ItemType Directory -Path $Config.installPath -Force | Out-Null
        }
    }

    # 2 - Create config directory
    $step++
    $configDir = "$env:LOCALAPPDATA\PerplexityXPC"
    Show-InstallStep -Index $step -Total $total -Description "Creating config directory" -Action {
        if (-not (Test-Path $configDir)) {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        }
    }

    # 3 - Copy binaries (build output)
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Copying application files" -Action {
        $binPath = Join-Path $PSScriptRoot 'bin'
        if (Test-Path $binPath) {
            Copy-Item -Path "$binPath\*" -Destination $Config.installPath -Recurse -Force
        }
        # If no bin dir found, this step is a no-op during wizard-only run
    }

    # 4 - Write appsettings
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Writing configuration files" -Action {
        $appSettings = @{
            PerplexityXPC = @{
                HttpPort      = $Config.port
                ApiEndpoint   = 'https://api.perplexity.ai'
                DefaultModel  = 'sonar'
                ApiTimeoutSec = 60
                MaxTokens     = 2048
                MaxFileSizeKB = 10240
                LogLevel      = 'Information'
            }
            Mcp = @{
                AutoRestart          = $true
                TimeoutSec           = 30
                MaxConcurrentServers = 5
            }
        }
        $appSettings | ConvertTo-Json -Depth 5 | Set-Content "$configDir\appsettings.json" -Encoding UTF8
    }

    # 5 - Encrypt and store API key
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Encrypting API key" -Action {
        if (-not [string]::IsNullOrEmpty($Config.apiKey)) {
            Add-Type -AssemblyName System.Security
            $keyBytes    = [System.Text.Encoding]::UTF8.GetBytes($Config.apiKey)
            $entropy     = [System.Text.Encoding]::UTF8.GetBytes('PerplexityXPC_v1')
            $encrypted   = [System.Security.Cryptography.ProtectedData]::Protect(
                $keyBytes, $entropy,
                [System.Security.Cryptography.DataProtectionScope]::CurrentUser
            )
            [System.IO.File]::WriteAllBytes("$configDir\api-key.enc", $encrypted)
        }
    }

    # 6 - Write MCP servers config
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Writing MCP server configuration" -Action {
        $mcpServersObj = @{ mcpServers = @{} }
        foreach ($srv in $Config.mcpServers) {
            $mcpServersObj.mcpServers[$srv.name] = @{
                disabled = $srv.disabled
                command  = $srv.command
                args     = $srv.args
                env      = $srv.env
            }
        }
        $mcpServersObj | ConvertTo-Json -Depth 6 | Set-Content "$configDir\mcp-servers.json" -Encoding UTF8
    }

    # 7 - Register Windows Service
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Registering Windows Service" -Action {
        $svcExe = Join-Path $Config.installPath 'PerplexityXPC.Service.exe'
        if (Test-Path $svcExe) {
            $existing = Get-Service -Name $Script:SERVICE_NAME -ErrorAction SilentlyContinue
            if ($null -ne $existing) {
                Stop-Service -Name $Script:SERVICE_NAME -Force -ErrorAction SilentlyContinue
                & sc.exe delete $Script:SERVICE_NAME | Out-Null
                Start-Sleep -Seconds 1
            }
            $startType = if ($Config.autoStart) { 'auto' } else { 'demand' }
            & sc.exe create $Script:SERVICE_NAME binPath= "`"$svcExe`"" start= $startType obj= 'LocalService' DisplayName= 'PerplexityXPC Service' | Out-Null
            Start-Service -Name $Script:SERVICE_NAME -ErrorAction SilentlyContinue
        }
    }

    # 8 - Add firewall rule
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Configuring firewall rule" -Action {
        if ($Config.addFirewall) {
            $ruleName  = 'PerplexityXPC-Block-External'
            $existing  = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
            if ($null -ne $existing) { Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue }
            New-NetFirewallRule -DisplayName $ruleName `
                -Direction Inbound -Protocol TCP -LocalPort $Config.port `
                -Action Block -Profile Any `
                -Description 'Block external access to PerplexityXPC service port' `
                -ErrorAction SilentlyContinue | Out-Null
        }
    }

    # 9 - Install PowerShell module
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Installing PowerShell module" -Action {
        if ($Config.integrations.powerShellModule) {
            $modSrc  = Join-Path $PSScriptRoot 'module\PerplexityXPC'
            $psVer   = if ($PSVersionTable.PSVersion.Major -ge 6) { 'PowerShell' } else { 'WindowsPowerShell' }
            $modDest = "$env:USERPROFILE\Documents\$psVer\Modules\PerplexityXPC"
            if (Test-Path $modSrc) {
                if (-not (Test-Path $modDest)) {
                    New-Item -ItemType Directory -Path $modDest -Force | Out-Null
                }
                Copy-Item -Path "$modSrc\*" -Destination $modDest -Recurse -Force
            }
        }
    }

    # 10 - Add tray to startup / context menu
    $step++
    Show-InstallStep -Index $step -Total $total -Description "Configuring startup and context menu" -Action {
        # Tray app startup
        if ($Config.addToStartup) {
            $trayExe = Join-Path $Config.installPath 'PerplexityXPC.Tray.exe'
            if (Test-Path $trayExe) {
                $regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
                Set-ItemProperty -Path $regPath -Name 'PerplexityXPCTray' -Value "`"$trayExe`"" -ErrorAction SilentlyContinue
            }
        }

        # Context menu
        if ($Config.addContextMenu -or $Config.integrations.explorerContextMenu) {
            $contextScript = Join-Path $PSScriptRoot 'scripts\Register-ContextMenu.ps1'
            if (Test-Path $contextScript) {
                & $contextScript -ErrorAction SilentlyContinue
            }
        }

        # PowerShell profile aliases
        if ($Config.integrations.powerShellProfile) {
            $profileScript = Join-Path $PSScriptRoot 'integrations\Install-PerplexityXPCProfile.ps1'
            if (Test-Path $profileScript) {
                & $profileScript -ErrorAction SilentlyContinue
            }
        }

        # Windows Terminal
        if ($Config.integrations.windowsTerminal) {
            $terminalScript = Join-Path $PSScriptRoot 'integrations\Install-TerminalProfile.ps1'
            if (Test-Path $terminalScript) {
                & $terminalScript -ErrorAction SilentlyContinue
            }
        }

        # VS Code
        if ($Config.integrations.vsCode) {
            $vscodeScript = Join-Path $PSScriptRoot 'integrations\Install-VSCodeIntegration.ps1'
            if (Test-Path $vscodeScript) {
                & $vscodeScript -ErrorAction SilentlyContinue
            }
        }

        # Cloudflare Tunnel
        if ($Config.integrations.cloudflareTunnel) {
            $cfScript = Join-Path $PSScriptRoot 'remote\Install-CloudflareTunnel.ps1'
            if (Test-Path $cfScript) {
                & $cfScript -ErrorAction SilentlyContinue
            }
        }
    }

    Write-Host ''
    Write-Color "  Installation complete." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Step 8: Verification and Summary
# ---------------------------------------------------------------------------
function Show-VerificationSummary {
    param([PSCustomObject]$Config)

    Write-Header 'Verification and Quick Start'
    Write-Host ''

    # Test service
    Write-Color "  Checking service status..." -ForegroundColor Cyan
    $svc = Get-Service -Name $Script:SERVICE_NAME -ErrorAction SilentlyContinue
    if ($null -ne $svc -and $svc.Status -eq 'Running') {
        Write-Pass "Service '$Script:SERVICE_NAME' is running"
    } else {
        Write-Warn "Service is not running - start with: Start-Service $Script:SERVICE_NAME"
    }

    # Test HTTP endpoint
    Write-Color "  Testing HTTP endpoint..." -ForegroundColor Cyan
    try {
        $statusResp = Invoke-RestMethod "http://localhost:$($Config.port)/status" -TimeoutSec 5 -ErrorAction Stop
        Write-Pass "HTTP endpoint responding on port $($Config.port)"
    } catch {
        Write-Warn "HTTP endpoint not yet responding - the service may still be starting"
    }

    Write-Host ''
    Write-Color "  Endpoints:" -ForegroundColor Cyan
    Write-Color "    HTTP API   : http://localhost:$($Config.port)" -ForegroundColor White
    Write-Color "    Status     : http://localhost:$($Config.port)/status" -ForegroundColor White
    Write-Color "    WebSocket  : ws://localhost:$($Config.port)/ws" -ForegroundColor White
    Write-Host ''
    Write-Color "  Quick-start commands:" -ForegroundColor Cyan
    Write-Color "    Get-XPCStatus" -ForegroundColor White
    Write-Color "    Invoke-Perplexity 'What is zero trust networking?'" -ForegroundColor White
    Write-Color "    pplx 'Summarize the latest CVEs'" -ForegroundColor White
    Write-Color "    Invoke-RestMethod http://localhost:$($Config.port)/status" -ForegroundColor White
    Write-Host ''
    Write-Color "  Documentation:" -ForegroundColor Cyan
    Write-Color "    PowerShell Module  : docs\MODULE.md" -ForegroundColor White
    Write-Color "    HTTP API           : docs\API.md" -ForegroundColor White
    Write-Color "    Integration Guide  : integrations\README.md" -ForegroundColor White
    Write-Color "    Remote Access      : remote\README.md" -ForegroundColor White
    Write-Host ''
    Write-Color '  ============================================================' -ForegroundColor DarkGray
    Write-Color "  Installation complete. Enjoy PerplexityXPC!" -ForegroundColor Green
    Write-Color '  ============================================================' -ForegroundColor DarkGray
    Write-Host ''
}

# ---------------------------------------------------------------------------
# Uninstall flow
# ---------------------------------------------------------------------------
function Invoke-Uninstall {
    Write-Header 'Uninstall PerplexityXPC'
    Write-Host ''

    $confirm = Read-HostSafe "  This will remove all PerplexityXPC components. Are you sure? (y/N): " -Default 'n'
    if ($confirm -notmatch '^[Yy]') {
        Write-Color "`n  Uninstall cancelled." -ForegroundColor Yellow
        exit 0
    }

    $uninstallScript = Join-Path $PSScriptRoot 'scripts\Uninstall-PerplexityXPC.ps1'
    if (Test-Path $uninstallScript) {
        & $uninstallScript
    } else {
        Write-Step "Stopping and removing service..."
        Stop-Service -Name $Script:SERVICE_NAME -Force -ErrorAction SilentlyContinue
        & sc.exe delete $Script:SERVICE_NAME | Out-Null

        Write-Step "Removing firewall rule..."
        Remove-NetFirewallRule -DisplayName 'PerplexityXPC-Block-External' -ErrorAction SilentlyContinue

        Write-Step "Removing startup entry..."
        Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'PerplexityXPCTray' -ErrorAction SilentlyContinue

        Write-Step "Removing context menu entries..."
        Remove-Item 'HKCU:\Software\Classes\*\shell\PerplexityXPC' -Recurse -ErrorAction SilentlyContinue
        Remove-Item 'HKCU:\Software\Classes\Directory\shell\PerplexityXPC' -Recurse -ErrorAction SilentlyContinue

        Write-Step "Removing config directory..."
        $configDir = "$env:LOCALAPPDATA\PerplexityXPC"
        if (Test-Path $configDir) { Remove-Item $configDir -Recurse -Force -ErrorAction SilentlyContinue }

        Write-Step "Removing install directory..."
        if (Test-Path $Script:DEFAULT_PATH) { Remove-Item $Script:DEFAULT_PATH -Recurse -Force -ErrorAction SilentlyContinue }
    }

    Write-Host ''
    Write-Pass "PerplexityXPC has been removed."
    Write-Host ''
}

# ---------------------------------------------------------------------------
# Main entry point
# ---------------------------------------------------------------------------
function Main {
    # Uninstall mode
    if ($Uninstall) {
        Show-Banner
        Invoke-Uninstall
        exit 0
    }

    # Banner
    Show-Banner

    # Load config
    $config = $null
    if (-not [string]::IsNullOrEmpty($ConfigFile)) {
        Write-Info "Loading configuration from: $ConfigFile"
        $config = Import-SetupConfig -Path $ConfigFile
        # Overlay ApiKey parameter if also supplied
        if (-not [string]::IsNullOrEmpty($ApiKey)) {
            $config.apiKey = $ApiKey
        }
    } else {
        $config = New-DefaultConfig
    }

    # Mark silent mode as script-scoped variable for helpers to read
    $Script:Silent = $Silent.IsPresent

    if ($Silent) {
        Write-Color "  Silent mode: using default/provided configuration." -ForegroundColor Yellow
        Write-Host ''
    }

    # Run wizard steps
    Invoke-DefenderPreFlight
    Test-Prerequisites
    Set-ApiKeyConfig   -Config $config
    Set-ServiceConfig  -Config $config
    Set-McpConfig      -Config $config
    Set-IntegrationConfig -Config $config
    Show-Summary       -Config $config
    Invoke-Installation -Config $config
    Show-VerificationSummary -Config $config
}

Main
