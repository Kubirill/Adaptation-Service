using System.Collections.Generic;

namespace AdaptationCore
{
    public sealed class AdaptationAuditRecord
    {
        public AdaptationEvent inputs;
        public AdaptationDecision output;
        public string config_version_hash;
        public int seed;
        public Dictionary<string, float> intermediates = new Dictionary<string, float>();
    }
}
