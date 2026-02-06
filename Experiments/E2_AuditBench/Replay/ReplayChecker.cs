using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Experiments.E2_AuditBench.Replay;

internal sealed class ReplayResult
{
    public string Mode { get; init; } = string.Empty;
    public int SessionsTotal { get; init; }
    public int SessionsMatched { get; init; }
    public double MatchRatio => SessionsTotal == 0 ? 1.0 : (double)SessionsMatched / SessionsTotal;
}

internal static class ReplayChecker
{
    public static ReplayResult Check(string mode, string? logPath, Func<SessionEventData, DecisionData> compute)
    {
        if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
        {
            return new ReplayResult { Mode = mode, SessionsTotal = 0, SessionsMatched = 0 };
        }

        return mode switch
        {
            "jsonl" => ReplayJsonl(mode, logPath, compute),
            "otel" => ReplayOtel(mode, logPath, compute),
            "prov" => ReplayProv(mode, logPath, compute),
            _ => new ReplayResult
            {
                Mode = mode,
                SessionsTotal = 0,
                SessionsMatched = 0
            }
        };
    }

    private static ReplayResult ReplayJsonl(string mode, string logPath, Func<SessionEventData, DecisionData> compute)
    {
        var total = 0;
        var matched = 0;
        foreach (var line in File.ReadLines(logPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var root = JsonDocument.Parse(line).RootElement;
            var ek = JsonSerializer.Deserialize<SessionEventData>(root.GetProperty("ek").GetRawText(), JsonDefaults.Canonical);
            var recordedDecision = JsonSerializer.Deserialize<DecisionData>(root.GetProperty("dk").GetRawText(), JsonDefaults.Canonical);
            if (ek is null || recordedDecision is null)
            {
                continue;
            }

            total++;
            var replayed = compute(ek);
            if (HashDecision(replayed).Equals(HashDecision(recordedDecision), StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }
        }

        return new ReplayResult
        {
            Mode = mode,
            SessionsTotal = total,
            SessionsMatched = matched
        };
    }

    private static ReplayResult ReplayOtel(string mode, string logPath, Func<SessionEventData, DecisionData> compute)
    {
        var total = 0;
        var matched = 0;
        foreach (var line in File.ReadLines(logPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var root = JsonDocument.Parse(line).RootElement;
            if (!root.TryGetProperty("attributes", out var attributes))
            {
                continue;
            }

            var ekJson = attributes.GetProperty("ek_json").GetString();
            var dkJson = attributes.GetProperty("dk_json").GetString();
            if (ekJson is null || dkJson is null)
            {
                continue;
            }

            var ek = JsonSerializer.Deserialize<SessionEventData>(ekJson, JsonDefaults.Canonical);
            var recordedDecision = JsonSerializer.Deserialize<DecisionData>(dkJson, JsonDefaults.Canonical);
            if (ek is null || recordedDecision is null)
            {
                continue;
            }

            total++;
            var replayed = compute(ek);
            if (HashDecision(replayed).Equals(HashDecision(recordedDecision), StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }
        }

        return new ReplayResult
        {
            Mode = mode,
            SessionsTotal = total,
            SessionsMatched = matched
        };
    }

    private static ReplayResult ReplayProv(string mode, string logPath, Func<SessionEventData, DecisionData> compute)
    {
        var total = 0;
        var matched = 0;
        foreach (var line in File.ReadLines(logPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var root = JsonDocument.Parse(line).RootElement;
            if (!root.TryGetProperty("ek_entity", out var ekEntity))
            {
                continue;
            }

            if (!root.TryGetProperty("dk_entity", out var dkEntity))
            {
                continue;
            }

            var ekJson = ekEntity.GetProperty("attributes").GetProperty("ek_json").GetString();
            var dkJson = dkEntity.GetProperty("attributes").GetProperty("dk_json").GetString();
            if (ekJson is null || dkJson is null)
            {
                continue;
            }

            var ek = JsonSerializer.Deserialize<SessionEventData>(ekJson, JsonDefaults.Canonical);
            var recordedDecision = JsonSerializer.Deserialize<DecisionData>(dkJson, JsonDefaults.Canonical);
            if (ek is null || recordedDecision is null)
            {
                continue;
            }

            total++;
            var replayed = compute(ek);
            if (HashDecision(replayed).Equals(HashDecision(recordedDecision), StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }
        }

        return new ReplayResult
        {
            Mode = mode,
            SessionsTotal = total,
            SessionsMatched = matched
        };
    }

    private static string HashDecision(DecisionData decision)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(decision, JsonDefaults.Canonical);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}