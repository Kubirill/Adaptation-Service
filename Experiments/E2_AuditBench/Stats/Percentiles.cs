using System;
using System.Collections.Generic;
using System.Linq;

namespace Experiments.E2_AuditBench.Stats;

internal static class Percentiles
{
    public static double ComputePercentile(List<long> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        var sorted = samples.OrderBy(x => x).ToArray();
        var rank = percentile * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);

        if (upper < 0)
        {
            return sorted[0];
        }

        if (upper >= sorted.Length)
        {
            return sorted[^1];
        }

        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}