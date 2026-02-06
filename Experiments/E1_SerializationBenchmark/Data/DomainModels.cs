using System.Text;

namespace E1_SerializationBenchmark.Data;

public record NpcFeatureData(
    string Name,
    float Agility,
    float Strength,
    int Alertness,
    bool IsHostile,
    string Classification,
    float Stamina,
    int Level,
    bool CanUseMagic,
    string Role,
    float Speed,
    int Experience,
    bool IsBoss,
    string Faction,
    float Luck);

public record NpcParamData(string Name, float Value);
public record ExplanationEntryData(string Key, string Value);

public record EventData(
    string SessionId,
    string SceneId,
    float ResultZ,
    float TimeT,
    int Attempts,
    int Seed,
    string ConfigVersion,
    IReadOnlyList<NpcFeatureData> NpcFeatures,
    IReadOnlyList<NpcParamData> NpcParams);

public record DecisionData(
    string NextSceneId,
    IReadOnlyList<NpcParamData> NpcParams,
    IReadOnlyList<ExplanationEntryData> Explanation,
    int Seed,
    string ConfigVersion);

public static class DataFactory
{
    private static readonly Random Random = new(42);

    private static readonly string[] Roles = new[]
    {
        "Scout", "Guard", "Healer", "Mage", "Engineer", "Support", "Leader", "Assassin"
    };

    private static readonly string[] Factions = new[] { "Vanguard", "Sable", "Aurora", "Solstice" };

    public static EventData CreateEventData()
    {
        var features = Enumerable.Range(0, 25)
            .Select(CreateNpcFeature)
            .ToList();

        var npcParams = Enumerable.Range(0, 20)
            .Select(index => new NpcParamData($"param_{index}", (float)(index + 1) * 1.27f))
            .ToList();

        return new EventData(
            SessionId: $"session-{Guid.NewGuid():N}",
            SceneId: $"scene-{Random.Next(1000, 9999)}",
            ResultZ: (float)(Random.NextDouble() * 1000),
            TimeT: (float)(Random.NextDouble() * 500),
            Attempts: Random.Next(1, 10),
            Seed: Random.Next(),
            ConfigVersion: $"cfg-{Random.Next(1, 5)}.{Random.Next(0, 99)}",
            NpcFeatures: features,
            NpcParams: npcParams);
    }

    public static DecisionData CreateDecisionData()
    {
        var npcParams = Enumerable.Range(0, 20)
            .Select(index => new NpcParamData($"param_{index}", (float)(index + 1) * 2.13f))
            .ToList();

        var explanation = Enumerable.Range(0, 10)
            .Select(index => new ExplanationEntryData($"why_{index}", $"value_{Random.Next(1000, 9999)}"))
            .ToList();

        return new DecisionData(
            NextSceneId: $"scene-{Random.Next(1000, 9999)}-next",
            NpcParams: npcParams,
            Explanation: explanation,
            Seed: Random.Next(),
            ConfigVersion: $"cfg-{Random.Next(1, 5)}.{Random.Next(0, 99)}");
    }

    private static NpcFeatureData CreateNpcFeature(int index)
    {
        return new NpcFeatureData(
            Name: $"npc_feature_{index}",
            Agility: (float)(Random.NextDouble() * 10),
            Strength: (float)(Random.NextDouble() * 20),
            Alertness: Random.Next(10, 100),
            IsHostile: Random.Next(0, 2) == 1,
            Classification: $"class_{Random.Next(1, 5)}",
            Stamina: (float)(Random.NextDouble() * 50),
            Level: Random.Next(1, 40),
            CanUseMagic: Random.Next(0, 2) == 1,
            Role: Roles[Random.Next(Roles.Length)],
            Speed: (float)(Random.NextDouble() * 8),
            Experience: Random.Next(100, 1000),
            IsBoss: Random.Next(0, 2) == 1,
            Faction: Factions[Random.Next(Factions.Length)],
            Luck: (float)(Random.NextDouble() * 5));
    }
}
