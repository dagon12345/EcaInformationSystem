$ServiceName = "EcaLocalSync"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service `"$ServiceName`" is not installed - nothing to do."
    Read-Host "Press Enter to close"
    exit 0
}

Write-Host "Stopping service..."
if ($existing.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}

Write-Host "Removing service..."
& sc.exe delete $ServiceName

Write-Host "Done."
Read-Host "Press Enter to close"
