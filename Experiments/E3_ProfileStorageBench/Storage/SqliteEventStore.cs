using System;
using System.Collections.Generic;

using Experiments.E3_ProfileStorageBench.Models;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal sealed class SqliteEventStore : SqliteStorageBase
{
    public SqliteEventStore(string dbPath) : base(dbPath)
    {
    }

    public void Initialize()
    {
        EnsureInitialized();
    }

    public void Append(SessionEvent sessionEvent)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO Events(profile_id, seq, ts, event_json, seed, config_version, content_version, rules_version)
                            VALUES ($profile, $seq, $ts, $json, $seed, $cfg, $content, $rules);";
        cmd.Parameters.AddWithValue("$profile", "learner");
        cmd.Parameters.AddWithValue("$seq", sessionEvent.Seq);
        cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$json", System.Text.Json.JsonSerializer.Serialize(sessionEvent, JsonDefaults.DefaultOptions));
        cmd.Parameters.AddWithValue("$seed", sessionEvent.Seed);
        cmd.Parameters.AddWithValue("$cfg", sessionEvent.ConfigVersion);
        cmd.Parameters.AddWithValue("$content", sessionEvent.ContentVersion);
        cmd.Parameters.AddWithValue("$rules", sessionEvent.RulesVersion);
        cmd.ExecuteNonQuery();
    }

    public List<SessionEvent> ReadEvents(int uptoSeq)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"SELECT event_json FROM Events
                            WHERE seq <= $upto
                            ORDER BY seq ASC;";
        cmd.Parameters.AddWithValue("$upto", uptoSeq);

        var list = new List<SessionEvent>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var json = reader.GetString(0);
            var evt = System.Text.Json.JsonSerializer.Deserialize<SessionEvent>(json, JsonDefaults.DefaultOptions);
            if (evt != null)
            {
                list.Add(evt);
            }
        }

        return list;
    }

    protected override void CreateTables()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Events (
                                event_id INTEGER PRIMARY KEY AUTOINCREMENT,
                                profile_id TEXT,
                                seq INTEGER,
                                ts TEXT,
                                event_json TEXT,
                                seed INTEGER,
                                config_version TEXT,
                                content_version TEXT,
                                rules_version TEXT
                            );";
        cmd.ExecuteNonQuery();
    }
}
