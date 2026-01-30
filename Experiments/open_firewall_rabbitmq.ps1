param(
    [int]$AmqpPort = 5672,
    [int]$MgmtPort = 15672
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Run PowerShell as Administrator"
    exit 1
}

function Ensure-FirewallRule {
    param(
        [string]$Name,
        [int]$Port
    )
    $existing = Get-NetFirewallRule -DisplayName $Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-NetFirewallRule -DisplayName $Name -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private | Out-Null
        Write-Host "Firewall rule created for TCP port $Port (Private profile)."
    } else {
        Write-Host "Firewall rule already exists for TCP port $Port (Private profile)."
    }
}

Ensure-FirewallRule -Name "RabbitMQ AMQP TCP $AmqpPort" -Port $AmqpPort
Ensure-FirewallRule -Name "RabbitMQ Mgmt TCP $MgmtPort" -Port $MgmtPort
