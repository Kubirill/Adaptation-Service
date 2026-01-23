param(
    [Parameter(Mandatory = $true)][string]$Arch,
    [int]$Trials = 3,
    [int]$Sessions = 10,
    [int]$WarmupSessions = 5,
    [int]$Seed = 1234,
    [string]$UnityPath = $env:UNITY_PATH,
    [string]$ServiceUrl = "",
    [int]$ServiceTimeoutMs = 3000,
    [int]$ServiceRetries = 2,
    [int]$ServiceRetryDelayMs = 250,
    [string]$ProfileId = ""
)

$ErrorActionPreference = "Stop"

function Resolve-UnityPath {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path $Preferred)) {
        return (Resolve-Path $Preferred).Path
    }

    $candidates = Get-ChildItem "C:\Program Files\Unity\Hub\Editor" -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" } |
        Where-Object { Test-Path $_ }

    if ($candidates.Count -gt 0) {
        return ($candidates | Sort-Object -Descending | Select-Object -First 1)
    }

    throw "Unity.exe not found. Set UNITY_PATH env var or pass -UnityPath."
}

$unity = Resolve-UnityPath -Preferred $UnityPath
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "UnityProject"
$timestamp = (Get-Date -Format "yyyyMMdd_HHmmss")
$outRoot = Join-Path $repoRoot "Experiments\out\$Arch\$timestamp"

New-Item -ItemType Directory -Force -Path $outRoot | Out-Null

Write-Host "Unity: $unity"
Write-Host "Project: $projectPath"
Write-Host "Output: $outRoot"

for ($i = 0; $i -lt $Trials; $i++) {
    $trialDir = Join-Path $outRoot ("trial_{0:d4}" -f $i)
    New-Item -ItemType Directory -Force -Path $trialDir | Out-Null

    $args = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $projectPath,
        "-executeMethod", "AdaptationUnity.Editor.BatchModeRunner.Run",
        "-adapter", $Arch,
        "-sessions", $Sessions,
        "-warmupSessions", $WarmupSessions,
        "-seed", ($Seed + $i),
        "-outDir", $trialDir
    )
    if ($ServiceUrl) {
        $args += @("-serviceUrl", $ServiceUrl)
        $args += @("-serviceTimeoutMs", $ServiceTimeoutMs)
        $args += @("-serviceRetries", $ServiceRetries)
        $args += @("-serviceRetryDelayMs", $ServiceRetryDelayMs)
        if ($ProfileId) {
            $args += @("-profileId", $ProfileId)
        }
    }

    Write-Host "Trial $i -> $trialDir"
    & $unity @args | Tee-Object -FilePath (Join-Path $trialDir "unity.log")
    if ($LASTEXITCODE -ne 0) {
        throw "Unity exited with code $LASTEXITCODE"
    }
}
