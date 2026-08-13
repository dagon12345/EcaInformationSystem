$ErrorActionPreference = "Stop"

$root = $PSScriptRoot

# --- SFTP connection settings ---
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

# --- Upload an entire folder in ONE persistent SFTP session using lftp ---
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

# ✅ FIXED — upload publish/client/wwwroot directly, not the whole publish
# output. dotnet publish's own top-level folder carries an SDK-generated
# web.config (no cache rules, a bare rewrite-to-wwwroot rule) that used to
# land ABOVE the hand-authored wwwroot/web.config (which correctly sets
# no-cache on index.html/blazor.boot.json and immutable on the hashed
# _framework files) — an unnecessary extra hop that made cache headers
# unreliable and was the likely cause of deployed updates needing a hard
# refresh (Ctrl+Shift+R) instead of a plain one to show up. This app is
# served as pure static content (client-side Blazor WASM), so only the
# wwwroot contents ever need to be deployed.
Write-Host "Uploading Client via SFTP..." -ForegroundColor Cyan
Upload-ToSftp -LocalFolder "$root/publish/client/wwwroot" -FtpHostName $clientFtpHost -FtpUser $clientFtpUser -FtpPass $clientFtpPass

Write-Host "Done. Client deployed via SFTP." -ForegroundColor Green