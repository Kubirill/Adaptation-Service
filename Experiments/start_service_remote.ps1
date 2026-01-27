param(
    [string]$ConfigVersion = "v1",
    [string]$ConfigRoot = "",
    [string]$AuditRoot = "",
    [string]$Urls = "",
    [string]$DotNetPath = $env:DOTNET_PATH
)

$ErrorActionPreference = "Stop"

$servicePortEnv = $env:SERVICE_PORT
$servicePort = 0
if ($servicePortEnv) {
    [int]::TryParse($servicePortEnv, [ref]$servicePort) | Out-Null
}
if (-not $Urls) {
    if (-not $servicePort) {
        $servicePort = 5000
    }
    $Urls = "http://0.0.0.0:$servicePort"
}

function Quote-Arg {
    param([string]$Arg)
    if ($Arg -match "\s") { return '"' + $Arg + '"' }
    return $Arg
}

function Get-PrivateIPv4 {
    $candidates = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -match "^10\." -or
            $_.IPAddress -match "^192\.168\." -or
            $_.IPAddress -match "^172\.(1[6-9]|2\d|3[0-1])\."
        } |
        Sort-Object -Property InterfaceMetric, IPAddress
    return ($candidates | Select-Object -First 1 -ExpandProperty IPAddress)
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $ConfigRoot) {
    $ConfigRoot = Join-Path $repoRoot "Configs"
}
if ($AuditRoot) {
    New-Item -ItemType Directory -Force -Path $AuditRoot | Out-Null
}

$serviceProject = Join-Path $repoRoot "Service\Service.csproj"
if (-not $servicePort) {
    try {
        $uri = [uri]$Urls
        $servicePort = $uri.Port
    } catch {
        $servicePort = 5000
    }
}

$logDir = Join-Path $repoRoot ("Experiments\out\_service\{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$serviceOutLog = Join-Path $logDir "service.out.log"
$serviceErrLog = Join-Path $logDir "service.err.log"

Write-Host "Service project: $serviceProject"
Write-Host "Config root: $ConfigRoot"
Write-Host "Config version: $ConfigVersion"
Write-Host "Audit root: $AuditRoot"
Write-Host "Urls: $Urls"
Write-Host "Log dir: $logDir"

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
$serviceArgs += @("--urls", $Urls)

$serviceCmd = '"' + $dotnet + '" ' + ($serviceArgs -join ' ')
Write-Host "Service command: $serviceCmd"

$serviceProc = Start-Process -FilePath $dotnet -ArgumentList $serviceArgs -PassThru -RedirectStandardOutput $serviceOutLog -RedirectStandardError $serviceErrLog

$healthUrl = "http://127.0.0.1:$servicePort/health"
$ready = $false
for ($h = 0; $h -lt 30; $h++) {
    if ($serviceProc.HasExited) {
        Write-Host "Service exited before becoming ready. Exit code: $($serviceProc.ExitCode)"
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
    if (Test-Path $serviceOutLog) {
        Write-Host "Last 50 stdout lines:"
        Get-Content -Path $serviceOutLog -Tail 50 | ForEach-Object { Write-Host $_ }
    }
    if (Test-Path $serviceErrLog) {
        Write-Host "Last 50 stderr lines:"
        Get-Content -Path $serviceErrLog -Tail 50 | ForEach-Object { Write-Host $_ }
    }
    throw "Service did not become ready at $healthUrl"
}

$pcIp = Get-PrivateIPv4
if (-not $pcIp) {
    Write-Host "Service ready. PID: $($serviceProc.Id)"
    Write-Host "SERVICE_URL=http://<PC_IP>:$servicePort"
    Write-Host "Unable to auto-detect private IPv4; set PC_IP manually."
} else {
    Write-Host "Service ready. PID: $($serviceProc.Id)"
    Write-Host ("SERVICE_URL=http://{0}:{1}" -f $pcIp, $servicePort)
}
