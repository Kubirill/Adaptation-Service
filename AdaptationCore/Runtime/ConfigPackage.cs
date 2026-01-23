using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AdaptationCore
{
    [DataContract]
    public sealed class ConfigPackage
    {
        [DataMember(Name = "version")]
        public string version;

        public string version_hash;

        [DataMember(Name = "npc_base_params")]
        public Dictionary<string, float> npc_base_params = new Dictionary<string, float>();

        [DataMember(Name = "result_weight")]
        public float result_weight = 0.6f;

        [DataMember(Name = "time_weight")]
        public float time_weight = 0.4f;

        [DataMember(Name = "time_scale")]
        public float time_scale = 0.1f;
    }
}
