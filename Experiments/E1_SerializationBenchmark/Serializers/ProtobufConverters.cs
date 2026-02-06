using Adaptation.Experiments;
using PbEventPayload = global::Adaptation.Experiments.EventPayload;
using PbDecisionPayload = global::Adaptation.Experiments.DecisionPayload;
using PbNpcFeature = global::Adaptation.Experiments.NpcFeature;
using PbNpcParam = global::Adaptation.Experiments.NpcParam;
using PbExplanationEntry = global::Adaptation.Experiments.ExplanationEntry;
using E1_SerializationBenchmark.Data;
using System.Linq;
using Google.Protobuf;

namespace E1_SerializationBenchmark.Serializers;

internal static class ProtobufConverters
{
    public static byte[] Serialize(EventData eventData)
    {
        return ToProto(eventData).ToByteArray();
    }

    public static byte[] Serialize(DecisionData decisionData)
    {
        return ToProto(decisionData).ToByteArray();
    }

    public static EventData DeserializeEvent(byte[] payload)
    {
        var proto = PbEventPayload.Parser.ParseFrom(payload);
        return FromProto(proto);
    }

    public static DecisionData DeserializeDecision(byte[] payload)
    {
        var proto = PbDecisionPayload.Parser.ParseFrom(payload);
        return FromProto(proto);
    }

    private static PbEventPayload ToProto(EventData data)
    {
        var payload = new PbEventPayload
        {
            SessionId = data.SessionId,
            SceneId = data.SceneId,
            ResultZ = data.ResultZ,
            TimeT = data.TimeT,
            Attempts = data.Attempts,
            Seed = data.Seed,
            ConfigVersion = data.ConfigVersion
        };

        payload.NpcFeatures.AddRange(data.NpcFeatures.Select(ToProto));
        payload.NpcParams.AddRange(data.NpcParams.Select(ToProto));
        return payload;
    }

    private static PbDecisionPayload ToProto(DecisionData data)
    {
        var payload = new PbDecisionPayload
        {
            NextSceneId = data.NextSceneId,
            Seed = data.Seed,
            ConfigVersion = data.ConfigVersion
        };

        payload.NpcParams.AddRange(data.NpcParams.Select(ToProto));
        payload.Explanation.AddRange(data.Explanation.Select(ToProto));
        return payload;
    }

    private static PbNpcFeature ToProto(NpcFeatureData data)
    {
        return new NpcFeature
        {
            Name = data.Name,
            Agility = data.Agility,
            Strength = data.Strength,
            Alertness = data.Alertness,
            IsHostile = data.IsHostile,
            Classification = data.Classification,
            Stamina = data.Stamina,
            Level = data.Level,
            CanUseMagic = data.CanUseMagic,
            Role = data.Role,
            Speed = data.Speed,
            Experience = data.Experience,
            IsBoss = data.IsBoss,
            Faction = data.Faction,
            Luck = data.Luck
        };
    }

    private static PbNpcParam ToProto(NpcParamData data)
    {
        return new NpcParam
        {
            Name = data.Name,
            Value = data.Value
        };
    }

    private static PbExplanationEntry ToProto(ExplanationEntryData data)
    {
        return new ExplanationEntry
        {
            Key = data.Key,
            Value = data.Value
        };
    }

    private static EventData FromProto(PbEventPayload proto)
    {
        var features = proto.NpcFeatures.Select(FromProto).ToList();
        var paramsList = proto.NpcParams.Select(FromProto).ToList();

        return new EventData(
            SessionId: proto.SessionId,
            SceneId: proto.SceneId,
            ResultZ: proto.ResultZ,
            TimeT: proto.TimeT,
            Attempts: proto.Attempts,
            Seed: proto.Seed,
            ConfigVersion: proto.ConfigVersion,
            NpcFeatures: features,
            NpcParams: paramsList);
    }

    private static DecisionData FromProto(PbDecisionPayload proto)
    {
        var paramsList = proto.NpcParams.Select(FromProto).ToList();
        var explanation = proto.Explanation.Select(FromProto).ToList();

        return new DecisionData(
            NextSceneId: proto.NextSceneId,
            NpcParams: paramsList,
            Explanation: explanation,
            Seed: proto.Seed,
            ConfigVersion: proto.ConfigVersion);
    }

    private static NpcFeatureData FromProto(PbNpcFeature proto)
    {
        return new NpcFeatureData(
            Name: proto.Name,
            Agility: proto.Agility,
            Strength: proto.Strength,
            Alertness: proto.Alertness,
            IsHostile: proto.IsHostile,
            Classification: proto.Classification,
            Stamina: proto.Stamina,
            Level: proto.Level,
            CanUseMagic: proto.CanUseMagic,
            Role: proto.Role,
            Speed: proto.Speed,
            Experience: proto.Experience,
            IsBoss: proto.IsBoss,
            Faction: proto.Faction,
            Luck: proto.Luck);
    }

    private static NpcParamData FromProto(PbNpcParam proto)
    {
        return new NpcParamData(proto.Name, proto.Value);
    }

    private static ExplanationEntryData FromProto(PbExplanationEntry proto)
    {
        return new ExplanationEntryData(proto.Key, proto.Value);
    }
}
