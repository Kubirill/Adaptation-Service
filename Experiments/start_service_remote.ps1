param(
    [string]$ConfigVersion = "v1",
    [string]$ConfigRoot = "",
    [string]$AuditRoot = "",
    [string]$Urls = "http://0.0.0.0:5000",
    [string]$DotNetPath = $env:DOTNET_PATH
)

$ErrorActionPreference = "Stop"

Write-Host "Remote service instructions:"
Write-Host "1) Copy repo to remote machine."
Write-Host "2) Run this script there:"
Write-Host "   powershell -ExecutionPolicy Bypass -File .\\Experiments\\start_service_remote.ps1"
Write-Host "3) Use the remote IP in Unity -serviceUrl, e.g. http://<remote-ip>:5000"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $ConfigRoot) {
    $ConfigRoot = Join-Path $repoRoot "Configs"
}
if ($AuditRoot) {
    New-Item -ItemType Directory -Force -Path $AuditRoot | Out-Null
}

$serviceProject = Join-Path $repoRoot "Service\Service.csproj"
Write-Host "Starting service on: $Urls"

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

& $dotnet run --project $serviceProject -- --ConfigRoot $ConfigRoot --ConfigVersion $ConfigVersion --AuditRoot $AuditRoot --urls $Urls
