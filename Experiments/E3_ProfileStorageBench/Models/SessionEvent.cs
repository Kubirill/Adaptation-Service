using System.Text.Json.Serialization;

namespace Experiments.E3_ProfileStorageBench.Models;

internal sealed class SessionEvent
{
    public int Seq { get; set; }
    public string SceneId { get; set; } = string.Empty;
    public float ResultZ { get; set; }
    public float TimeT { get; set; }
    public int Attempts { get; set; }
    public int Seed { get; set; }
    public string ConfigVersion { get; set; } = string.Empty;
    public string ContentVersion { get; set; } = string.Empty;
    public string RulesVersion { get; set; } = string.Empty;

    [JsonIgnore]
    public double DeltaSkill => ResultZ * 0.05 + Attempts * 0.0005;
}
