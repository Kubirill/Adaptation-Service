using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;

using Experiments.E3_ProfileStorageBench.Models;
using Experiments.E3_ProfileStorageBench.Storage;
using Experiments.E3_ProfileStorageBench.Stats;
using Experiments.E3_ProfileStorageBench.Workload;

namespace Experiments.E3_ProfileStorageBench;

internal static class Program
{
    internal static readonly int[] DefaultRestoreTargets = { 0, 10, 100, 1000, 5000, 10000 };

    private static void Main(string[] args)
    {
        var options = RunOptions.Parse(args);
        Execute(options);
    }

    private static void Execute(RunOptions options)
    {
        var outDir = Path.GetFullPath(options.OutDir);
        Directory.CreateDirectory(outDir);

        var modes = new[]
        {
            ("S1-CRUD", new Func<IStorageMode>(() => new SqliteProfileStore(Path.Combine(outDir, "s1_profile.db")))),
            ("S2-Event", new Func<IStorageMode>(() => new EventSourcingMode(Path.Combine(outDir, "s2_events.db")))),
            ("S3-Hybrid", new Func<IStorageMode>(() => new HybridMode(Path.Combine(outDir, "s3_hybrid.db"), options.SnapshotEvery)))
        };

        var writeRecords = new List<WriteRecord>();
        var restoreRecords = new List<RestoreRecord>();
        var sizeRecords = new List<SizeRecord>();

        foreach (var (name, factory) in modes)
        {
            var modeDir = Path.Combine(outDir, name);
            Directory.CreateDirectory(modeDir);

            using var storage = factory();
            storage.Initialize();

            var generator = new SessionGenerator(options.Seed);
            var profile = ProfileState.CreateDefault();

            for (var i = 0; i < options.Warmup; i++)
            {
                var sessionEvent = generator.Generate(i);
                profile.Apply(sessionEvent);
                storage.Persist(sessionEvent, profile.Clone(), ShouldSnapshot(sessionEvent.Seq, options.SnapshotEvery));
            }

            for (var i = options.Warmup; i < options.Warmup + options.Sessions; i++)
            {
                var sessionEvent = generator.Generate(i);
                profile.Apply(sessionEvent);
                var snapshotBoundary = ShouldSnapshot(sessionEvent.Seq, options.SnapshotEvery);
                var stateCopy = profile.Clone();
                var sw = Stopwatch.StartNew();
                storage.Persist(sessionEvent, stateCopy, snapshotBoundary);
                sw.Stop();
                writeRecords.Add(new WriteRecord(name, i - options.Warmup + 1, sw.Elapsed.TotalMilliseconds));
            }

            var sizeBytes = storage.GetStorageSizeBytes();
            sizeRecords.Add(new SizeRecord(name, options.Sessions, sizeBytes));

            foreach (var target in options.RestoreTargets)
            {
                var upto = Math.Min(target, options.Warmup + options.Sessions);
                var sw = Stopwatch.StartNew();
                storage.Restore(upto);
                sw.Stop();
                restoreRecords.Add(new RestoreRecord(name, upto, sw.Elapsed.TotalMilliseconds));
            }
        }

        WriteCsv(Path.Combine(outDir, "write_times.csv"), new[] { "mode", "session_index", "write_ms" },
            writeRecords.Select(r => new[] { r.Mode, r.SessionIndex.ToString(), FormatDouble(r.WriteMs) }));

        WriteCsv(Path.Combine(outDir, "restore_times.csv"), new[] { "mode", "N", "restore_ms" },
            restoreRecords.Select(r => new[] { r.Mode, r.N.ToString(), FormatDouble(r.RestoreMs) }));

        WriteCsv(Path.Combine(outDir, "sizes.csv"), new[] { "mode", "sessions", "bytes" },
            sizeRecords.Select(r => new[] { r.Mode, r.Sessions.ToString(), r.Bytes.ToString(CultureInfo.InvariantCulture) }));

        var summaryRows = new List<string[]>();
        foreach (var (name, _) in modes)
        {
            var writes = writeRecords.Where(r => r.Mode == name).Select(r => r.WriteMs).ToList();
            var restores = restoreRecords.Where(r => r.Mode == name).Select(r => r.RestoreMs).ToList();
            var sizes = sizeRecords.First(r => r.Mode == name);

            summaryRows.Add(new[]
            {
                name,
                FormatDouble(Percentiles.Compute(writes, 0.5)),
                FormatDouble(Percentiles.Compute(writes, 0.95)),
                FormatDouble(Percentiles.Compute(writes, 0.99)),
                FormatDouble(Percentiles.Compute(restores, 0.5)),
                FormatDouble(Percentiles.Compute(restores, 0.95)),
                FormatDouble(Percentiles.Compute(restores, 0.99)),
                sizes.Bytes.ToString(CultureInfo.InvariantCulture)
            });
        }

        WriteCsv(Path.Combine(outDir, "summary.csv"),
            new[] { "mode", "write_p50", "write_p95", "write_p99", "restore_p50", "restore_p95", "restore_p99", "bytes" },
            summaryRows);
    }

    private static bool ShouldSnapshot(int seq, int snapshotEvery)
    {
        return snapshotEvery > 0 && seq % snapshotEvery == 0;
    }

    private static string FormatDouble(double value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

    private static void WriteCsv(string path, string[] header, IEnumerable<string[]> rows)
    {
        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        writer.WriteLine(string.Join(",", header));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", row));
        }
    }
}

internal sealed record WriteRecord(string Mode, int SessionIndex, double WriteMs);
internal sealed record RestoreRecord(string Mode, int N, double RestoreMs);
internal sealed record SizeRecord(string Mode, int Sessions, long Bytes);

internal sealed record RunOptions
{
    public int Sessions { get; init; } = 10000;
    public int Warmup { get; init; } = 200;
    public int SnapshotEvery { get; init; } = 100;
    public string OutDir { get; init; } = "Experiments/E3_ProfileStorageBench/out";
    public int Seed { get; init; } = 54321;
    public int[] RestoreTargets { get; init; } = Program.DefaultRestoreTargets;

    public static RunOptions Parse(string[] args)
    {
        var options = new RunOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sessions" when i + 1 < args.Length && int.TryParse(args[++i], out var sessions):
                    options = options with { Sessions = sessions };
                    break;
                case "--warmup" when i + 1 < args.Length && int.TryParse(args[++i], out var warmup):
                    options = options with { Warmup = warmup };
                    break;
                case "--snapshotEvery" when i + 1 < args.Length && int.TryParse(args[++i], out var snapshot):
                    options = options with { SnapshotEvery = snapshot };
                    break;
                case "--outdir" when i + 1 < args.Length:
                    options = options with { OutDir = args[++i] };
                    break;
                case "--seed" when i + 1 < args.Length && int.TryParse(args[++i], out var seed):
                    options = options with { Seed = seed };
                    break;
                case "--restoreTargets" when i + 1 < args.Length:
                    options = options with { RestoreTargets = args[++i].Split(',').Select(int.Parse).ToArray() };
                    break;
            }
        }

        return options;
    }
}
