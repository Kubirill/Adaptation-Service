using AdaptationCore;

namespace AdaptationUnity.Adapters
{
    public sealed class BaselineAdapter : IAdaptationAdapter, IAdapterTiming
    {
        private double _lastDecisionMs;

        public string AdapterName => "Baseline";

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            using (DecisionBuildTiming.Begin(ms => _lastDecisionMs = ms))
            {
                return AdapterDecisions.BuildDecision(sessionEvent, AdapterName, "baseline_noop");
            }
        }

        public bool TryGetLastTiming(out double netMs, out double localMs, out double decisionMs)
        {
            netMs = 0;
            localMs = 0;
            decisionMs = _lastDecisionMs;
            var hasValue = decisionMs > 0;
            _lastDecisionMs = 0;
            return hasValue;
        }
    }
}
