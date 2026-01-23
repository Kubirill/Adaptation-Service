param(
    [int]$Trials = 3,
    [int]$Sessions = 10,
    [int]$Seed = 1234,
    [string]$UnityPath = $env:UNITY_PATH
)

& "$PSScriptRoot\run_common.ps1" -Arch "B1" -Trials $Trials -Sessions $Sessions -Seed $Seed -UnityPath $UnityPath
