param(
    [int]$Trials = 3,
    [int]$Sessions = 10,
    [int]$Seed = 1234,
    [string]$ServiceUrl = "http://localhost:5000",
    [int]$ServiceTimeoutMs = 3000,
    [int]$ServiceRetries = 2,
    [int]$ServiceRetryDelayMs = 250,
    [string]$ProfileId = "",
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
$outRoot = Join-Path $repoRoot "Experiments\out\B2\$timestamp"

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

    $serviceOutLog = Join-Path $trialDir "service.out.log"
    $serviceErrLog = Join-Path $trialDir "service.err.log"
    $serviceProject = Join-Path $repoRoot "Service\Service.csproj"
    $configRoot = Join-Path $repoRoot "Configs"

    $serviceArgs = @(
        "run",
        "--project", (Quote-Arg $serviceProject),
        "--",
        "--ConfigRoot", (Quote-Arg $configRoot),
        "--ConfigVersion", "v1",
        "--AuditRoot", (Quote-Arg $trialDir),
        "--urls", $ServiceUrl
    )

    $serviceCmd = '"' + $dotnet + '" ' + ($serviceArgs -join ' ')
    Write-Host "Starting service for trial $i"
    Write-Host "Service command: $serviceCmd"
    $serviceProc = Start-Process -FilePath $dotnet -ArgumentList $serviceArgs -PassThru -RedirectStandardOutput $serviceOutLog -RedirectStandardError $serviceErrLog

    $healthUrl = $ServiceUrl.TrimEnd('/') + "/health"
    $ready = $false
    for ($h = 0; $h -lt 30; $h++) {
        if ($serviceProc.HasExited) {
            Write-Host "Service exited before becoming ready. Exit code: $($serviceProc.ExitCode)"
            if (Test-Path $serviceErrLog) {
                Write-Host "Last 50 stderr lines:"
                Get-Content -Path $serviceErrLog -Tail 50 | ForEach-Object { Write-Host $_ }
            }
            break
        }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 2
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
            Write-Host "Service already exited; skipping Stop-Process"
        }
        throw "Service did not become ready at $healthUrl"
    }

    & "$PSScriptRoot\run_common.ps1" -Arch "B2" -Trials 1 -Sessions $Sessions -Seed ($Seed + $i) -UnityPath $unityPath -ServiceUrl $ServiceUrl -ServiceTimeoutMs $ServiceTimeoutMs -ServiceRetries $ServiceRetries -ServiceRetryDelayMs $ServiceRetryDelayMs -ProfileId $ProfileId | Out-Null

    if ($serviceProc -and -not $serviceProc.HasExited) {
        Stop-Process -Id $serviceProc.Id -Force
    } else {
        Write-Host "Service already exited; skipping Stop-Process"
    }
}
