using AdaptationCore;
using AdaptationUnity.Logging;

namespace AdaptationUnity.Adapters
{
    public sealed class EngineAdapterR3Grpc : IAdaptationAdapterWithAudit, IAdapterWithLogger, IAdapterSessionContext
    {
        public string AdapterName => "R3_remote_gRPC";

        private readonly GrpcClient _client;

        public EngineAdapterR3Grpc()
        {
            _client = new GrpcClient(RunConfig.Current);
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
