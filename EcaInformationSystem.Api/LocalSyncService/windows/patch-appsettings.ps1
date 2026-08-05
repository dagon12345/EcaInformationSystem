# Bakes the settings the LocalSync service needs directly into
# appsettings.json in this same folder. Idempotent - safe to run every
# time install-service.ps1 runs (e.g. after republishing).

$ErrorActionPreference = "Stop"
$path = Join-Path $PSScriptRoot "appsettings.json"

$json = Get-Content $path -Raw | ConvertFrom-Json

$json | Add-Member -NotePropertyName SyncOnlyMode -NotePropertyValue $true -Force

if (-not $json.ZkDirect) {
    $json | Add-Member -NotePropertyName ZkDirect -NotePropertyValue (New-Object PSObject) -Force
}

$json.ZkDirect | Add-Member -NotePropertyName Enabled -NotePropertyValue $true -Force
$json.ZkDirect | Add-Member -NotePropertyName RegionCode -NotePropertyValue 1600000000 -Force
$json.ZkDirect | Add-Member -NotePropertyName CommKey -NotePropertyValue 0 -Force
$json.ZkDirect | Add-Member -NotePropertyName PollIntervalSeconds -NotePropertyValue 60 -Force
$json.ZkDirect | Add-Member -NotePropertyName TimeoutMs -NotePropertyValue 20000 -Force

$json | ConvertTo-Json -Depth 10 | Set-Content $path

Write-Host "appsettings.json updated (SyncOnlyMode + ZkDirect enabled)."
