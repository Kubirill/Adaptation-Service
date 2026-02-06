$ErrorActionPreference = "Stop"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outRoot = Join-Path "Experiments/out/E2" $timestamp
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

$modes = @("none","jsonl","otel","prov")
foreach ($mode in $modes) {
    Write-Host ("Running mode " + $mode + "...")
    dotnet run --project Experiments/E2_AuditBench --configuration Release -- --mode $mode --sessions 10000 --warmup 1000 --flush per_session --outdir $outRoot
}

$metricsByMode = @{}
foreach ($mode in $modes) {
    $metricsFile = Join-Path $outRoot "$mode\metrics.json"
    if (Test-Path $metricsFile) {
        $metricsByMode[$mode] = Get-Content $metricsFile | ConvertFrom-Json
    }
}

$baseSummary = if ($metricsByMode.ContainsKey("none")) { $metricsByMode["none"] } else { $null }
$baseP95 = if ($baseSummary) { [double]$baseSummary.TotalP95Us } else { 0 }

$summaryRows = foreach ($mode in $modes) {
    if (-not $metricsByMode.ContainsKey($mode)) {
        continue
    }

    $entry = $metricsByMode[$mode]
    [pscustomobject]@{
        mode = $entry.Mode
        sessions = $entry.Sessions
        flush_mode = $entry.FlushMode
        total_p50_us = [double]$entry.TotalP50Us
        total_p95_us = [double]$entry.TotalP95Us
        total_p99_us = [double]$entry.TotalP99Us
        audit_p50_us = [double]$entry.AuditP50Us
        audit_p95_us = [double]$entry.AuditP95Us
        audit_p99_us = [double]$entry.AuditP99Us
        delta_total_p95_us_vs_none = [double]$entry.TotalP95Us - $baseP95
        bytes_total = [double]$entry.BytesTotal
        bytes_per_session_mean = [double]$entry.BytesPerSessionMean
        replay_match_ratio = [double]$entry.ReplayMatchRatio
        q1_ms = [double]$entry.Q1Ms
        q2_ms = [double]$entry.Q2Ms
        q3_ms = [double]$entry.Q3Ms
    }
}

$summaryPath = Join-Path $outRoot "summary.csv"
$summaryRows | Export-Csv -NoTypeInformation -Path $summaryPath

$replayRecords = foreach ($mode in $modes) {
    if (-not $metricsByMode.ContainsKey($mode)) {
        continue
    }

    $entry = $metricsByMode[$mode]
    [pscustomobject]@{
        mode = $entry.Mode
        sessions_total = $entry.ReplaySessionsTotal
        sessions_matched = $entry.ReplaySessionsMatched
        match_ratio = $entry.ReplayMatchRatio
    }
}

$replayPath = Join-Path $outRoot "replay_results.csv"
$replayRecords | Export-Csv -NoTypeInformation -Path $replayPath

Write-Host ("Summary written to " + $summaryPath)
Get-Content $summaryPath | Select-Object -First 10 | ForEach-Object { Write-Host $_ }
Write-Host ("Replay results written to " + $replayPath)
