$ErrorActionPreference = 'Stop'

$projectPath = "Experiments/E1_SerializationBenchmark"
$csproj = Join-Path $projectPath "E1_SerializationBenchmark.csproj"
$projectResultsFolder = Join-Path $projectPath "BenchmarkDotNet.Artifacts\results"
$rootResultsFolder = Join-Path (Get-Location) "BenchmarkDotNet.Artifacts\results"
$resultsFolder = if (Test-Path $projectResultsFolder) {
    $projectResultsFolder
} elseif (Test-Path $rootResultsFolder) {
    $rootResultsFolder
} else {
    throw "BenchmarkDotNet results folder not found."
}
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = Join-Path "Experiments/out/E1" $timestamp

Write-Host "Building release binaries..."
dotnet build $csproj -c Release

Write-Host "Running benchmarks (Release)..."
dotnet run --project $csproj -c Release --no-build

if (Test-Path $outFolder)
{
    Write-Host "Cleaning previous output directory $outFolder"
    Remove-Item $outFolder -Recurse -Force
}

New-Item -ItemType Directory -Path $outFolder -Force | Out-Null

Write-Host "Copying benchmark artifacts to $outFolder"
Copy-Item -Path (Join-Path $resultsFolder "*") -Destination $outFolder -Recurse -Force

$summaryCsv = Join-Path $resultsFolder "summary.csv"
$summaryJson = Join-Path $resultsFolder "summary.json"

Write-Host "Benchmark results archived to $outFolder"
Write-Host "Summary CSV: $summaryCsv"
Write-Host "Summary JSON: $summaryJson"
