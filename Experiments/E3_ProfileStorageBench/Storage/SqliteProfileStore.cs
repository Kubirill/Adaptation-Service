using System;
using System.IO;

using Experiments.E3_ProfileStorageBench.Models;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal sealed class SqliteProfileStore : SqliteStorageBase, IStorageMode
{
    private readonly string _dbPath;

    public SqliteProfileStore(string dbPath) : base(dbPath)
    {
        _dbPath = dbPath;
    }

    public string ModeName => "S1-CRUD";

    public void Initialize()
    {
        EnsureInitialized();
    }

    public void Persist(SessionEvent sessionEvent, ProfileState profileState, bool snapshotBoundary)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO Profile(profile_id, version, data_json, updated_at, config_version, content_version, rules_version, seed)
                            VALUES ($id, $version, $data, $updated, $cfg, $content, $rules, $seed)
                            ON CONFLICT(profile_id) DO UPDATE SET
                                version = $version,
                                data_json = $data,
                                updated_at = $updated,
                                config_version = $cfg,
                                content_version = $content,
                                rules_version = $rules,
                                seed = $seed;";

        cmd.Parameters.AddWithValue("$id", profileState.ProfileId);
        cmd.Parameters.AddWithValue("$version", sessionEvent.Seq);
        cmd.Parameters.AddWithValue("$data", profileState.ToJson());
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$cfg", profileState.ConfigVersion);
        cmd.Parameters.AddWithValue("$content", profileState.ContentVersion);
        cmd.Parameters.AddWithValue("$rules", profileState.RulesVersion);
        cmd.Parameters.AddWithValue("$seed", profileState.Seed);
        cmd.ExecuteNonQuery();
    }

    public ProfileState Restore(int uptoSeq)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT data_json FROM Profile LIMIT 1";
        var value = cmd.ExecuteScalar() as string;
        return string.IsNullOrEmpty(value) ? ProfileState.CreateDefault() : ProfileState.FromJson(value);
    }

    public long GetStorageSizeBytes()
    {
        return new FileInfo(_dbPath).Length;
    }

    protected override void CreateTables()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Profile (
                                profile_id TEXT PRIMARY KEY,
                                version INTEGER,
                                data_json TEXT,
                                updated_at TEXT,
                                config_version TEXT,
                                content_version TEXT,
                                rules_version TEXT,
                                seed INTEGER
                            );";
        cmd.ExecuteNonQuery();
    }
}
