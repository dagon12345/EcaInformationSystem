$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$markerFile = Join-Path $root ".last-deploy-api"

# --- FTP connection settings ---
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

# --- Determine which folders count as "API layer" for change detection ---
# Adjust this list if other shared projects should also trigger an API deploy.
$apiRelevantPaths = @(
    "EcaInformationSystem.Api",
    "EcaInformationSystem.Application",
    "EcaInformationSystem.Domain",
    "EcaInformationSystem.Infrastructure",
    "EcaInformationSystem.Common",
    "EcaInformationSystem.Shared"
)

$currentCommit = (git -C $root rev-parse HEAD).Trim()

$shouldDeploy = $true

if (Test-Path $markerFile) {
    $lastCommit = (Get-Content $markerFile -Raw).Trim()

    if ($lastCommit -eq $currentCommit) {
        Write-Host "No new commits since last API deploy ($lastCommit). Skipping." -ForegroundColor Yellow
        $shouldDeploy = $false
    }
    else {
        $changedFiles = git -C $root diff --name-only $lastCommit $currentCommit

        $touchesApi = $false
        foreach ($file in $changedFiles) {
            foreach ($path in $apiRelevantPaths) {
                if ($file -like "$path/*") {
                    $touchesApi = $true
                    break
                }
            }
            if ($touchesApi) { break }
        }

        if (-not $touchesApi) {
            Write-Host "Commits since last API deploy don't touch API-relevant folders. Skipping." -ForegroundColor Yellow
            $shouldDeploy = $false
        }
    }
}

if (-not $shouldDeploy) {
    Write-Host "Nothing to deploy for API." -ForegroundColor Green
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
        $remoteUrl = "ftp://$FtpHostName/wwwroot/app_offline.htm"
        curl --ftp-create-dirs -T "$tempFile" "$remoteUrl" --user "${FtpUser}:${FtpPass}" --disable-epsv --retry 3 --retry-delay 3 --silent --show-error
        if ($LASTEXITCODE -ne 0) { throw "Failed to upload app_offline.htm (curl exit code $LASTEXITCODE)" }
        Remove-Item $tempFile -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    else {
        curl -Q "-DELE /wwwroot/app_offline.htm" "ftp://$FtpHostName/wwwroot/" --user "${FtpUser}:${FtpPass}" --disable-epsv --silent --show-error --output /dev/null
    }
}

try {
    Write-Host "Publishing API..." -ForegroundColor Cyan
    dotnet publish "$($apiProject.FullName)" -c Release -o "$root/publish/api"

    Write-Host "Taking API offline for deployment..." -ForegroundColor Yellow
    Set-AppOffline -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass -Enable $true

    Write-Host "Uploading API via FTP..." -ForegroundColor Cyan
    Upload-ToFtp -LocalFolder "$root/publish/api" -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass
}
finally {
    Write-Host "Bringing API back online..." -ForegroundColor Yellow
    Set-AppOffline -FtpHostName $apiFtpHost -FtpUser $apiFtpUser -FtpPass $apiFtpPass -Enable $false
}

$currentCommit | Out-File -FilePath $markerFile -Encoding utf8 -NoNewline
Write-Host "Done. API deployed via FTP. Marker updated to $currentCommit." -ForegroundColor Green