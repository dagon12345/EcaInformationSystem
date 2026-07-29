$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$markerFile = Join-Path $root ".last-deploy-client"

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

# --- Determine which folders count as "Client layer" for change detection ---
# Adjust this list if other shared projects should also trigger a Client deploy.
$clientRelevantPaths = @(
    "EcaInformationSystem.Client",
    "EcaInformationSystem.Shared",
    "EcaInformationSystem.Common"
)

$currentCommit = (git -C $root rev-parse HEAD).Trim()

$shouldDeploy = $true

if (Test-Path $markerFile) {
    $lastCommit = (Get-Content $markerFile -Raw).Trim()

    if ($lastCommit -eq $currentCommit) {
        Write-Host "No new commits since last Client deploy ($lastCommit). Skipping." -ForegroundColor Yellow
        $shouldDeploy = $false
    }
    else {
        $changedFiles = git -C $root diff --name-only $lastCommit $currentCommit

        $touchesClient = $false
        foreach ($file in $changedFiles) {
            foreach ($path in $clientRelevantPaths) {
                if ($file -like "$path/*") {
                    $touchesClient = $true
                    break
                }
            }
            if ($touchesClient) { break }
        }

        if (-not $touchesClient) {
            Write-Host "Commits since last Client deploy don't touch Client-relevant folders. Skipping." -ForegroundColor Yellow
            $shouldDeploy = $false
        }
    }
}

if (-not $shouldDeploy) {
    Write-Host "Nothing to deploy for Client." -ForegroundColor Green
    exit 0
}

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

$currentCommit | Out-File -FilePath $markerFile -Encoding utf8 -NoNewline
Write-Host "Done. Client deployed via FTP. Marker updated to $currentCommit." -ForegroundColor Green