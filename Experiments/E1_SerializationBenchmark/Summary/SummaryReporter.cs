using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace E1_SerializationBenchmark.Reporting;

internal static class SummaryReporter
{
    public static (string CsvPath, string JsonPath) Publish(IReadOnlyList<SummaryRecord> records)
    {
        var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts", "results");
        Directory.CreateDirectory(resultsDir);

        var csvPath = Path.Combine(resultsDir, "summary.csv");
        var jsonPath = Path.Combine(resultsDir, "summary.json");

        var csvLines = new List<string> { SummaryRecord.CsvHeader() };
        csvLines.AddRange(records.Select(r => r.CsvLine()));
        File.WriteAllText(csvPath, string.Join(Environment.NewLine, csvLines));

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(records, options));

        return (csvPath, jsonPath);
    }
}
