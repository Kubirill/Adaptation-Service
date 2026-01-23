namespace AdaptationCore
{
    public interface IAdaptationAdapter
    {
        string AdapterName { get; }
        AdaptationDecision ComputeNext(AdaptationEvent sessionEvent);
    }
}
