param(
    [string]$ConfigVersion = "v1",
    [string]$ConfigRoot = "",
    [string]$AuditRoot = "",
    [string]$Urls = "http://localhost:5000",
    [string]$DotNetPath = $env:DOTNET_PATH
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $ConfigRoot) {
    $ConfigRoot = Join-Path $repoRoot "Configs"
}

if ($AuditRoot) {
    New-Item -ItemType Directory -Force -Path $AuditRoot | Out-Null
}

$serviceProject = Join-Path $repoRoot "Service\Service.csproj"

Write-Host "Service project: $serviceProject"
Write-Host "Config root: $ConfigRoot"
Write-Host "Config version: $ConfigVersion"
Write-Host "Audit root: $AuditRoot"
Write-Host "Urls: $Urls"

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
