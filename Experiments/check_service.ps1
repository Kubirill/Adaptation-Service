param(
    [string]$ConfigVersion = "v1",
    [string]$ConfigRoot = "",
    [string]$AuditRoot = "",
    [string]$ServiceUrl = "http://localhost:5000",
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

if (-not $LogDir) {
    $LogDir = Join-Path $repoRoot ("Experiments\out\check_service\{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

if ($AuditRoot) {
    New-Item -ItemType Directory -Force -Path $AuditRoot | Out-Null
}

$serviceProject = Join-Path $repoRoot "Service\Service.csproj"
$serviceOutLog = Join-Path $LogDir "service.out.log"
$serviceErrLog = Join-Path $LogDir "service.err.log"

Write-Host "Service project: $serviceProject"
Write-Host "Config root: $ConfigRoot"
Write-Host "Config version: $ConfigVersion"
Write-Host "Audit root: $AuditRoot"
Write-Host "Service URL: $ServiceUrl"
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
    "--ConfigVersion", $ConfigVersion
)
if ($AuditRoot) {
    $serviceArgs += @("--AuditRoot", (Quote-Arg $AuditRoot))
}
$serviceArgs += @("--urls", $ServiceUrl)

$serviceCmd = '"' + $dotnet + '" ' + ($serviceArgs -join ' ')
Write-Host "Service command: $serviceCmd"

$serviceProc = Start-Process -FilePath $dotnet -ArgumentList $serviceArgs -PassThru -RedirectStandardOutput $serviceOutLog -RedirectStandardError $serviceErrLog

$healthUrl = $ServiceUrl.TrimEnd('/') + "/health"
$ready = $false
for ($h = 0; $h -lt 30; $h++) {
    if ($serviceProc.HasExited) {
        Write-Host "Service exited before becoming ready. Exit code: $($serviceProc.ExitCode)"
        Write-Host "Service failed during startup; check service.out.log for build errors."
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

$healthResponse = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 5
Write-Host "Health status: $($healthResponse.StatusCode) $($healthResponse.Content)"

if ($serviceProc -and -not $serviceProc.HasExited) {
    Stop-Process -Id $serviceProc.Id
    $serviceProc.WaitForExit(5000) | Out-Null
    if (-not $serviceProc.HasExited) {
        Stop-Process -Id $serviceProc.Id -Force
    }
} else {
    Write-Host "Service already exited; skipping Stop-Process"
}
