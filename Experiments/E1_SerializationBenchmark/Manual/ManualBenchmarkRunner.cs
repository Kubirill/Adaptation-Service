using System.Diagnostics;
using System.Linq;

namespace E1_SerializationBenchmark.Manual;

public sealed record ManualStats(
    FormatType Format,
    MessageType Message,
    OperationType Operation,
    double P50Microseconds,
    double P95Microseconds,
    double P99Microseconds);

public static class ManualBenchmarkRunner
{
    public static IReadOnlyDictionary<(FormatType, MessageType, OperationType), ManualStats> Run(int iterations = 800)
    {
        var fixture = Serialization.SerializationFixture.Instance;
        var results = new Dictionary<(FormatType, MessageType, OperationType), ManualStats>();

        foreach (FormatType format in Enum.GetValues<FormatType>())
        {
            foreach (MessageType message in Enum.GetValues<MessageType>())
            {
                foreach (OperationType operation in Enum.GetValues<OperationType>())
                {
                    var durations = Measure(format, message, operation, fixture, iterations);
                    var micros = durations.Select(ticks => ticks * 1_000_000.0 / Stopwatch.Frequency).ToArray();
                    Array.Sort(micros);

                    var stats = new ManualStats(
                        format,
                        message,
                        operation,
                        P50Microseconds: Percentile(micros, 50),
                        P95Microseconds: Percentile(micros, 95),
                        P99Microseconds: Percentile(micros, 99));

                    results[(format, message, operation)] = stats;
                }
            }
        }

        return results;
    }

    private static double[] Measure(FormatType format, MessageType message, OperationType operation, Serialization.SerializationFixture fixture, int iterations)
    {
        var action = operation switch
        {
            OperationType.Serialize => new Action(() => fixture.Serialize(format, message)),
            OperationType.Deserialize => new Action(() => fixture.Deserialize(format, message)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        // Warmup once before recording.
        action();

        var durations = new double[iterations];
        var stopwatch = new Stopwatch();

        for (int i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            action();
            stopwatch.Stop();
            durations[i] = stopwatch.ElapsedTicks;
        }

        return durations;
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        if (sortedSamples.Length == 0)
        {
            return 0;
        }

        var rank = percentile / 100.0 * (sortedSamples.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (upper >= sortedSamples.Length)
        {
            return sortedSamples[^1];
        }

        var fraction = rank - lower;
        return sortedSamples[lower] + (sortedSamples[upper] - sortedSamples[lower]) * fraction;
    }
}
