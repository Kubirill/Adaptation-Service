using AdaptationUnity.Logging;

namespace AdaptationUnity.Adapters
{
    public interface IAdapterWithLogger
    {
        void SetLogger(SessionLogWriter logWriter);
    }
}
