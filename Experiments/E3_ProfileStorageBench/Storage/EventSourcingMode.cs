using System;
using System.IO;
using System.Linq;

using Experiments.E3_ProfileStorageBench.Models;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal sealed class EventSourcingMode : IStorageMode
{
    private readonly string _dbPath;
    private readonly SqliteEventStore _eventStore;

    public EventSourcingMode(string dbPath)
    {
        _dbPath = dbPath;
        _eventStore = new SqliteEventStore(dbPath);
    }

    public string ModeName => "S2-EventStream";

    public void Initialize()
    {
        _eventStore.Initialize();
    }

    public void Persist(SessionEvent sessionEvent, ProfileState profileState, bool snapshotBoundary)
    {
        _eventStore.Append(sessionEvent);
    }

    public ProfileState Restore(int uptoSeq)
    {
        var events = _eventStore.ReadEvents(uptoSeq);
        var state = ProfileState.CreateDefault();
        foreach (var evt in events)
        {
            state.Apply(evt);
        }

        return state;
    }

    public long GetStorageSizeBytes() => new FileInfo(_dbPath).Length;

    public void Dispose()
    {
        _eventStore.Dispose();
    }
}
