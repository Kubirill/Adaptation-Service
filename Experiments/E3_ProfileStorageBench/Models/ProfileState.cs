using System.Collections.Generic;
using System.Text.Json;

namespace Experiments.E3_ProfileStorageBench.Models;

internal sealed class ProfileState
{
    private static readonly string ProfileIdConst = "learner";

    public string ProfileId { get; init; } = ProfileIdConst;
    public double SkillLevel { get; set; }
    public double Fatigue { get; set; }
    public double Accuracy { get; set; }
    public double ReactionTime { get; set; }
    public string LastSceneId { get; set; } = string.Empty;
    public Dictionary<string, double> NpcParamPreferences { get; set; } = new();
    public string ConfigVersion { get; set; } = string.Empty;
    public string ContentVersion { get; set; } = string.Empty;
    public string RulesVersion { get; set; } = string.Empty;
    public int Seed { get; set; }

    public ProfileState Clone()
    {
        return new ProfileState
        {
            ProfileId = ProfileId,
            SkillLevel = SkillLevel,
            Fatigue = Fatigue,
            Accuracy = Accuracy,
            ReactionTime = ReactionTime,
            LastSceneId = LastSceneId,
            NpcParamPreferences = new Dictionary<string, double>(NpcParamPreferences),
            ConfigVersion = ConfigVersion,
            ContentVersion = ContentVersion,
            RulesVersion = RulesVersion,
            Seed = Seed
        };
    }

    public void Apply(SessionEvent sessionEvent)
    {
        SkillLevel = Clamp(SkillLevel + sessionEvent.DeltaSkill, 0, 1);
        Fatigue = Clamp(0.5 - sessionEvent.ResultZ * 0.1 + sessionEvent.Attempts * 0.01, 0, 1);
        Accuracy = Clamp(Accuracy + sessionEvent.ResultZ * 0.02 - sessionEvent.Attempts * 0.005, 0, 1);
        ReactionTime = Clamp(ReactionTime + sessionEvent.TimeT * 0.001 - sessionEvent.ResultZ * 0.01, 0, 1);
        LastSceneId = sessionEvent.SceneId;
        ConfigVersion = sessionEvent.ConfigVersion;
        ContentVersion = sessionEvent.ContentVersion;
        RulesVersion = sessionEvent.RulesVersion;
        Seed = sessionEvent.Seed;

        foreach (var key in NpcParamPreferences.Keys)
        {
            NpcParamPreferences[key] = Clamp(NpcParamPreferences[key] + (sessionEvent.ResultZ - 0.5) * 0.01, -1, 1);
        }
    }

    public static ProfileState CreateDefault()
    {
        var state = new ProfileState
        {
            SkillLevel = 0.5,
            Fatigue = 0.5,
            Accuracy = 0.5,
            ReactionTime = 0.5
        };

        for (var i = 0; i < 20; i++)
        {
            state.NpcParamPreferences[$"npc_pref_{i}"] = 0.1 * (i % 5);
        }

        return state;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonDefaults.DefaultOptions);

    public static ProfileState FromJson(string json) => JsonSerializer.Deserialize<ProfileState>(json, JsonDefaults.DefaultOptions) ?? CreateDefault();

    private static double Clamp(double value, double min, double max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
