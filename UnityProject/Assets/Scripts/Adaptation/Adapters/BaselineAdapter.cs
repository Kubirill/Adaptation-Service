using AdaptationCore;

namespace AdaptationUnity.Adapters
{
    public sealed class BaselineAdapter : IAdaptationAdapter
    {
        public string AdapterName => "Baseline";

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            return AdapterDecisions.BuildDecision(sessionEvent, AdapterName, "baseline_noop");
        }
    }
}
