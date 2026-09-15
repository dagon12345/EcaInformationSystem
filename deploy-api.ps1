$ErrorActionPreference = "Stop"

$root = $PSScriptRoot

# --- SFTP connection settings ---
$apiFtpHost = "site76298.siteasp.net"
$apiFtpUser = "site76298"
$apiFtpPass = $env:MONSTERASP_API_FTP_PASS

if ([string]::IsNullOrWhiteSpace($apiFtpPass)) {
    throw "Missing FTP password. Set MONSTERASP_API_FTP_PASS before running."
}

# --- Auto-locate the actual .csproj file ---
$apiProject = Get-ChildItem -Path $root -Recurse -Filter "*.csproj" |
    Where-Object { $_.Name -match "Api" } | Select-Object -First 1

if (-not $apiProject) { throw "Could not find Api .csproj under $root" }

# --- Upload an entire folder in ONE persistent SFTP session using lftp ---
# ✅ CHANGED — EcaInformationSystem.Domain.dll got stuck on the server in a
# bad state once, and EVERY later mirror pass since then silently skipped
# re-uploading it — lftp's mirror only transfers files it thinks differ
# (by size/mtime), so once a bad copy's size happened to match, no amount
# of "fix the code and redeploy" ever actually re-sent that file. Deleting
# every managed assembly on the server first guarantees mirror sees them
# all as missing and does a full, unconditional re-upload every deploy —
# no diff-based skip can ever mask a repeat of this again.
function Remove-RemoteManagedAssemblies {
    param(
        [string]$LocalFolder,
        [string]$FtpHostName,
        [string]$FtpUser,
        [string]$FtpPass
    )

    $assemblies = Get-ChildItem -Path $LocalFolder -Include "*.dll", "*.pdb" -File
    $rmCommands = ($assemblies | ForEach-Object { "rm -f `"/wwwroot/$($_.Name)`"" }) -join "`n"

    $tempScript = New-TemporaryFile
    @"
set net:max-retries 3
set net:timeout 30
open -u $FtpUser,$FtpPass sftp://$FtpHostName
$rmCommands
bye
"@ | Out-File -FilePath $tempScript -Encoding utf8

    lftp -f $tempScript
    Remove-Item $tempScript -ErrorAction SilentlyContinue
    # No exit-code check — deleting a file that isn't there yet (first-ever
    # deploy) is expected to "fail" per-file; lftp keeps going regardless.
}

function Upload-ToSftp {
    param(
        [string]$LocalFolder,
        [string]$FtpHostName,
        [string]$FtpUser,
        [string]$FtpPass
    )

    $lftpScript = @"
set net:max-retries 3
set net:reconnect-interval-base 3
set net:timeout 30
open -u $FtpUser,$FtpPass sftp://$FtpHostName
mirror -R --parallel=2 --verbose --no-perms "$LocalFolder" /wwwroot
bye
"@

    $tempScriptFile = New-TemporaryFile
    $lftpScript | Out-File -FilePath $tempScriptFile -Encoding utf8

    lftp -f $tempScriptFile
    $exitCode = $LASTEXITCODE

    Remove-Item $tempScriptFile -ErrorAction SilentlyContinue

    if ($exitCode -ne 0) {
        throw "lftp mirror upload failed for $LocalFolder (exit code $exitCode)"
    }
}

# --- Toggle app_offline.htm to release/re-lock IIS file handles ---
function Set-AppOffline {
    param(
        [string]$FtpHostName,
        [string]$FtpUser,
        [string]$FtpPass,
        [bool]$Enable
    )

    if ($Enable) {
        $tempFile = New-TemporaryFile
        "App is being deployed, please check back shortly." | Out-File -FilePath $tempFile -Encoding utf8 -NoNewline
        $tempScript = New-TemporaryFile
        @"
set net:max-retries 3
set net:timeout 30
open -u $FtpUser,$FtpPass sftp://$FtpHostName
put "$tempFile" -o /wwwroot/app_offline.htm
bye
"@ | Out-File -FilePath $tempScript -Encoding utf8
        lftp -f $tempScript
        if ($LASTEXITCODE -ne 0) { throw "Failed to upload app_offline.htm (lftp exit code $LASTEXITCODE)" }
        Remove-Item $tempFile, $tempScript -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    else {
        $tempScript = New-TemporaryFile
        @"
set net:max-retries 3
set net:timeout 30
open -u $FtpUser,$FtpPass sftp://$FtpHostName
rm -f /wwwroot/app_offline.htm
bye
"@ | Out-File -FilePath $tempScript -Encoding utf8
        lftp -f $tempScript
        Remove-Item $tempScript -ErrorAction SilentlyContinue
    }
}

try {
    Write-Host "Publishing API..." -ForegroundColor Cyan
    dotnet publish "$($apiProject.FullName)" -c Release -o "$root/publish/api"

    Write-Host "Taking API offline for deployment..." -ForegroundColor Yellow
    Set-AppOffline -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass -Enable $true

    Write-Host "Clearing old assemblies to force a clean re-upload..." -ForegroundColor Yellow
    Remove-RemoteManagedAssemblies -LocalFolder "$root/publish/api" -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass

    Write-Host "Uploading API via SFTP..." -ForegroundColor Cyan
    Upload-ToSftp -LocalFolder "$root/publish/api" -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass
}
finally {
    Write-Host "Bringing API back online..." -ForegroundColor Yellow
    Set-AppOffline -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass -Enable $false
}

Write-Host "Done. API deployed via SFTP." -ForegroundColor Green