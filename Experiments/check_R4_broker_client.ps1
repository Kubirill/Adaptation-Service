param(
    [string]${BrokerHost} = "",
    [int]${BrokerPort} = 5672,
    [string]$DotNetPath = $env:DOTNET_PATH
)

$ErrorActionPreference = "Stop"

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMs = 1000
    )
    $client = New-Object System.Net.Sockets.TcpClient
    $async = $client.BeginConnect($HostName, $Port, $null, $null)
    if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
        $client.Close()
        return $false
    }
    try {
        $client.EndConnect($async) | Out-Null
        $client.Close()
        return $true
    } catch {
        $client.Close()
        return $false
    }
}

if (-not ${BrokerHost}) {
    ${BrokerHost} = $env:BROKER_HOST
}
if (-not ${BrokerHost}) {
    throw "BROKER_HOST is required (set env var or pass -BrokerHost)."
}


Write-Host "Checking broker connectivity at ${BrokerHost}:${BrokerPort}..."
if (-not (Test-TcpPort -HostName ${BrokerHost} -Port ${BrokerPort} -TimeoutMs 1000)) {
    throw "Cannot connect to broker at ${BrokerHost}:${BrokerPort}"
}

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

$env:BROKER_HOST = ${BrokerHost}
$env:BROKER_PORT = ${BrokerPort}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "Experiments\BrokerClientCheck\BrokerClientCheck.csproj"

Write-Host "Running broker RPC check..."
& $dotnet run --project $project
