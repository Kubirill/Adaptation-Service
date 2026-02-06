using System;

namespace Experiments.E3_ProfileStorageBench.Workload;

using Experiments.E3_ProfileStorageBench.Models;

internal sealed class SessionGenerator
{
    private readonly string[] _scenes = new[]
    {
        "training_ground",
        "urban_lab",
        "zero_day",
        "coastal_ridge",
        "forest_reclaim",
        "industrial_loop"
    };

    private readonly int[] _contentVersions = { 100, 101, 110 };
    private readonly int[] _rulesVersions = { 1, 2, 3 };

    private readonly int _seed;

    public SessionGenerator(int seed)
    {
        _seed = seed;
    }

    public SessionEvent Generate(int index)
    {
        var rand = new Random(_seed + index * 17);
        var scene = _scenes[index % _scenes.Length];
        return new SessionEvent
        {
            Seq = index + 1,
            SceneId = scene,
            ResultZ = (float)rand.NextDouble(),
            TimeT = (float)(rand.NextDouble() * 90.0),
            Attempts = rand.Next(1, 6),
            Seed = _seed + index,
            ConfigVersion = $"cv-{(index % 4) + 1}",
            ContentVersion = $"c{_contentVersions[index % _contentVersions.Length]}",
            RulesVersion = $"r{_rulesVersions[index % _rulesVersions.Length]}"
        };
    }
}
