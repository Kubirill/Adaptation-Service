using AdaptationCore;
using UnityEngine;

namespace AdaptationUnity.Adapters
{
    public sealed class B2JsonRoundTripAdapter : IAdaptationAdapter
    {
        public string AdapterName => "B2";

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            var json = JsonUtility.ToJson(sessionEvent);
            var roundTrip = JsonUtility.FromJson<AdaptationEvent>(json);
            return AdapterDecisions.BuildDecision(roundTrip, AdapterName, "json_round_trip");
        }
    }
}
