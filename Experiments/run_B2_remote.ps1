param(
    [int]$Trials = 3,
    [int]$Sessions = 10,
    [int]$WarmupSessions = 5,
    [int]$Seed = 1234,
    [string]$ServiceUrl = "",
    [int]$ServiceTimeoutMs = 3000,
    [int]$ServiceRetries = 2,
    [int]$ServiceRetryDelayMs = 250,
    [string]$ProfileId = ""
)

$ErrorActionPreference = "Stop"

if (-not $ServiceUrl) {
    throw "ServiceUrl is required for remote runs, e.g. http://<remote-ip>:5000"
}

& "$PSScriptRoot\run_common.ps1" -Arch "B2" -Trials $Trials -Sessions $Sessions -WarmupSessions $WarmupSessions -Seed $Seed -ServiceUrl $ServiceUrl -ServiceTimeoutMs $ServiceTimeoutMs -ServiceRetries $ServiceRetries -ServiceRetryDelayMs $ServiceRetryDelayMs -ProfileId $ProfileId
