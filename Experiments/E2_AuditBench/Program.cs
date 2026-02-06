using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdaptationCore;
using Experiments.E2_AuditBench.Replay;
using Experiments.E2_AuditBench.Stats;
using Experiments.E2_AuditBench.Writers;

namespace Experiments.E2_AuditBench;

internal static class Program
{
    private const int GeneratorSeed = 1_234_567;
    private static readonly JsonSerializerOptions ModeOptions = new() { WriteIndented = true };

    private static readonly ConfigPackage Config = CreateConfig();

    private static void Main(string[] args)
    {
        var runArguments = RunArguments.Parse(args);
        Execute(runArguments);
    }

    private static void Execute(RunArguments arguments)
    {
        var outDir = Path.GetFullPath(arguments.OutDir);
        Directory.CreateDirectory(outDir);
        var modeDir = Path.Combine(outDir, arguments.Mode);
        Directory.CreateDirectory(modeDir);

        var flushPerSession = arguments.FlushMode == FlushMode.PerSession;
        var logPath = string.Empty;
        var totalTimes = new List<long>(arguments.Sessions);
        var computeTimes = new List<long>(arguments.Sessions);
        var auditTimes = new List<long>(arguments.Sessions);
        var payloads = new List<long>(arguments.Sessions);

        using (var auditWriter = CreateAuditWriter(arguments.Mode, modeDir, flushPerSession))
        using (var sessionMetricsStream = new StreamWriter(Path.Combine(modeDir, "session_metrics.csv"), false, Encoding.UTF8))
        {
            logPath = auditWriter.LogFilePath;
            sessionMetricsStream.WriteLine("session_id,total_us,compute_us,audit_us,payload_bytes");

            var generator = new SessionGenerator(GeneratorSeed, Config.version ?? "v1.0");
            Warmup(generator, arguments.Warmup, auditWriter);

            for (var i = arguments.Warmup; i < arguments.Warmup + arguments.Sessions; i++)
            {
                var sessionEvent = generator.Generate(i);
                var metric = MeasureSession(sessionEvent, auditWriter, arguments.Mode != "none");
                totalTimes.Add(metric.TotalUs);
                computeTimes.Add(metric.ComputeUs);
                auditTimes.Add(metric.AuditUs);
                payloads.Add(metric.PayloadBytes);
                sessionMetricsStream.WriteLine($"{metric.SessionId},{metric.TotalUs},{metric.ComputeUs},{metric.AuditUs},{metric.PayloadBytes}");
            }

            auditWriter.Flush();
            sessionMetricsStream.Flush();
        }
        var replay = ReplayChecker.Check(arguments.Mode, logPath, RecomputeDecision);
        var queries = QueryRunner.Run(arguments.Mode, logPath);
        var summary = new ModeSummary
        {
            Mode = arguments.Mode,
            Sessions = arguments.Sessions,
            FlushMode = arguments.FlushMode == FlushMode.PerSession ? "per_session" : "end",
            TotalP50Us = Percentiles.ComputePercentile(totalTimes, 0.5),
            TotalP95Us = Percentiles.ComputePercentile(totalTimes, 0.95),
            TotalP99Us = Percentiles.ComputePercentile(totalTimes, 0.99),
            AuditP50Us = Percentiles.ComputePercentile(auditTimes, 0.5),
            AuditP95Us = Percentiles.ComputePercentile(auditTimes, 0.95),
            AuditP99Us = Percentiles.ComputePercentile(auditTimes, 0.99),
            BytesTotal = payloads.Sum(),
            BytesPerSessionMean = payloads.Count == 0 ? 0 : payloads.Sum() / (double)payloads.Count,
            ReplayMatchRatio = replay.MatchRatio,
            ReplaySessionsTotal = replay.SessionsTotal,
            ReplaySessionsMatched = replay.SessionsMatched,
            Q1Ms = queries.Q1Ms,
            Q2Ms = queries.Q2Ms,
            Q3Ms = queries.Q3Ms
        };

        File.WriteAllText(Path.Combine(modeDir, "metrics.json"), JsonSerializer.Serialize(summary, ModeOptions));
    }

    private static void Warmup(SessionGenerator generator, int warmup, IAuditWriter writer)
    {
        for (var i = 0; i < warmup; i++)
        {
            var sessionEvent = generator.Generate(i);
            MeasureSession(sessionEvent, writer, false);
        }
    }

    private static SessionMetric MeasureSession(SessionEventData sessionEvent, IAuditWriter writer, bool recordAudit)
    {
        var totalStart = Stopwatch.GetTimestamp();
        var computeStart = totalStart;
        var decision = RecomputeDecision(sessionEvent);
        var computeEnd = Stopwatch.GetTimestamp();

        long auditUs = 0;
        long payload = 0;
        if (recordAudit)
        {
            var auditStart = Stopwatch.GetTimestamp();
            payload = writer.Write(sessionEvent, decision, Config.version_hash);
            var auditEnd = Stopwatch.GetTimestamp();
            auditUs = TicksToMicroseconds(auditEnd - auditStart);
        }

        var totalEnd = Stopwatch.GetTimestamp();
        var totalUs = TicksToMicroseconds(totalEnd - totalStart);
        var computeUs = TicksToMicroseconds(computeEnd - computeStart);
        return new SessionMetric(sessionEvent.session_id, totalUs, computeUs, auditUs, payload);
    }

    private static DecisionData RecomputeDecision(SessionEventData sessionEvent)
    {
        var adaptation = new AdaptationEvent
        {
            session_id = sessionEvent.session_id,
            scene_id = sessionEvent.scene_id,
            time_t = sessionEvent.time_t,
            result_z = sessionEvent.result_z,
            attempts_a = sessionEvent.attempts,
            seed = sessionEvent.seed,
            config_version = sessionEvent.config_version
        };

        var result = AdaptationEngine.ComputeNext(adaptation, Config);
        var summary = new DecisionData
        {
            next_scene_id = result.Decision.next_scene_id,
            npc_params = result.Decision.npc_params
                .Select(p => new NpcParamValue { name = p.name, value = p.value })
                .ToList(),
            explanation = BuildExplanation(result.Decision.explanation, sessionEvent).ToList(),
            seed = result.Decision.seed,
            config_version = result.Decision.config_version,
            correlation_id = sessionEvent.session_id
        };

        return summary;
    }

    private static IEnumerable<ExplanationEntryData> BuildExplanation(List<ExplanationEntry>? baseEntries, SessionEventData sessionEvent)
    {
        if (baseEntries is { Count: > 0 })
        {
            foreach (var entry in baseEntries)
            {
                yield return new ExplanationEntryData { name = entry.name, value = entry.value };
            }
        }

        yield return new ExplanationEntryData { name = "session_scene", value = sessionEvent.scene_id };
        yield return new ExplanationEntryData { name = "attempts", value = sessionEvent.attempts.ToString() };
        yield return new ExplanationEntryData { name = "features", value = sessionEvent.npc_features.Count.ToString() };
        yield return new ExplanationEntryData { name = "params", value = sessionEvent.npc_params.Count.ToString() };
        yield return new ExplanationEntryData { name = "result_z", value = sessionEvent.result_z.ToString("0.000") };
        yield return new ExplanationEntryData { name = "time_t", value = sessionEvent.time_t.ToString("0.000") };
    }

    private static IAuditWriter CreateAuditWriter(string mode, string modeDir, bool flushPerSession)
    {
        return mode switch
        {
            "jsonl" => new JsonlAuditWriter(modeDir, flushPerSession),
            "otel" => new OtelAuditWriter(modeDir, flushPerSession),
            "prov" => new ProvAuditWriter(modeDir, flushPerSession),
            _ => new NullAuditWriter()
        };
    }

    private static ConfigPackage CreateConfig()
    {
        var config = new ConfigPackage
        {
            version = "v1.0.0",
            version_hash = "config-hash-2026",
            result_weight = 0.6f,
            time_weight = 0.4f,
            time_scale = 0.1f
        };

        config.npc_base_params["aggression"] = 0.2f;
        config.npc_base_params["curiosity"] = 0.3f;
        config.npc_base_params["patience"] = 0.6f;
        config.npc_base_params["alertness"] = 0.7f;
        return config;
    }

    private static long TicksToMicroseconds(long ticks)
    {
        return (long)(ticks * 1_000_000.0 / Stopwatch.Frequency);
    }

    private sealed record SessionMetric(string SessionId, long TotalUs, long ComputeUs, long AuditUs, long PayloadBytes);

    private sealed record RunArguments
    {
        public string Mode { get; init; } = "none";
        public int Sessions { get; init; } = 10000;
        public int Warmup { get; init; } = 1000;
        public FlushMode FlushMode { get; init; } = FlushMode.PerSession;
        public string OutDir { get; init; } = "Experiments/E2_AuditBench/out";

        public static RunArguments Parse(string[] args)
        {
            var result = new RunArguments();
            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                switch (token)
                {
                    case "--mode" when i + 1 < args.Length:
                        result = result with { Mode = args[++i].ToLowerInvariant() };
                        break;
                    case "--sessions" when i + 1 < args.Length && int.TryParse(args[++i], out var sessions):
                        result = result with { Sessions = sessions };
                        break;
                    case "--warmup" when i + 1 < args.Length && int.TryParse(args[++i], out var warmup):
                        result = result with { Warmup = warmup };
                        break;
                    case "--flush" when i + 1 < args.Length:
                        result = result with { FlushMode = ParseFlushMode(args[++i]) };
                        break;
                    case "--outdir" when i + 1 < args.Length:
                        result = result with { OutDir = args[++i] };
                        break;
                }
            }

            return result;
        }

        private static FlushMode ParseFlushMode(string value)
        {
            return value.Equals("end", StringComparison.OrdinalIgnoreCase)
                ? FlushMode.End
                : FlushMode.PerSession;
        }
    }

    private enum FlushMode
    {
        PerSession,
        End
    }

    private sealed class ModeSummary
    {
        public string Mode { get; init; } = string.Empty;
        public int Sessions { get; init; }
        public string FlushMode { get; init; } = string.Empty;
        public double TotalP50Us { get; init; }
        public double TotalP95Us { get; init; }
        public double TotalP99Us { get; init; }
        public double AuditP50Us { get; init; }
        public double AuditP95Us { get; init; }
        public double AuditP99Us { get; init; }
        public double BytesTotal { get; init; }
        public double BytesPerSessionMean { get; init; }
        public double ReplayMatchRatio { get; init; }
        public int ReplaySessionsTotal { get; init; }
        public int ReplaySessionsMatched { get; init; }
        public double Q1Ms { get; init; }
        public double Q2Ms { get; init; }
        public double Q3Ms { get; init; }
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public string? LogFilePath => null;
        public long Write(SessionEventData sessionEvent, DecisionData decision, string configHash) => 0;
        public void Flush() { }
        public void Dispose() { }
    }
}
