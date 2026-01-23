namespace AdaptationCore
{
    public interface IAdaptationAdapterWithAudit : IAdaptationAdapter
    {
        AdaptationDecision ComputeNext(AdaptationEvent sessionEvent, out AdaptationAuditRecord auditRecord);
    }
}
