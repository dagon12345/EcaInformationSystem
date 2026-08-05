# Quick reachability check after install/start - a 401 means the service
# IS running correctly (the endpoint requires login, which is expected from
# a raw script call with no token). Anything else means something's wrong.

try {
    $r = Invoke-WebRequest -Uri "http://localhost:5010/api/dtr/test-connection" -Method POST -UseBasicParsing -TimeoutSec 5
    Write-Host "Responded with HTTP $($r.StatusCode)"
}
catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 401) {
            Write-Host "Responded with HTTP 401 Unauthorized - this means it IS running correctly."
        }
        else {
            Write-Host "Responded with HTTP $code - unexpected, but it is at least running."
        }
    }
    else {
        Write-Host "NOT RESPONDING - check Event Viewer > Windows Logs > Application for EcaLocalSync errors."
    }
}
