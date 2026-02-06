using System.IO;

using Experiments.E3_ProfileStorageBench.Models;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal sealed class HybridMode : IStorageMode
{
    private readonly string _dbPath;
    private readonly SqliteEventStore _eventStore;
    private readonly SqliteSnapshotStore _snapshotStore;
    private readonly int _snapshotEvery;

    public HybridMode(string dbPath, int snapshotEvery)
    {
        _dbPath = dbPath;
        _snapshotEvery = snapshotEvery;
        _eventStore = new SqliteEventStore(dbPath);
        _snapshotStore = new SqliteSnapshotStore(dbPath);
    }

    public string ModeName => "S3-Hybrid";

    public void Initialize()
    {
        _eventStore.Initialize();
        _snapshotStore.Initialize();
    }

    public void Persist(SessionEvent sessionEvent, ProfileState profileState, bool snapshotBoundary)
    {
        _eventStore.Append(sessionEvent);
        if (snapshotBoundary)
        {
            _snapshotStore.WriteSnapshot(profileState, sessionEvent.Seq);
        }
    }

    public ProfileState Restore(int uptoSeq)
    {
        var snapshot = _snapshotStore.ReadLatest(uptoSeq);
        var state = snapshot is not null
            ? ProfileState.FromJson(snapshot.SnapshotJson)
            : ProfileState.CreateDefault();

        var events = _eventStore.ReadEvents(uptoSeq);
        var startSeq = snapshot?.UptoSeq ?? 0;
        foreach (var evt in events)
        {
            if (evt.Seq <= startSeq)
            {
                continue;
            }

            state.Apply(evt);
        }

        return state;
    }

    public long GetStorageSizeBytes() => new FileInfo(_dbPath).Length;

    public void Dispose()
    {
        _eventStore.Dispose();
        _snapshotStore.Dispose();
    }
}
