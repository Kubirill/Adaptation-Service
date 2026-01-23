param(
    [int]$Trials = 3,
    [int]$Sessions = 10,
    [int]$WarmupSessions = 5,
    [int]$Seed = 1234,
    [string]$UnityPath = $env:UNITY_PATH
)

& "$PSScriptRoot\run_common.ps1" -Arch "Baseline" -Trials $Trials -Sessions $Sessions -WarmupSessions $WarmupSessions -Seed $Seed -UnityPath $UnityPath
