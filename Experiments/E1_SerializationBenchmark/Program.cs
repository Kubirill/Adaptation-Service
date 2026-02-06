using BenchmarkDotNet.Running;
using E1_SerializationBenchmark.Manual;
using E1_SerializationBenchmark.Reporting;
using E1_SerializationBenchmark.Serialization;
using E1_SerializationBenchmark;

Console.WriteLine("Starting serialization benchmark suite...");
var benchmarkSummary = BenchmarkRunner.Run<SerializationBenchmarks>();

Console.WriteLine("Running manual percentile measurements...");
var manualStats = ManualBenchmarkRunner.Run();

var records = SummaryFactory.Create(benchmarkSummary, manualStats, SerializationFixture.Instance);
var (csvPath, jsonPath) = SummaryReporter.Publish(records);

Console.WriteLine($"Summary CSV: {csvPath}");
Console.WriteLine($"Summary JSON: {jsonPath}");
