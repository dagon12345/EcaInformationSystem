$ErrorActionPreference = "Stop"

# $PSScriptRoot should always be set when run via -File, but fall back to
# deriving it from the invocation path just in case something unusual
# about how this got launched leaves it empty.
$InstallDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$ExePath = Join-Path $InstallDir "EcaInformationSystem.Api.exe"
$PatchScript = Join-Path $InstallDir "patch-appsettings.ps1"
$CheckScript = Join-Path $InstallDir "check-service.ps1"
$ServiceName = "EcaLocalSync"

Write-Host ""
Write-Host "============================================================"
Write-Host " ECA-InFORMS Biometric Local Sync - Service Installer"
Write-Host "============================================================"
Write-Host " Install folder: $InstallDir"
Write-Host ""

if (-not (Test-Path -LiteralPath $ExePath)) {
    Write-Host "ERROR: could not find EcaInformationSystem.Api.exe"
    Write-Host "  Looked for: $ExePath"
    Write-Host "Make sure this script sits in the SAME folder as EcaInformationSystem.Api.exe"
    Write-Host "(the dotnet publish output folder)."
    Read-Host "Press Enter to exit"
    exit 1
}

if (-not (Test-Path -LiteralPath $PatchScript)) {
    Write-Host "ERROR: could not find patch-appsettings.ps1"
    Write-Host "  Looked for: $PatchScript"
    Write-Host "Make sure patch-appsettings.ps1 was copied into this same folder."
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Applying required settings to appsettings.json..."
Write-Host "  Running: $PatchScript"
try {
    & "$PatchScript"
}
catch {
    Write-Host "ERROR: Failed to update appsettings.json - $($_.Exception.Message)"
    Read-Host "Press Enter to exit"
    exit 1
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping and removing existing service..."
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Installing service..."
# Passing binPath as separate array elements to sc.exe avoids all manual
# quote-escaping - PowerShell's call operator (&) builds the child process
# command line correctly regardless of spaces in $ExePath.
$binPath = "`"$ExePath`" --urls=http://localhost:5010"
& sc.exe create $ServiceName binPath= $binPath start= auto | Out-Null
& sc.exe description $ServiceName "ECA-InFORMS biometric device local sync bridge" | Out-Null

Write-Host "Starting service..."
Start-Service -Name $ServiceName

Start-Sleep -Seconds 3

Write-Host ""
Write-Host "-- Checking it's actually up ----------------------------------"
if (Test-Path -LiteralPath $CheckScript) {
    & "$CheckScript"
}
else {
    Write-Host "(check-service.ps1 not found next to this script - skipping the check)"
}

Write-Host ""
Write-Host "============================================================"
Write-Host " Done. The service will now start automatically every time"
Write-Host " this PC boots - nothing else to run, ever, on this machine."
Write-Host "============================================================"
Read-Host "Press Enter to close"
