$ErrorActionPreference = "Stop"

$root = $PSScriptRoot

# --- FTP connection settings ---
$clientFtpHost = "site76299.siteasp.net"
$clientFtpUser = "site76299"
$clientFtpPass = $env:MONSTERASP_CLIENT_FTP_PASS

if ([string]::IsNullOrWhiteSpace($clientFtpPass)) {
    throw "Missing FTP password. Set MONSTERASP_CLIENT_FTP_PASS before running."
}

# --- Auto-locate the actual .csproj file ---
$clientProject = Get-ChildItem -Path $root -Recurse -Filter "*.csproj" |
    Where-Object { $_.Name -match "Client" } | Select-Object -First 1

if (-not $clientProject) { throw "Could not find Client .csproj under $root" }

# --- Upload an entire folder in ONE persistent FTP session using lftp ---
function Upload-ToFtp {
    param(
        [string]$LocalFolder,
        [string]$FtpHostName,
        [string]$FtpUser,
        [string]$FtpPass
    )

    $lftpScript = @"
set ftp:ssl-allow no
set net:max-retries 3
set net:reconnect-interval-base 3
set net:timeout 30
open -u $FtpUser,$FtpPass ftp://$FtpHostName
mirror -R --parallel=4 --verbose --no-perms "$LocalFolder" /wwwroot
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

Write-Host "Publishing Client..." -ForegroundColor Cyan
dotnet publish "$($clientProject.FullName)" -c Release -o "$root/publish/client"

Write-Host "Uploading Client via FTP..." -ForegroundColor Cyan
Upload-ToFtp -LocalFolder "$root/publish/client" -FtpHostName $clientFtpHost -FtpUser $clientFtpUser -FtpPass $clientFtpPass

Write-Host "Done. Client deployed via FTP." -ForegroundColor Green