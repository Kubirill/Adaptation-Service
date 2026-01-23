namespace AdaptationUnity.Adapters
{
    public interface IAdapterSessionContext
    {
        void SetSessionContext(string sessionId, int sessionIndex, bool warmup);
    }
}
