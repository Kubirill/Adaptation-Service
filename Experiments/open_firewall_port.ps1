param(
    [int]$Port = 0
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Run PowerShell as Administrator"
    exit 1
}

if (-not $Port) {
    $servicePortEnv = $env:SERVICE_PORT
    if ($servicePortEnv) {
        [int]::TryParse($servicePortEnv, [ref]$Port) | Out-Null
    }
}
if (-not $Port) {
    $Port = 5000
}

$ruleName = "AdaptationService TCP $Port"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (-not $existing) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private | Out-Null
    Write-Host "Firewall rule created for TCP port $Port (Private profile)."
} else {
    Write-Host "Firewall rule already exists for TCP port $Port (Private profile)."
}
