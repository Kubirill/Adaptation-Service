using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Reports;
using E1_SerializationBenchmark.Manual;

namespace E1_SerializationBenchmark.Reporting;

internal static class SummaryFactory
{
    public static IReadOnlyList<SummaryRecord> Create(
        BenchmarkDotNet.Reports.Summary summary,
        IReadOnlyDictionary<(FormatType, MessageType, OperationType), ManualStats> manualStats,
        Serialization.SerializationFixture fixture)
    {
        var records = new List<SummaryRecord>();

        foreach (var report in summary.Reports)
        {
            var format = ParseEnumParameter<FormatType>(report, nameof(SerializationBenchmarks.Format));
            var message = ParseEnumParameter<MessageType>(report, nameof(SerializationBenchmarks.Message));
            var operation = ParseEnumParameter<OperationType>(report, nameof(SerializationBenchmarks.Operation));

            var stats = manualStats[(format, message, operation)];
            var gcStats = report.GcStats;

            var record = new SummaryRecord(
                format,
                message,
                operation,
                fixture.GetPayloadSize(format, message),
                MeanMicroseconds(report),
                stats.P50Microseconds,
                stats.P95Microseconds,
                stats.P99Microseconds,
                (long)gcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase),
                gcStats.Gen0Collections,
                gcStats.Gen1Collections,
                gcStats.Gen2Collections);

            records.Add(record);
        }

        return records
            .OrderBy(r => r.Format)
            .ThenBy(r => r.Message)
            .ThenBy(r => r.Operation)
            .ToList();
    }

    private static T ParseEnumParameter<T>(BenchmarkReport report, string parameterName) where T : struct, Enum
    {
        var parameter = report.BenchmarkCase.Parameters.Items.First(p => string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase));
        var textValue = parameter.Value?.ToString() ?? string.Empty;
        if (Enum.TryParse<T>(textValue, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Unable to parse parameter {parameterName} value '{parameter.Value}' as {typeof(T).Name}.");
    }

    private static double MeanMicroseconds(BenchmarkReport report)
    {
        var meanNanoseconds = report.ResultStatistics?.Mean ?? 0d;
        return meanNanoseconds / 1_000.0;
    }
}
