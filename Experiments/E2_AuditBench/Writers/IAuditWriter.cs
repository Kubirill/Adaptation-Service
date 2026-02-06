using Experiments.E2_AuditBench;

namespace Experiments.E2_AuditBench.Writers;

internal interface IAuditWriter : IDisposable
{
    string? LogFilePath { get; }
    long Write(SessionEventData sessionEvent, DecisionData decision, string configHash);
    void Flush();
}