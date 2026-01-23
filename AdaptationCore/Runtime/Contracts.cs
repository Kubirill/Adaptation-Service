using System;
using System.Collections.Generic;

namespace AdaptationCore
{
    [Serializable]
    public sealed class AdaptationEvent
    {
        public string session_id;
        public string scene_id;
        public float result_z;
        public float time_t;
        public int? attempts_a;
        public int seed;
        public string config_version;
    }

    [Serializable]
    public sealed class AdaptationDecision
    {
        public string next_scene_id;
        public List<NpcParam> npc_params = new List<NpcParam>();
        public List<ExplanationEntry> explanation = new List<ExplanationEntry>();
        public int seed;
        public string config_version;
    }

    [Serializable]
    public sealed class NpcParam
    {
        public string name;
        public float value;
    }

    [Serializable]
    public sealed class ExplanationEntry
    {
        public string name;
        public string value;
    }
}
