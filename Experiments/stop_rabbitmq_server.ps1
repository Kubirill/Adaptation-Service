param(
    [string]$ComposePath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $ComposePath) {
    $ComposePath = Join-Path $repoRoot "Broker\docker-compose.rabbitmq.yml"
}

if (-not (Test-Path $ComposePath)) {
    throw "docker-compose file not found at $ComposePath"
}

Write-Host "Stopping RabbitMQ via docker compose..."
& docker compose -f $ComposePath down | Out-Null
Write-Host "RabbitMQ stopped."
