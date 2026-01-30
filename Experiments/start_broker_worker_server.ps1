param(
    [string]$LogDir = "",
    [string]$DotNetPath = $env:DOTNET_PATH
)

$ErrorActionPreference = "Stop"

function Quote-Arg {
    param([string]$Arg)
    if ($Arg -match "\s") { return '"' + $Arg + '"' }
    return $Arg
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $LogDir) {
    $LogDir = Join-Path $repoRoot ("Experiments\server_logs\{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$dotnet = $DotNetPath
if (-not $dotnet) {
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCmd) {
        $dotnet = $dotnetCmd.Source
    }
}
if (-not $dotnet) {
    throw "dotnet not found. Install .NET 8 SDK or set DOTNET_PATH."
}

$workerProject = Join-Path $repoRoot "ServiceBroker\ServiceBroker.csproj"
$workerOutLog = Join-Path $LogDir "broker_worker.out.log"
$workerErrLog = Join-Path $LogDir "broker_worker.err.log"
$pidFile = Join-Path $LogDir "broker_worker.pid"
$lastPidFile = Join-Path $repoRoot "Experiments\server_logs\broker_worker.pid"

$env:WORKER_LOG_DIR = $LogDir

$workerArgs = @(
    "run",
    "--project", (Quote-Arg $workerProject)
)

$workerCmd = '"' + $dotnet + '" ' + ($workerArgs -join ' ')
Write-Host "Worker project: $workerProject"
Write-Host "Log dir: $LogDir"
Write-Host "Worker command: $workerCmd"

$workerProc = Start-Process -FilePath $dotnet -ArgumentList $workerArgs -PassThru -WorkingDirectory $repoRoot -RedirectStandardOutput $workerOutLog -RedirectStandardError $workerErrLog
Set-Content -Path $pidFile -Value $workerProc.Id -Encoding ASCII
Set-Content -Path $lastPidFile -Value $workerProc.Id -Encoding ASCII

Start-Sleep -Seconds 2
if ($workerProc.HasExited) {
    Write-Host "Worker exited early with code $($workerProc.ExitCode)."
    if (Test-Path $workerOutLog) {
        Write-Host "Last 50 stdout lines:"
        Get-Content -Path $workerOutLog -Tail 50 | ForEach-Object { Write-Host $_ }
    }
    if (Test-Path $workerErrLog) {
        Write-Host "Last 50 stderr lines:"
        Get-Content -Path $workerErrLog -Tail 50 | ForEach-Object { Write-Host $_ }
    }
    exit 1
}

Write-Host "Worker started. PID: $($workerProc.Id)"
Write-Host "PID file: $pidFile"
