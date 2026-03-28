#Requires -Version 5.1
<#
.SYNOPSIS
    Adds Windows Defender and ASR exclusions for PerplexityXPC.

.DESCRIPTION
    PerplexityXPC runs as a Windows Service and spawns child processes (MCP servers,
    tray application, context menu helper). Without Defender exclusions, real-time
    protection and Attack Surface Reduction (ASR) rules may block or quarantine these
    processes during installation and at runtime.

    This script must be run as Administrator BEFORE running Summon-Aunties.ps1
    or Install-PerplexityXPC.ps1.

    It adds exclusions for:
      - Process: PerplexityXPC.Service.exe
      - Process: PerplexityXPC.Tray.exe
      - Process: PerplexityXPC.ContextMenu.exe
      - Path:    C:\Program Files\PerplexityXPC\ (or custom InstallPath)
      - Path:    %LOCALAPPDATA%\PerplexityXPC\
      - Path:    %USERPROFILE%\Documents\WindowsPowerShell\Modules\PerplexityXPC\

    When ASR is active it also adds the install path to the ASR-only exclusion list
    (AttackSurfaceReductionOnlyExclusions).

    Use the -Uninstall switch to remove all exclusions added by this script.

.PARAMETER InstallPath
    The PerplexityXPC installation directory.
    Defaults to "C:\Program Files\PerplexityXPC".

.PARAMETER Uninstall
    Removes all Defender and ASR exclusions added for PerplexityXPC.

.EXAMPLE
    .\Add-DefenderExclusions.ps1

.EXAMPLE
    .\Add-DefenderExclusions.ps1 -InstallPath "D:\Apps\PerplexityXPC"

.EXAMPLE
    .\Add-DefenderExclusions.ps1 -Uninstall

.NOTES
    Requires Windows 10 build 1809 or later.
    Must be run as Administrator.
    Compatible with PowerShell 5.1 and PowerShell 7+.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(HelpMessage = "PerplexityXPC installation directory. Default: C:\Program Files\PerplexityXPC")]
    [string]$InstallPath = 'C:\Program Files\PerplexityXPC',

    [Parameter(HelpMessage = "Remove all PerplexityXPC Defender exclusions.")]
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Color helpers
# ---------------------------------------------------------------------------
function Write-Ok   { param([string]$Msg) Write-Host "  [OK]   $Msg" -ForegroundColor Green }
function Write-Warn { param([string]$Msg) Write-Host "  [WARN] $Msg" -ForegroundColor Yellow }
function Write-Err  { param([string]$Msg) Write-Host "  [ERR]  $Msg" -ForegroundColor Red }
function Write-Info { param([string]$Msg) Write-Host "  [INFO] $Msg" -ForegroundColor Cyan }
function Write-Head { param([string]$Msg)
    $line = '-' * 60
    Write-Host ''
    Write-Host $line -ForegroundColor DarkGray
    Write-Host "  $Msg" -ForegroundColor Cyan
    Write-Host $line -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Admin check
# ---------------------------------------------------------------------------
function Test-IsAdmin {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
    Write-Err 'This script must be run as Administrator.'
    Write-Host '  Re-run from an elevated PowerShell prompt, or right-click and choose "Run as Administrator".' -ForegroundColor Yellow
    exit 1
}

# ---------------------------------------------------------------------------
# Build exclusion lists
# ---------------------------------------------------------------------------
$localAppData = [System.Environment]::GetFolderPath('LocalApplicationData')
$userProfile  = [System.Environment]::GetFolderPath('UserProfile')

# Expand the InstallPath trailing slash for consistency
$installPathNorm = $InstallPath.TrimEnd('\') + '\'

$excludePaths = @(
    $installPathNorm,
    "$localAppData\PerplexityXPC\",
    "$userProfile\Documents\WindowsPowerShell\Modules\PerplexityXPC\"
)

$excludeProcesses = @(
    (Join-Path $InstallPath 'PerplexityXPC.Service.exe'),
    (Join-Path $InstallPath 'PerplexityXPC.Tray.exe'),
    (Join-Path $InstallPath 'PerplexityXPC.ContextMenu.exe')
)

# ---------------------------------------------------------------------------
# Check if Defender is available
# ---------------------------------------------------------------------------
$mpPreferenceAvailable = $null -ne (Get-Command 'Get-MpPreference' -ErrorAction SilentlyContinue)
if (-not $mpPreferenceAvailable) {
    Write-Warn 'Get-MpPreference is not available on this system. Windows Defender may not be installed.'
    Write-Warn 'Skipping exclusion configuration.'
    exit 0
}

# ---------------------------------------------------------------------------
# Uninstall mode - remove exclusions
# ---------------------------------------------------------------------------
if ($Uninstall) {
    Write-Head 'Removing PerplexityXPC Defender Exclusions'

    foreach ($path in $excludePaths) {
        try {
            if ($PSCmdlet.ShouldProcess($path, 'Remove Defender path exclusion')) {
                Remove-MpPreference -ExclusionPath $path -ErrorAction SilentlyContinue
                Remove-MpPreference -AttackSurfaceReductionOnlyExclusions $path -ErrorAction SilentlyContinue
                Write-Ok "Removed path exclusion: $path"
            }
        } catch {
            Write-Warn "Could not remove path exclusion '$path': $_"
        }
    }

    foreach ($proc in $excludeProcesses) {
        try {
            if ($PSCmdlet.ShouldProcess($proc, 'Remove Defender process exclusion')) {
                Remove-MpPreference -ExclusionProcess $proc -ErrorAction SilentlyContinue
                Write-Ok "Removed process exclusion: $proc"
            }
        } catch {
            Write-Warn "Could not remove process exclusion '$proc': $_"
        }
    }

    Write-Host ''
    Write-Ok 'All PerplexityXPC Defender exclusions removed.'
    exit 0
}

# ---------------------------------------------------------------------------
# Add exclusions
# ---------------------------------------------------------------------------
Write-Head 'Adding PerplexityXPC Defender Exclusions'
Write-Info 'This ensures Windows Defender and ASR rules do not block PerplexityXPC.'
Write-Host ''

# -- Path exclusions --
Write-Host '  Path exclusions:' -ForegroundColor White
foreach ($path in $excludePaths) {
    try {
        if ($PSCmdlet.ShouldProcess($path, 'Add Defender path exclusion')) {
            Add-MpPreference -ExclusionPath $path -ErrorAction Stop
            Write-Ok "Path: $path"
        }
    } catch {
        Write-Err "Failed to add path exclusion '$path': $_"
    }
}

Write-Host ''

# -- Process exclusions --
Write-Host '  Process exclusions:' -ForegroundColor White
foreach ($proc in $excludeProcesses) {
    try {
        if ($PSCmdlet.ShouldProcess($proc, 'Add Defender process exclusion')) {
            Add-MpPreference -ExclusionProcess $proc -ErrorAction Stop
            Write-Ok "Process: $proc"
        }
    } catch {
        Write-Err "Failed to add process exclusion '$proc': $_"
    }
}

Write-Host ''

# -- ASR exclusions (only if ASR is configured) --
Write-Host '  ASR (Attack Surface Reduction) exclusions:' -ForegroundColor White
try {
    $mpPref = Get-MpPreference -ErrorAction Stop
    # AttackSurfaceReductionRules_Ids is non-null when ASR rules are configured
    $asrEnabled = ($null -ne $mpPref.AttackSurfaceReductionRules_Ids) -and
                  ($mpPref.AttackSurfaceReductionRules_Ids.Count -gt 0)

    if ($asrEnabled) {
        foreach ($path in $excludePaths) {
            try {
                if ($PSCmdlet.ShouldProcess($path, 'Add ASR exclusion')) {
                    Add-MpPreference -AttackSurfaceReductionOnlyExclusions $path -ErrorAction Stop
                    Write-Ok "ASR exclusion: $path"
                }
            } catch {
                Write-Err "Failed to add ASR exclusion '$path': $_"
            }
        }
    } else {
        Write-Info 'No ASR rules detected - skipping AttackSurfaceReductionOnlyExclusions (not needed).'
    }
} catch {
    Write-Warn "Could not read ASR configuration: $_"
    Write-Info 'Attempting to add ASR exclusions anyway...'
    foreach ($path in $excludePaths) {
        try {
            if ($PSCmdlet.ShouldProcess($path, 'Add ASR exclusion')) {
                Add-MpPreference -AttackSurfaceReductionOnlyExclusions $path -ErrorAction SilentlyContinue
                Write-Ok "ASR exclusion (best-effort): $path"
            }
        } catch {
            Write-Warn "Could not add ASR exclusion '$path': $_"
        }
    }
}

Write-Host ''
Write-Host '  --------------------------------------------------------' -ForegroundColor DarkGray
Write-Ok 'Defender exclusions configured successfully.'
Write-Info 'You can now run Summon-Aunties.ps1 or Install-PerplexityXPC.ps1.'
Write-Host ''
Write-Host '  To remove these exclusions later, run:' -ForegroundColor DarkGray
Write-Host "    .\Add-DefenderExclusions.ps1 -Uninstall" -ForegroundColor DarkGray
Write-Host ''
