@echo off
setlocal

echo.
echo ==========================================
echo    PerplexityXPC - Setup Launcher
echo ==========================================
echo.
echo This launcher handles Windows Defender and MOTW compatibility
echo before starting the PowerShell setup wizard.
echo.

:: -------------------------------------------------------------------
:: Step 1 - Unblock all downloaded files
:: (Removes Zone.Identifier ADS added when ZIP was downloaded)
:: -------------------------------------------------------------------
echo [1/2] Unblocking downloaded files...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "Get-ChildItem -Path '%~dp0' -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue; Write-Host '  Done.' -ForegroundColor Green"

echo.

:: -------------------------------------------------------------------
:: Step 2 - Launch the PowerShell setup wizard
:: The wizard handles Defender exclusions as its first step.
:: -------------------------------------------------------------------
echo [2/2] Launching setup wizard...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Summon-Aunties.ps1" %*

echo.
if %ERRORLEVEL% NEQ 0 (
    echo   Setup exited with error code %ERRORLEVEL%.
    echo   Review the output above for details.
) else (
    echo   Setup completed.
)

echo.
pause
endlocal
