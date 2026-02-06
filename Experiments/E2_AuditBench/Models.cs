using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Experiments.E2_AuditBench;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

internal sealed class SessionEventData
{
    public string session_id { get; set; } = string.Empty;
    public string scene_id { get; set; } = string.Empty;
    public float result_z { get; set; }
    public float time_t { get; set; }
    public int attempts { get; set; }
    public int seed { get; set; }
    public string config_version { get; set; } = string.Empty;
    public List<NpcFeature> npc_features { get; set; } = new();
    public List<NpcParamValue> npc_params { get; set; } = new();
}

internal sealed class NpcFeature
{
    public string name { get; set; } = string.Empty;
    public string feature_type { get; set; } = string.Empty;
    public float float_value { get; set; }
    public int int_value { get; set; }
    public bool bool_value { get; set; }
    public string string_value { get; set; } = string.Empty;
}

internal sealed class NpcParamValue
{
    public string name { get; set; } = string.Empty;
    public float value { get; set; }
}

internal sealed class ExplanationEntryData
{
    public string name { get; set; } = string.Empty;
    public string value { get; set; } = string.Empty;
}

internal sealed class DecisionData
{
    public string next_scene_id { get; set; } = string.Empty;
    public List<NpcParamValue> npc_params { get; set; } = new();
    public List<ExplanationEntryData> explanation { get; set; } = new();
    public int seed { get; set; }
    public string config_version { get; set; } = string.Empty;
    public string correlation_id { get; set; } = string.Empty;
}
