using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Experiments.E2_AuditBench;

namespace Experiments.E2_AuditBench.Stats;

internal sealed class QueryResults
{
    public double Q1Ms { get; init; }
    public double Q2Ms { get; init; }
    public double Q3Ms { get; init; }
}

internal static class QueryRunner
{
    public static QueryResults Run(string mode, string? logPath)
    {
        if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
        {
            return new QueryResults();
        }

        var records = LoadRecords(mode, logPath);
        if (records.Count == 0)
        {
            return new QueryResults();
        }

        var q1 = TimeQuery(() => RunQuery1(records));
        var q2 = TimeQuery(() => RunQuery2(records));
        var q3 = TimeQuery(() => RunQuery3(records));
        return new QueryResults { Q1Ms = q1, Q2Ms = q2, Q3Ms = q3 };
    }

    private static void RunQuery1(List<AuditSnapshot> records)
    {
        var record = records.First();
        Console.WriteLine($"Q1: {record.CorrelationId} -> {record.NextSceneId}, explanation count {record.Explanation.Count}");
    }

    private static void RunQuery2(List<AuditSnapshot> records)
    {
        var byVersion = records.GroupBy(r => r.ConfigVersion)
            .Select(g => (Version: g.Key, Count: g.Count()))
            .OrderByDescending(t => t.Count)
            .ToList();

        Console.WriteLine("Q2: sessions per config_version");
        foreach (var (version, count) in byVersion)
        {
            Console.WriteLine($"  {version}: {count}");
        }
    }

    private static void RunQuery3(List<AuditSnapshot> records)
    {
        const string target = "hard_scene";
        var found = records.Count(r => string.Equals(r.NextSceneId, target, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"Q3: {found} sessions targeting {target}");
    }

    private static double TimeQuery(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private static List<AuditSnapshot> LoadRecords(string mode, string logPath)
    {
        var list = new List<AuditSnapshot>();
        foreach (var line in File.ReadLines(logPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            switch (mode)
            {
                case "jsonl":
                    list.AddRange(ParseJsonl(line));
                    break;
                case "otel":
                    list.AddRange(ParseOtel(line));
                    break;
                case "prov":
                    list.AddRange(ParseProv(line));
                    break;
            }
        }

        return list;
    }

    private static IEnumerable<AuditSnapshot> ParseJsonl(string line)
    {
        var root = JsonDocument.Parse(line).RootElement;
        if (!root.TryGetProperty("correlation_id", out var cid))
        {
            yield break;
        }

        if (!root.TryGetProperty("dk", out var decisionElement))
        {
            yield break;
        }

        var decision = JsonSerializer.Deserialize<DecisionData>(decisionElement.GetRawText(), JsonDefaults.Canonical);
        if (decision is null)
        {
            yield break;
        }

        yield return new AuditSnapshot
        {
            CorrelationId = cid.GetString() ?? string.Empty,
            NextSceneId = decision.next_scene_id,
            ConfigVersion = decision.config_version,
            Explanation = decision.explanation
        };
    }

    private static IEnumerable<AuditSnapshot> ParseOtel(string line)
    {
        var root = JsonDocument.Parse(line).RootElement;
        if (!root.TryGetProperty("attributes", out var attributes))
        {
            yield break;
        }

        if (!attributes.TryGetProperty("dk_json", out var dkJsonElement))
        {
            yield break;
        }

        if (!attributes.TryGetProperty("correlation_id", out var cidElement))
        {
            yield break;
        }

        var dkJson = dkJsonElement.GetString();
        if (string.IsNullOrEmpty(dkJson))
        {
            yield break;
        }

        var decision = JsonSerializer.Deserialize<DecisionData>(dkJson, JsonDefaults.Canonical);
        if (decision is null)
        {
            yield break;
        }

        yield return new AuditSnapshot
        {
            CorrelationId = cidElement.GetString() ?? string.Empty,
            NextSceneId = decision.next_scene_id,
            ConfigVersion = decision.config_version,
            Explanation = decision.explanation
        };
    }

    private static IEnumerable<AuditSnapshot> ParseProv(string line)
    {
        var root = JsonDocument.Parse(line).RootElement;
        if (!root.TryGetProperty("dk_entity", out var dkEntity))
        {
            yield break;
        }

        if (!root.TryGetProperty("correlation_id", out var cidElement))
        {
            yield break;
        }

        if (!dkEntity.TryGetProperty("attributes", out var attributes)
            || !attributes.TryGetProperty("dk_json", out var dkJsonElement))
        {
            yield break;
        }

        var dkJson = dkJsonElement.GetString();
        if (string.IsNullOrEmpty(dkJson))
        {
            yield break;
        }

        var decision = JsonSerializer.Deserialize<DecisionData>(dkJson, JsonDefaults.Canonical);
        if (decision is null)
        {
            yield break;
        }

        yield return new AuditSnapshot
        {
            CorrelationId = cidElement.GetString() ?? string.Empty,
            NextSceneId = decision.next_scene_id,
            ConfigVersion = decision.config_version,
            Explanation = decision.explanation
        };
    }

    private sealed class AuditSnapshot
    {
        public string CorrelationId { get; init; } = string.Empty;
        public string NextSceneId { get; init; } = string.Empty;
        public string ConfigVersion { get; init; } = string.Empty;
        public List<ExplanationEntryData> Explanation { get; init; } = new();
    }
}