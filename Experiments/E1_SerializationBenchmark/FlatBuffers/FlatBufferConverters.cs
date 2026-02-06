using System.Linq;
using Google.FlatBuffers;
using Adaptation.Experiments.Flatbuffers;
using E1_SerializationBenchmark.Data;

namespace E1_SerializationBenchmark.FlatBuffers;

internal static class FlatBufferConverters
{
    public static byte[] Serialize(EventData data)
    {
        var builder = new FlatBufferBuilder(4096);
        var sessionId = builder.CreateString(data.SessionId);
        var sceneId = builder.CreateString(data.SceneId);
        var configVersion = builder.CreateString(data.ConfigVersion);

        var featureOffsets = data.NpcFeatures
            .Select(feature => NpcFeature.CreateNpcFeature(
                builder,
                builder.CreateString(feature.Name),
                feature.Agility,
                feature.Strength,
                feature.Alertness,
                feature.IsHostile,
                builder.CreateString(feature.Classification),
                feature.Stamina,
                feature.Level,
                feature.CanUseMagic,
                builder.CreateString(feature.Role),
                feature.Speed,
                feature.Experience,
                feature.IsBoss,
                builder.CreateString(feature.Faction),
                feature.Luck))
            .ToArray();

        var paramOffsets = data.NpcParams
            .Select(param => NpcParam.CreateNpcParam(builder, builder.CreateString(param.Name), param.Value))
            .ToArray();

        var featureVector = EventPayload.CreateNpcFeaturesVector(builder, featureOffsets);
        var paramVector = EventPayload.CreateNpcParamsVector(builder, paramOffsets);

        var payload = EventPayload.CreateEventPayload(
            builder,
            sessionId,
            sceneId,
            data.ResultZ,
            data.TimeT,
            data.Attempts,
            data.Seed,
            configVersion,
            featureVector,
            paramVector);

        builder.Finish(payload.Value);
        return builder.SizedByteArray();
    }

    public static byte[] Serialize(DecisionData data)
    {
        var builder = new FlatBufferBuilder(4096);
        var nextSceneId = builder.CreateString(data.NextSceneId);
        var configVersion = builder.CreateString(data.ConfigVersion);

        var paramOffsets = data.NpcParams
            .Select(param => NpcParam.CreateNpcParam(builder, builder.CreateString(param.Name), param.Value))
            .ToArray();

        var explanationOffsets = data.Explanation
            .Select(explanation => ExplanationEntry.CreateExplanationEntry(
                builder,
                builder.CreateString(explanation.Key),
                builder.CreateString(explanation.Value)))
            .ToArray();

        var paramVector = DecisionPayload.CreateNpcParamsVector(builder, paramOffsets);
        var explanationVector = DecisionPayload.CreateExplanationVector(builder, explanationOffsets);

        var payload = DecisionPayload.CreateDecisionPayload(
            builder,
            nextSceneId,
            paramVector,
            explanationVector,
            data.Seed,
            configVersion);

        builder.Finish(payload.Value);
        return builder.SizedByteArray();
    }

    public static EventData DeserializeEvent(byte[] payload)
    {
        var buffer = new ByteBuffer(payload);
        var root = EventPayload.GetRootAsEventPayload(buffer);

        var features = Enumerable.Range(0, root.NpcFeaturesLength)
            .Select(i => root.NpcFeatures(i))
            .Where(item => item.HasValue)
            .Select(item => FromFlatFeature(item.Value))
            .ToList();

        var parameters = Enumerable.Range(0, root.NpcParamsLength)
            .Select(i => root.NpcParams(i))
            .Where(item => item.HasValue)
            .Select(item => FromFlatParam(item.Value))
            .ToList();

        return new EventData(
            SessionId: root.SessionId ?? string.Empty,
            SceneId: root.SceneId ?? string.Empty,
            ResultZ: root.ResultZ,
            TimeT: root.TimeT,
            Attempts: root.Attempts,
            Seed: root.Seed,
            ConfigVersion: root.ConfigVersion ?? string.Empty,
            NpcFeatures: features,
            NpcParams: parameters);
    }

    public static DecisionData DeserializeDecision(byte[] payload)
    {
        var buffer = new ByteBuffer(payload);
        var root = DecisionPayload.GetRootAsDecisionPayload(buffer);

        var parameters = Enumerable.Range(0, root.NpcParamsLength)
            .Select(i => root.NpcParams(i))
            .Where(item => item.HasValue)
            .Select(item => FromFlatParam(item.Value))
            .ToList();

        var explanation = Enumerable.Range(0, root.ExplanationLength)
            .Select(i => root.Explanation(i))
            .Where(item => item.HasValue)
            .Select(item => FromFlatExplanation(item.Value))
            .ToList();

        return new DecisionData(
            NextSceneId: root.NextSceneId ?? string.Empty,
            NpcParams: parameters,
            Explanation: explanation,
            Seed: root.Seed,
            ConfigVersion: root.ConfigVersion ?? string.Empty);
    }

    private static NpcFeatureData FromFlatFeature(NpcFeature flat)
    {
        return new NpcFeatureData(
            Name: flat.Name ?? string.Empty,
            Agility: flat.Agility,
            Strength: flat.Strength,
            Alertness: flat.Alertness,
            IsHostile: flat.IsHostile,
            Classification: flat.Classification ?? string.Empty,
            Stamina: flat.Stamina,
            Level: flat.Level,
            CanUseMagic: flat.CanUseMagic,
            Role: flat.Role ?? string.Empty,
            Speed: flat.Speed,
            Experience: flat.Experience,
            IsBoss: flat.IsBoss,
            Faction: flat.Faction ?? string.Empty,
            Luck: flat.Luck);
    }

    private static NpcParamData FromFlatParam(NpcParam flat)
    {
        return new NpcParamData(flat.Name ?? string.Empty, flat.Value);
    }

    private static ExplanationEntryData FromFlatExplanation(ExplanationEntry flat)
    {
        return new ExplanationEntryData(flat.Key ?? string.Empty, flat.Value ?? string.Empty);
    }
}
