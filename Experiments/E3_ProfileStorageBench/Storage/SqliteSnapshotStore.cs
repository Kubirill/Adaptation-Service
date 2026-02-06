using System;
using System.IO;

using Experiments.E3_ProfileStorageBench.Models;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal sealed class SqliteSnapshotStore : SqliteStorageBase
{
    private readonly string _dbPath;

    public SqliteSnapshotStore(string dbPath) : base(dbPath)
    {
        _dbPath = dbPath;
    }

    public void Initialize()
    {
        EnsureInitialized();
    }

    public void WriteSnapshot(ProfileState profileState, int uptoSeq)
    {
        var json = profileState.ToJson();
        var hash = SnapshotHash(json);
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO Snapshots(profile_id, upto_seq, snapshot_json, snapshot_hash, ts, rules_version, content_version)
                            VALUES ($profile, $seq, $json, $hash, $ts, $rules, $content)
                            ON CONFLICT(profile_id) DO UPDATE SET
                                upto_seq = $seq,
                                snapshot_json = $json,
                                snapshot_hash = $hash,
                                ts = $ts,
                                rules_version = $rules,
                                content_version = $content;";

        cmd.Parameters.AddWithValue("$profile", profileState.ProfileId);
        cmd.Parameters.AddWithValue("$seq", uptoSeq);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$rules", profileState.RulesVersion);
        cmd.Parameters.AddWithValue("$content", profileState.ContentVersion);
        cmd.ExecuteNonQuery();
    }

    public SnapshotMetadata? ReadLatest(int uptoSeq)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"SELECT upto_seq, snapshot_json, snapshot_hash, rules_version, content_version
                            FROM Snapshots
                            WHERE upto_seq <= $upto
                            ORDER BY upto_seq DESC
                            LIMIT 1;";
        cmd.Parameters.AddWithValue("$upto", uptoSeq);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SnapshotMetadata
        {
            UptoSeq = reader.GetInt32(0),
            SnapshotJson = reader.GetString(1),
            SnapshotHash = reader.GetString(2),
            RulesVersion = reader.GetString(3),
            ContentVersion = reader.GetString(4)
        };
    }

    protected override void CreateTables()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Snapshots (
                                profile_id TEXT PRIMARY KEY,
                                upto_seq INTEGER,
                                snapshot_json TEXT,
                                snapshot_hash TEXT,
                                ts TEXT,
                                rules_version TEXT,
                                content_version TEXT
                            );";
        cmd.ExecuteNonQuery();
    }

    private static string SnapshotHash(string json)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}

internal sealed class SnapshotMetadata
{
    public int UptoSeq { get; init; }
    public string SnapshotJson { get; init; } = string.Empty;
    public string SnapshotHash { get; init; } = string.Empty;
    public string RulesVersion { get; init; } = string.Empty;
    public string ContentVersion { get; init; } = string.Empty;
}
