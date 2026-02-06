using System;
using System.Collections.Generic;

namespace Experiments.E2_AuditBench;

internal sealed class SessionGenerator
{
    private readonly string[] _scenePool = new[]
    {
        "training_scene",
        "urban_scene",
        "hard_scene",
        "coastal_scene",
        "forest_scene",
        "industrial_scene"
    };

    private readonly int _baseSeed;
    private readonly string _configVersion;

    public SessionGenerator(int baseSeed, string configVersion)
    {
        _baseSeed = baseSeed;
        _configVersion = configVersion;
    }

    public SessionEventData Generate(int index)
    {
        var rand = new Random(_baseSeed + index);
        var session = new SessionEventData
        {
            session_id = $"session-{index + 1:D6}",
            scene_id = _scenePool[index % _scenePool.Length],
            result_z = (float)rand.NextDouble(),
            time_t = (float)(rand.NextDouble() * 60.0),
            attempts = rand.Next(1, 6),
            seed = _baseSeed + rand.Next(1_000_000),
            config_version = _configVersion
        };

        session.npc_features = BuildFeatures(rand, 24);
        session.npc_params = BuildParams(rand, 20);
        return session;
    }

    private static List<NpcFeature> BuildFeatures(Random rand, int count)
    {
        var features = new List<NpcFeature>(count);
        var types = new[] { "float", "int", "bool", "string" };
        for (var i = 0; i < count; i++)
        {
            var type = types[i % types.Length];
            var feature = new NpcFeature
            {
                name = $"feature_{i}",
                feature_type = type
            };

            switch (type)
            {
                case "float":
                    feature.float_value = (float)(rand.NextDouble() * 2.0 - 1.0);
                    break;
                case "int":
                    feature.int_value = rand.Next(0, 100);
                    break;
                case "bool":
                    feature.bool_value = rand.Next(0, 2) == 0;
                    break;
                case "string":
                    feature.string_value = $"tag-{rand.Next(1_000_000)}";
                    break;
            }

            features.Add(feature);
        }

        return features;
    }

    private static List<NpcParamValue> BuildParams(Random rand, int count)
    {
        var items = new List<NpcParamValue>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new NpcParamValue
            {
                name = $"npc_param_{i}",
                value = (float)(rand.NextDouble() * 1.5)
            });
        }

        return items;
    }
}