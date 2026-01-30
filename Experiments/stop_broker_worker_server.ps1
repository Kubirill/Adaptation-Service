param(
    [string]$PidFile = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $PidFile) {
    $PidFile = Join-Path $repoRoot "Experiments\server_logs\broker_worker.pid"
}

if (-not (Test-Path $PidFile)) {
    Write-Host "PID file not found: $PidFile"
    exit 0
}

$pidText = Get-Content -Path $PidFile -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $pidText) {
    Write-Host "PID file empty: $PidFile"
    exit 0
}

if (-not [int]::TryParse($pidText, [ref]$pid)) {
    Write-Host "Invalid PID in file: $PidFile"
    exit 0
}

try {
    $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
    if ($proc) {
        Stop-Process -Id $pid -Force
        Write-Host "Stopped worker PID $pid."
    } else {
        Write-Host "Worker PID $pid not running."
    }
} catch {
    Write-Host "Failed to stop PID $pid: $($_.Exception.Message)"
}
