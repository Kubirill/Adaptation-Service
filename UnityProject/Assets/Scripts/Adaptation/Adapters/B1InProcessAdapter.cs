using AdaptationCore;

namespace AdaptationUnity.Adapters
{
    public sealed class B1InProcessAdapter : IAdaptationAdapter
    {
        public string AdapterName => "B1";

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            return AdapterDecisions.BuildDecision(sessionEvent, AdapterName, "direct_in_process");
        }
    }
}
