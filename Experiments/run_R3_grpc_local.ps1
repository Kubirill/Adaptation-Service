param(
    [int]$Trials = 3,
    [int]$Sessions = 10,
    [int]$WarmupSessions = 5,
    [int]$Seed = 1234,
    [string]$GrpcUrl = "http://127.0.0.1:6002",
    [string]$HealthUrl = "http://127.0.0.1:6003",
    [int]$ServiceTimeoutMs = 3000,
    [int]$ServiceRetries = 1,
    [int]$ServiceRetryDelayMs = 250,
    [string]$DotNetPath = $env:DOTNET_PATH
)

$ErrorActionPreference = "Stop"

function Quote-Arg {
    param([string]$Arg)
    if ($Arg -match "\s") { return '"' + $Arg + '"' }
    return $Arg
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$unityPath = $env:UNITY_PATH
$timestamp = (Get-Date -Format "yyyyMMdd_HHmmss")
$outRoot = Join-Path $repoRoot "Experiments\out\R3_grpc\$timestamp"

New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

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

for ($i = 0; $i -lt $Trials; $i++) {
    $trialDir = Join-Path $outRoot ("trial_{0:d4}" -f $i)
    New-Item -ItemType Directory -Force -Path $trialDir | Out-Null

    $serviceOutLog = Join-Path $trialDir "service_grpc.out.log"
    $serviceErrLog = Join-Path $trialDir "service_grpc.err.log"
    $serviceProject = Join-Path $repoRoot "ServiceGrpc\ServiceGrpc.csproj"
    $configRoot = Join-Path $repoRoot "Configs"

    $serviceArgs = @(
        "run",
        "--project", (Quote-Arg $serviceProject),
        "--",
        "--ConfigRoot", (Quote-Arg $configRoot),
        "--ConfigVersion", "v1",
        "--AuditRoot", (Quote-Arg $trialDir),
        "--GrpcUrl", $GrpcUrl,
        "--HealthUrl", $HealthUrl
    )

    $serviceCmd = '"' + $dotnet + '" ' + ($serviceArgs -join ' ')
    Write-Host "Starting ServiceGrpc for trial $i"
    Write-Host "Service command: $serviceCmd"
    $serviceProc = Start-Process -FilePath $dotnet -ArgumentList $serviceArgs -PassThru -RedirectStandardOutput $serviceOutLog -RedirectStandardError $serviceErrLog

    $healthEndpoint = $HealthUrl.TrimEnd('/') + "/health"
    $ready = $false
    for ($h = 0; $h -lt 30; $h++) {
        if ($serviceProc.HasExited) {
            Write-Host "ServiceGrpc exited before becoming ready. Exit code: $($serviceProc.ExitCode)"
            Write-Host "Service failed during startup; check service_grpc.out.log for build errors."
            if (Test-Path $serviceOutLog) {
                Write-Host "Last 50 stdout lines:"
                Get-Content -Path $serviceOutLog -Tail 50 | ForEach-Object { Write-Host $_ }
            }
            if (Test-Path $serviceErrLog) {
                Write-Host "Last 50 stderr lines:"
                Get-Content -Path $serviceErrLog -Tail 50 | ForEach-Object { Write-Host $_ }
            }
            break
        }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $healthEndpoint -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    if (-not $ready) {
        if ($serviceProc -and -not $serviceProc.HasExited) {
            Stop-Process -Id $serviceProc.Id -Force
        } else {
            Write-Host "ServiceGrpc already exited; skipping Stop-Process"
        }
        throw "ServiceGrpc did not become ready at $healthEndpoint"
    }

    & "$PSScriptRoot\run_common.ps1" -Arch "R3_grpc" -AdapterName "R3_remote_gRPC" -Trials 1 -Sessions $Sessions -WarmupSessions $WarmupSessions -Seed ($Seed + $i) -UnityPath $unityPath -ServiceGrpcUrl $GrpcUrl -ServiceTimeoutMs $ServiceTimeoutMs -ServiceRetries $ServiceRetries -ServiceRetryDelayMs $ServiceRetryDelayMs | Out-Null

    if ($serviceProc -and -not $serviceProc.HasExited) {
        Stop-Process -Id $serviceProc.Id -Force
    } else {
        Write-Host "ServiceGrpc already exited; skipping Stop-Process"
    }
}
