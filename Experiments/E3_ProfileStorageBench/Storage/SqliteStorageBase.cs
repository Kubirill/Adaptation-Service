using Microsoft.Data.Sqlite;

namespace Experiments.E3_ProfileStorageBench.Storage;

internal abstract class SqliteStorageBase : IDisposable
{
    protected readonly SqliteConnection Connection;
    private bool _initialized;

    protected SqliteStorageBase(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        Connection = new SqliteConnection(builder.ConnectionString);
        Connection.Open();
    }

    protected void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        CreateTables();
        _initialized = true;
    }

    protected abstract void CreateTables();

    public void Dispose()
    {
        Connection.Dispose();
    }

    protected long ExecuteScalarLong(string sql)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return result is long value ? value : 0;
    }
}
