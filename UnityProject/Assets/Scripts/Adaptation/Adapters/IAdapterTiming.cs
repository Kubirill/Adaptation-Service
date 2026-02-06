namespace AdaptationUnity.Adapters
{
    public interface IAdapterTiming
    {
        bool TryGetLastTiming(out double netMs, out double localMs, out double decisionMs);
    }
}
