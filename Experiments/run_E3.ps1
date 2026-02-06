$ErrorActionPreference = "Stop"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outRoot = Join-Path "Experiments/out/E3" $timestamp
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

Write-Host "Building E3 benchmark..."
dotnet build Experiments/E3_ProfileStorageBench -c Release

Write-Host "Running storage benchmark..."
dotnet run --project Experiments/E3_ProfileStorageBench --configuration Release -- --outdir $outRoot

$summaryPath = Join-Path $outRoot "summary.csv"
Write-Host "Experiment outputs saved to $outRoot"
Write-Host "Summary:"
Get-Content $summaryPath | Select-Object -First 10 | ForEach-Object { Write-Host $_ }
