#Requires -Version 5.1
<#
.SYNOPSIS
    Creates a self-signed code-signing certificate and optionally signs all
    PerplexityXPC PowerShell scripts with it.

.DESCRIPTION
    Unsigned PowerShell scripts downloaded from the internet can trigger
    Windows Defender warnings and ExecutionPolicy blocks even when they are
    not malicious. A code-signing certificate makes scripts look legitimate
    to Windows and eliminates "untrusted script" warnings.

    This script:
      1. Creates a self-signed code-signing certificate in Cert:\CurrentUser\My.
      2. Optionally exports the public (.cer) certificate for distribution
         or import into other machines.
      3. Optionally signs all .ps1, .psm1, and .psd1 files in the project
         with the certificate (requires -SignScripts).
      4. Optionally adds the certificate to the Trusted Publishers store so
         signed scripts run without user prompts (requires -TrustCert and
         Admin elevation).

    NOTE: A self-signed certificate is trusted only on the machine where it
    was created (or machines where you import the .cer). For enterprise
    deployment use a certificate from an internal PKI or a commercial CA.

.PARAMETER CertName
    Common Name (CN) for the certificate.
    Defaults to "PerplexityXPC Code Signing".

.PARAMETER SignScripts
    Sign all .ps1, .psm1, and .psd1 files found under the project root
    using the new (or existing) certificate.

.PARAMETER TrustCert
    Add the certificate to Cert:\LocalMachine\TrustedPublisher and
    Cert:\LocalMachine\Root so signed scripts run without prompts.
    Requires Administrator privileges.

.PARAMETER ExportPath
    Path (including filename) where the public .cer file will be exported.
    Example: "C:\Users\You\Desktop\PerplexityXPC-CodeSigning.cer"
    Defaults to the project root (parent of scripts\) as
    "PerplexityXPC-CodeSigning.cer".

.EXAMPLE
    .\New-CodeSigningCert.ps1

.EXAMPLE
    .\New-CodeSigningCert.ps1 -SignScripts -TrustCert

.EXAMPLE
    .\New-CodeSigningCert.ps1 -CertName "My Company Code Signing" -SignScripts -ExportPath "C:\Share\cert.cer"

.NOTES
    Compatible with PowerShell 5.1 and PowerShell 7+.
    -TrustCert requires Administrator elevation.
    The self-signed cert expires in 5 years.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(HelpMessage = "Certificate common name.")]
    [string]$CertName = 'PerplexityXPC Code Signing',

    [Parameter(HelpMessage = "Sign all .ps1/.psm1/.psd1 files in the project with the cert.")]
    [switch]$SignScripts,

    [Parameter(HelpMessage = "Add cert to TrustedPublisher and Root stores (requires Admin).")]
    [switch]$TrustCert,

    [Parameter(HelpMessage = "Export path for the public .cer file.")]
    [string]$ExportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
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

function Test-IsAdmin {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
$scriptDir  = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$projectRoot = Split-Path -Parent $scriptDir
if (-not (Test-Path $projectRoot)) { $projectRoot = $scriptDir }

if ([string]::IsNullOrEmpty($ExportPath)) {
    $ExportPath = Join-Path $projectRoot 'PerplexityXPC-CodeSigning.cer'
}

# ---------------------------------------------------------------------------
# Step 1 - Create or reuse certificate
# ---------------------------------------------------------------------------
Write-Head 'Step 1 - Code Signing Certificate'

$existingCert = Get-ChildItem 'Cert:\CurrentUser\My' -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq "CN=$CertName" } |
    Select-Object -First 1

if ($existingCert) {
    Write-Info "Existing certificate found: $($existingCert.Thumbprint)"
    Write-Info "Subject: $($existingCert.Subject)"
    Write-Info "Expires: $($existingCert.NotAfter)"
    $cert = $existingCert
} else {
    Write-Info "Creating new self-signed code-signing certificate: CN=$CertName"
    try {
        # New-SelfSignedCertificate is available in PS 5.1+ via PKI module
        $certParams = @{
            Subject           = "CN=$CertName"
            Type              = 'CodeSigningCert'
            CertStoreLocation = 'Cert:\CurrentUser\My'
            NotAfter          = (Get-Date).AddYears(5)
            HashAlgorithm     = 'SHA256'
            KeyLength         = 2048
            KeyUsage          = 'DigitalSignature'
        }
        $cert = New-SelfSignedCertificate @certParams -ErrorAction Stop
        Write-Ok "Certificate created: $($cert.Thumbprint)"
        Write-Info "Stored in: Cert:\CurrentUser\My"
    } catch {
        Write-Err "Failed to create certificate: $_"
        exit 1
    }
}

# ---------------------------------------------------------------------------
# Step 2 - Export public certificate
# ---------------------------------------------------------------------------
Write-Head 'Step 2 - Export Public Certificate'
try {
    if ($PSCmdlet.ShouldProcess($ExportPath, 'Export public certificate (.cer)')) {
        $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        [System.IO.File]::WriteAllBytes($ExportPath, $certBytes)
        Write-Ok "Public certificate exported to: $ExportPath"
        Write-Info 'Distribute this .cer file to other machines and import into their'
        Write-Info 'Trusted Publishers store to make signed scripts run without prompts.'
    }
} catch {
    Write-Warn "Could not export certificate: $_"
}

# ---------------------------------------------------------------------------
# Step 3 - Add to Trusted Publishers (optional, requires Admin)
# ---------------------------------------------------------------------------
if ($TrustCert) {
    Write-Head 'Step 3 - Trust Certificate'
    if (-not (Test-IsAdmin)) {
        Write-Err '-TrustCert requires Administrator privileges. Re-run elevated or add the cert manually.'
        Write-Info "Import $ExportPath into Cert:\LocalMachine\TrustedPublisher and Cert:\LocalMachine\Root"
    } else {
        $stores = @('TrustedPublisher', 'Root')
        foreach ($storeName in $stores) {
            try {
                if ($PSCmdlet.ShouldProcess("Cert:\LocalMachine\$storeName", "Add certificate")) {
                    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'LocalMachine')
                    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
                    $store.Add($cert)
                    $store.Close()
                    Write-Ok "Added to Cert:\LocalMachine\$storeName"
                }
            } catch {
                Write-Warn "Could not add to $storeName store: $_"
            }
        }
    }
} else {
    Write-Head 'Step 3 - Trust Certificate'
    Write-Info 'Skipping (use -TrustCert to add cert to TrustedPublisher store).'
}

# ---------------------------------------------------------------------------
# Step 4 - Sign scripts (optional)
# ---------------------------------------------------------------------------
if ($SignScripts) {
    Write-Head 'Step 4 - Sign PowerShell Scripts'
    Write-Info "Searching for scripts under: $projectRoot"

    $scriptExtensions = @('.ps1', '.psm1', '.psd1')
    try {
        $filesToSign = Get-ChildItem -Path $projectRoot -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $scriptExtensions -contains $_.Extension.ToLower() }
    } catch {
        Write-Err "Failed to enumerate scripts: $_"
        $filesToSign = @()
    }

    if ($null -eq $filesToSign -or @($filesToSign).Count -eq 0) {
        Write-Warn 'No .ps1/.psm1/.psd1 files found to sign.'
    } else {
        $signedCount = 0
        $errorCount  = 0

        foreach ($file in $filesToSign) {
            try {
                if ($PSCmdlet.ShouldProcess($file.FullName, 'Set-AuthenticodeSignature')) {
                    $result = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert `
                        -TimestampServer 'http://timestamp.digicert.com' -ErrorAction Stop
                    if ($result.Status -eq 'Valid') {
                        Write-Ok "Signed: $($file.FullName)"
                        $signedCount++
                    } else {
                        Write-Warn "Signature status '$($result.Status)': $($file.FullName)"
                        $errorCount++
                    }
                }
            } catch {
                # Timestamp server may be unavailable - retry without timestamp
                try {
                    $result = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert -ErrorAction Stop
                    if ($result.Status -eq 'Valid') {
                        Write-Ok "Signed (no timestamp): $($file.FullName)"
                        $signedCount++
                    } else {
                        Write-Warn "Could not sign '$($file.FullName)': status=$($result.Status)"
                        $errorCount++
                    }
                } catch {
                    Write-Err "Failed to sign '$($file.FullName)': $_"
                    $errorCount++
                }
            }
        }

        Write-Host ''
        Write-Ok "Signed $signedCount file(s)."
        if ($errorCount -gt 0) {
            Write-Warn "$errorCount file(s) could not be signed."
        }
    }
} else {
    Write-Head 'Step 4 - Sign PowerShell Scripts'
    Write-Info 'Skipping (use -SignScripts to sign all .ps1/.psm1/.psd1 files).'
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '  --------------------------------------------------------' -ForegroundColor DarkGray
Write-Ok 'Code signing setup complete.'
Write-Host ''
Write-Host '  Certificate thumbprint : ' -NoNewline -ForegroundColor Gray
Write-Host $cert.Thumbprint -ForegroundColor White
Write-Host '  Certificate expires    : ' -NoNewline -ForegroundColor Gray
Write-Host $cert.NotAfter -ForegroundColor White
Write-Host '  Public cert exported   : ' -NoNewline -ForegroundColor Gray
Write-Host $ExportPath -ForegroundColor White
Write-Host ''
if (-not $SignScripts) {
    Write-Host '  To sign all scripts now, re-run with:' -ForegroundColor DarkGray
    Write-Host "    .\New-CodeSigningCert.ps1 -SignScripts" -ForegroundColor DarkGray
}
if (-not $TrustCert) {
    Write-Host '  To trust signed scripts on this machine without prompts, re-run with:' -ForegroundColor DarkGray
    Write-Host "    .\New-CodeSigningCert.ps1 -TrustCert  (requires Admin)" -ForegroundColor DarkGray
}
Write-Host ''
