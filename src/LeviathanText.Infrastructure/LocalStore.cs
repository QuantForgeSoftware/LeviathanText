using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure;

/// <summary>
/// SQLite + native-JSON local persistence (todo §4 "Storage and jobs"): metadata and
/// document payloads stored transactionally; the native project document (JSON) is the
/// record of record. Schema migrations are versioned by <see cref="SchemaVersion"/>; a
/// file written by a newer schema is refused, never silently upgraded.
/// </summary>
public sealed class LocalStore : IDisposable, IProjectStore
{
    public const int SchemaVersion = 1;

    private readonly SqliteConnection _connection;

    public LocalStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        _connection.Open();
        Migrate();
    }

    private void Migrate()
    {
        using var tx = _connection.BeginTransaction();
        var userVersion = _connection.CreateCommand();
        userVersion.CommandText = "PRAGMA user_version";
        var existing = Convert.ToInt32(userVersion.ExecuteScalar());

        if (existing > SchemaVersion)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.SchemaVersionUnsupported,
                Scope = "local-store",
                Details = new Dictionary<string, string>
                {
                    ["storeSchema"] = existing.ToString(),
                    ["supportedSchema"] = SchemaVersion.ToString(),
                },
                Message = "Store was created by a newer schema; refusing to open (downgrades are unsupported).",
            });

        if (existing < SchemaVersion)
        {
            var ddl = _connection.CreateCommand();
            ddl.Transaction = tx;
            ddl.CommandText = """
                CREATE TABLE IF NOT EXISTS projects (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    contract_version TEXT NOT NULL,
                    json TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS jobs (
                    id TEXT PRIMARY KEY,
                    type TEXT NOT NULL,
                    status TEXT NOT NULL,
                    idempotency_key TEXT UNIQUE,
                    retries INTEGER NOT NULL DEFAULT 0,
                    json TEXT NOT NULL
                );
                """;
            ddl.ExecuteNonQuery();
            var setVersion = _connection.CreateCommand();
            setVersion.Transaction = tx;
            setVersion.CommandText = $"PRAGMA user_version={SchemaVersion}";
            setVersion.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ---- projects ----

    public void SaveProject(NativeProject project)
    {
        var json = NativeJson.Save(project);
        using var tx = _connection.BeginTransaction();
        var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO projects (id, name, contract_version, json) VALUES ($id, $name, $cv, $json)
            ON CONFLICT(id) DO UPDATE SET name = $name, contract_version = $cv, json = $json
            """;
        cmd.Parameters.AddWithValue("$id", project.Id.ToString());
        cmd.Parameters.AddWithValue("$name", project.Name);
        cmd.Parameters.AddWithValue("$cv", ContractVersion.Current);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public NativeProject? LoadProject(EntityId id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        var json = cmd.ExecuteScalar() as string;
        return json is null ? null : NativeJson.Load(json);
    }

    public IReadOnlyList<(EntityId Id, string Name)> ListProjects()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM projects ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var list = new List<(EntityId, string)>();
        while (reader.Read())
            list.Add((EntityId.Parse(reader.GetString(0), EntityKind.Collection), reader.GetString(1)));
        return list;
    }

    // ---- jobs ----

    public void SaveJob(JobRecord job)
    {
        using var tx = _connection.BeginTransaction();
        var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO jobs (id, type, status, idempotency_key, retries, json) VALUES ($id, $type, $status, $key, $retries, $json)
            ON CONFLICT(id) DO UPDATE SET type = $type, status = $status, idempotency_key = $key, retries = $retries, json = $json
            """;
        cmd.Parameters.AddWithValue("$id", job.Id.ToString());
        cmd.Parameters.AddWithValue("$type", job.Type);
        cmd.Parameters.AddWithValue("$status", job.Status.ToString());
        cmd.Parameters.AddWithValue("$key", (object?)job.IdempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$retries", job.RetriesUsed);
        cmd.Parameters.AddWithValue("$json", ContractJson.ToJson(job));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public JobRecord? LoadJob(EntityId id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return (cmd.ExecuteScalar() as string) is { } json ? ContractJson.FromJson<JobRecord>(json) : null;
    }

    public JobRecord? FindJobByIdempotencyKey(string key)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs WHERE idempotency_key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return (cmd.ExecuteScalar() as string) is { } json ? ContractJson.FromJson<JobRecord>(json) : null;
    }

    /// <summary>All jobs in a given status, oldest first (worker claim queries).</summary>
    public IReadOnlyList<JobRecord> LoadJobsByStatus(JobStatus status)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs WHERE status = $status ORDER BY id";
        cmd.Parameters.AddWithValue("$status", status.ToString());
        using var reader = cmd.ExecuteReader();
        var jobs = new List<JobRecord>();
        while (reader.Read())
            jobs.Add(ContractJson.FromJson<JobRecord>(reader.GetString(0)));
        return jobs;
    }

    public void Dispose() => _connection.Dispose();
}
