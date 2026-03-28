#Requires -Version 5.1
<#
.SYNOPSIS
    Removes the Mark of the Web (MOTW) Zone.Identifier from all PerplexityXPC files.

.DESCRIPTION
    When a ZIP or installer is downloaded from the internet, Windows attaches a
    Zone.Identifier Alternate Data Stream (ADS) to every extracted file. This
    "Mark of the Web" causes PowerShell to block script execution, prompts
    "Do you want to run this software?" dialogs for executables, and triggers
    additional Windows Defender scrutiny.

    This script uses Unblock-File to strip the Zone.Identifier ADS from all
    relevant files in the PerplexityXPC project directory. Run it FIRST, before
    any other script in this repository.

    It targets:
      - .ps1  (PowerShell scripts)
      - .psm1 (PowerShell modules)
      - .psd1 (PowerShell module manifests)
      - .exe  (Executables)
      - .dll  (Libraries)
      - .cmd  (Batch launchers)

.PARAMETER Path
    Root directory to unblock. Defaults to the directory containing this script
    (i.e., the project root when run from setup media).

.EXAMPLE
    .\scripts\Unblock-PerplexityXPC.ps1

.EXAMPLE
    .\scripts\Unblock-PerplexityXPC.ps1 -Path "C:\Downloads\perplexity-windows"

.NOTES
    Does not require Administrator privileges - Unblock-File operates on files
    owned by the current user.
    Compatible with PowerShell 5.1 and PowerShell 7+.
    Safe to run multiple times (idempotent).
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(HelpMessage = "Root directory to unblock. Default: project root (parent of scripts\).")]
    [string]$Path = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

# ---------------------------------------------------------------------------
# Resolve root path
# ---------------------------------------------------------------------------
if ([string]::IsNullOrEmpty($Path)) {
    # Default: parent of the scripts\ folder (project root)
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
    $parentDir = Split-Path -Parent $scriptDir
    # If we cannot resolve a parent, fall back to scriptDir itself
    $Path = if ($parentDir -and (Test-Path $parentDir)) { $parentDir } else { $scriptDir }
}

if (-not (Test-Path $Path)) {
    Write-Host "  [ERR] Path not found: $Path" -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '  PerplexityXPC - Remove Mark of the Web (MOTW)' -ForegroundColor Cyan
Write-Host '  -----------------------------------------------' -ForegroundColor DarkGray
Write-Host "  Root: $Path" -ForegroundColor Gray
Write-Host ''

# ---------------------------------------------------------------------------
# Target extensions
# ---------------------------------------------------------------------------
$targetExtensions = @('.ps1', '.psm1', '.psd1', '.exe', '.dll', '.cmd')

$totalFound    = 0
$totalUnblocked = 0
$totalSkipped  = 0
$totalErrors   = 0

# ---------------------------------------------------------------------------
# Collect files to process
# ---------------------------------------------------------------------------
try {
    $allFiles = Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $targetExtensions -contains $_.Extension.ToLower() }
} catch {
    Write-Host "  [ERR] Failed to enumerate files in '$Path': $_" -ForegroundColor Red
    exit 1
}

if ($null -eq $allFiles -or @($allFiles).Count -eq 0) {
    Write-Host '  [WARN] No target files found.' -ForegroundColor Yellow
    exit 0
}

$totalFound = @($allFiles).Count
Write-Host "  Found $totalFound file(s) to inspect." -ForegroundColor Gray
Write-Host ''

# ---------------------------------------------------------------------------
# Check for Zone.Identifier and unblock
# ---------------------------------------------------------------------------
foreach ($file in $allFiles) {
    # Check if Zone.Identifier ADS exists
    $hasMotw = $false
    try {
        $ads = Get-Item -LiteralPath $file.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
        $hasMotw = $null -ne $ads
    } catch {
        # Stream not present or access denied - treat as no MOTW
        $hasMotw = $false
    }

    if (-not $hasMotw) {
        $totalSkipped++
        continue
    }

    # File has MOTW - unblock it
    try {
        if ($PSCmdlet.ShouldProcess($file.FullName, 'Unblock-File (remove Zone.Identifier)')) {
            Unblock-File -LiteralPath $file.FullName -ErrorAction Stop
            Write-Host "  [OK] Unblocked: $($file.FullName)" -ForegroundColor Green
            $totalUnblocked++
        }
    } catch {
        Write-Host "  [ERR] Could not unblock '$($file.FullName)': $_" -ForegroundColor Red
        $totalErrors++
    }
}

# ---------------------------------------------------------------------------
# Also run a catch-all Unblock-File on the entire tree to catch any
# file types we did not enumerate explicitly
# ---------------------------------------------------------------------------
try {
    if ($PSCmdlet.ShouldProcess($Path, 'Unblock-File (catch-all recursive pass)')) {
        Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue |
            Unblock-File -ErrorAction SilentlyContinue
    }
} catch {
    Write-Host "  [WARN] Catch-all unblock pass encountered an error: $_" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '  -----------------------------------------------' -ForegroundColor DarkGray
if ($totalErrors -gt 0) {
    Write-Host "  [WARN] Completed with errors." -ForegroundColor Yellow
} else {
    Write-Host "  [OK]   Completed successfully." -ForegroundColor Green
}
Write-Host "         Files found    : $totalFound" -ForegroundColor Gray
Write-Host "         Unblocked      : $totalUnblocked" -ForegroundColor Gray
Write-Host "         Already clean  : $totalSkipped" -ForegroundColor Gray
if ($totalErrors -gt 0) {
    Write-Host "         Errors         : $totalErrors" -ForegroundColor Red
}
Write-Host ''
Write-Host '  You can now run Summon-Aunties.ps1 or Setup.cmd.' -ForegroundColor Cyan
Write-Host ''
