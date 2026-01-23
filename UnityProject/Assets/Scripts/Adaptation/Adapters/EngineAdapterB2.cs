using AdaptationCore;
using AdaptationUnity.Logging;

namespace AdaptationUnity.Adapters
{
    public sealed class EngineAdapterB2 : IAdaptationAdapterWithAudit, IAdapterWithLogger
    {
        public string AdapterName => "B2";

        private readonly ServiceClient _client;

        public EngineAdapterB2()
        {
            _client = new ServiceClient(RunConfig.Current);
        }

        public void SetLogger(SessionLogWriter logWriter)
        {
            _client.SetLogger(logWriter);
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            return ComputeNext(sessionEvent, out _);
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent, out AdaptationAuditRecord auditRecord)
        {
            return _client.ComputeNext(sessionEvent, out auditRecord);
        }
    }
}
