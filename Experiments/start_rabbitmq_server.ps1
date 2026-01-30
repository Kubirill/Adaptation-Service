param(
    [string]$ComposePath = "",
    [int]$TimeoutSeconds = 30
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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $ComposePath) {
    $ComposePath = Join-Path $repoRoot "Broker\docker-compose.rabbitmq.yml"
}

if (-not (Test-Path $ComposePath)) {
    throw "docker-compose file not found at $ComposePath"
}

Write-Host "Starting RabbitMQ via docker compose..."
Write-Host "Compose file: $ComposePath"

$composeOutput = & docker compose -f $ComposePath up -d 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $composeOutput
    throw "docker compose failed (exit code $LASTEXITCODE)."
}

$ready = $false
for ($i = 0; $i -lt $TimeoutSeconds; $i++) {
    if (Test-TcpPort -HostName "127.0.0.1" -Port 5672 -TimeoutMs 1000) {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 1
}

if (-not $ready) {
    throw "RabbitMQ did not become ready on 5672 within $TimeoutSeconds seconds."
}

Write-Host "RabbitMQ is ready."
Write-Host "Management UI: http://127.0.0.1:15672 (user/pass from BROKER_USER/BROKER_PASS or defaults)"
