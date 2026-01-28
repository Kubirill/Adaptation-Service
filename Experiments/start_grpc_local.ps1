param(
    [string]$ConfigVersion = "v1",
    [string]$ConfigRoot = "",
    [string]$AuditRoot = "",
    [string]$GrpcUrl = "http://127.0.0.1:6002",
    [string]$HealthUrl = "http://127.0.0.1:6003",
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
if (-not $ConfigRoot) {
    $ConfigRoot = Join-Path $repoRoot "Configs"
}

if ($AuditRoot) {
    New-Item -ItemType Directory -Force -Path $AuditRoot | Out-Null
}

$serviceProject = Join-Path $repoRoot "ServiceGrpc\ServiceGrpc.csproj"
if (-not $LogDir) {
    $LogDir = Join-Path $repoRoot ("Experiments\out\service_grpc\{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
 
$serviceOutLog = Join-Path $LogDir "service_grpc.out.log"
$serviceErrLog = Join-Path $LogDir "service_grpc.err.log"

Write-Host "ServiceGrpc project: $serviceProject"
Write-Host "Config root: $ConfigRoot"
Write-Host "Config version: $ConfigVersion"
Write-Host "Audit root: $AuditRoot"
Write-Host "Grpc URL: $GrpcUrl"
Write-Host "Health URL: $HealthUrl"
Write-Host "Log dir: $LogDir"

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

$serviceArgs = @(
    "run",
    "--project", (Quote-Arg $serviceProject),
    "--",
    "--ConfigRoot", (Quote-Arg $ConfigRoot),
    "--ConfigVersion", $ConfigVersion,
    "--GrpcUrl", $GrpcUrl,
    "--HealthUrl", $HealthUrl
)
if ($AuditRoot) {
    $serviceArgs += @("--AuditRoot", (Quote-Arg $AuditRoot))
}

$serviceCmd = '"' + $dotnet + '" ' + ($serviceArgs -join ' ')
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
        exit 1
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

Write-Host "ServiceGrpc ready. PID: $($serviceProc.Id)"
