using AdaptationCore;
using AdaptationUnity.Logging;

namespace AdaptationUnity.Adapters
{
    public sealed class EngineAdapterR4BrokerRemote : IAdaptationAdapterWithAudit, IAdapterWithLogger, IAdapterSessionContext
    {
        public string AdapterName => "R4_remote_BrokerRPC";

        private readonly BrokerClient _client;

        public EngineAdapterR4BrokerRemote()
        {
            _client = new BrokerClient(RunConfig.Current);
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

        public void SetSessionContext(string sessionId, int sessionIndex, bool warmup)
        {
            _client.SetSessionContext(sessionId, sessionIndex, warmup);
        }
    }
}
