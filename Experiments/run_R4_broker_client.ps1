param(
    [int]$Trials = 5,
    [int]$Sessions = 30,
    [int]$WarmupSessions = 5,
    [int]$Seed = 1234,
    [string]$UnityPath = $env:UNITY_PATH,
    [string]${BrokerHost} = "",
    [int]${BrokerPort} = 5672,
    [switch]$SkipCheck
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

$env:BROKER_HOST = ${BrokerHost}
$env:BROKER_PORT = ${BrokerPort}

if (-not $SkipCheck) {
    & "$PSScriptRoot\check_R4_broker_client.ps1" -BrokerHost ${BrokerHost} -BrokerPort ${BrokerPort}
}

& "$PSScriptRoot\run_common.ps1" -Arch "R4_broker_client" -AdapterName "R4_remote_BrokerRPC" -Trials $Trials -Sessions $Sessions -WarmupSessions $WarmupSessions -Seed $Seed -UnityPath $UnityPath | Out-Null
