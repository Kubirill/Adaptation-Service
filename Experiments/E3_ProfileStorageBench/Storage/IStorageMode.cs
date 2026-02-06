using Experiments.E3_ProfileStorageBench.Models;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal interface IStorageMode : IDisposable
{
    string ModeName { get; }
    void Initialize();
    void Persist(SessionEvent sessionEvent, ProfileState profileState, bool snapshotBoundary);
    ProfileState Restore(int uptoSeq);
    long GetStorageSizeBytes();
}
