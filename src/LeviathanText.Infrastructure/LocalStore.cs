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
    public const int SchemaVersion = 4;

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
                CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL UNIQUE,
                    role TEXT NOT NULL,
                    key_hash TEXT NOT NULL UNIQUE,
                    created_at TEXT NOT NULL,
                    disabled INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS auth_audit (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    at TEXT NOT NULL,
                    user_id TEXT,
                    user_name TEXT,
                    role TEXT,
                    action TEXT NOT NULL,
                    method TEXT NOT NULL,
                    path TEXT NOT NULL,
                    status INTEGER
                );
                CREATE TABLE IF NOT EXISTS project_members (
                    project_id TEXT NOT NULL,
                    user_id TEXT NOT NULL,
                    added_at TEXT NOT NULL,
                    PRIMARY KEY (project_id, user_id)
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

    // ---- users (shared deployments) ----

    public UserAccount CreateUser(string name, UserRole role, string keyHash)
    {
        var id = $"usr-{Guid.NewGuid().ToString("N")[..12]}";
        var createdAt = DateTimeOffset.UtcNow;
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (id, name, role, key_hash, created_at, disabled)
            VALUES ($id, $name, $role, $hash, $created, 0)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$role", role.ToString());
        cmd.Parameters.AddWithValue("$hash", keyHash);
        cmd.Parameters.AddWithValue("$created", createdAt.ToString("O"));
        try { cmd.ExecuteNonQuery(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = name,
                Message = "A user with that name or key already exists.",
            });
        }
        return new UserAccount { Id = id, Name = name, Role = role, CreatedAt = createdAt };
    }

    public IReadOnlyList<UserAccount> LoadUsers()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, role, created_at, disabled FROM users ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var users = new List<UserAccount>();
        while (reader.Read())
        {
            users.Add(new UserAccount
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Role = Enum.Parse<UserRole>(reader.GetString(2)),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
                Disabled = reader.GetInt64(4) != 0,
            });
        }
        return users;
    }

    /// <summary>Authenticate a key hash; disabled accounts are treated as absent.</summary>
    public UserAccount? FindUserByKeyHash(string keyHash)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, role, created_at, disabled FROM users WHERE key_hash = $hash AND disabled = 0";
        cmd.Parameters.AddWithValue("$hash", keyHash);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new UserAccount
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            Role = Enum.Parse<UserRole>(reader.GetString(2)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    public void DisableUser(string id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET disabled = 1 WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() == 0)
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = id, Message = "No such user." });
    }

    public void UpdateUserKey(string id, string keyHash)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET key_hash = $hash WHERE id = $id";
        cmd.Parameters.AddWithValue("$hash", keyHash);
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() == 0)
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = id, Message = "No such user." });
    }

    // ---- project membership ----

    public void AddProjectMember(EntityId projectId, string userId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO project_members (project_id, user_id, added_at) VALUES ($project, $user, $at)
            ON CONFLICT(project_id, user_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$user", userId);
        cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void RemoveProjectMember(EntityId projectId, string userId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM project_members WHERE project_id = $project AND user_id = $user";
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$user", userId);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<string> LoadProjectMembers(EntityId projectId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT user_id FROM project_members WHERE project_id = $project ORDER BY user_id";
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = cmd.ExecuteReader();
        var members = new List<string>();
        while (reader.Read()) members.Add(reader.GetString(0));
        return members;
    }

    public bool IsProjectMember(EntityId projectId, string userId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM project_members WHERE project_id = $project AND user_id = $user LIMIT 1";
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$user", userId);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>Find the project containing a line/surface/region/annotation id (membership checks).</summary>
    public EntityId? FindProjectForEntity(EntityId entityId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, json FROM projects";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var project = NativeJson.Load(reader.GetString(1));
            bool found = project.Objects.SelectMany(o => o.Surfaces).Any(s =>
                s.Id == entityId || s.Lines.Any(l => l.Id == entityId) || s.Regions.Any(r => r.Id == entityId)
                || s.Annotations.Any(a => a.Id == entityId));
            if (found) return project.Id;
        }
        return null;
    }

    // ---- auth audit ----

    public void AppendAuthAudit(AuthAuditEntry entry)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO auth_audit (at, user_id, user_name, role, action, method, path, status)
            VALUES ($at, $uid, $name, $role, $action, $method, $path, $status)
            """;
        cmd.Parameters.AddWithValue("$at", entry.At.ToString("O"));
        cmd.Parameters.AddWithValue("$uid", (object?)entry.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", (object?)entry.UserName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$role", entry.Role is { } role ? role.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("$action", entry.Action);
        cmd.Parameters.AddWithValue("$method", entry.Method);
        cmd.Parameters.AddWithValue("$path", entry.Path);
        cmd.Parameters.AddWithValue("$status", (object?)entry.Status ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AuthAuditEntry> LoadAuthAudit(int limit = 100)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, at, user_id, user_name, role, action, method, path, status
            FROM auth_audit ORDER BY id DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        using var reader = cmd.ExecuteReader();
        var entries = new List<AuthAuditEntry>();
        while (reader.Read())
        {
            entries.Add(new AuthAuditEntry
            {
                Id = reader.GetInt64(0),
                At = DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
                UserName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Role = reader.IsDBNull(4) ? null : Enum.Parse<UserRole>(reader.GetString(4)),
                Action = reader.GetString(5),
                Method = reader.GetString(6),
                Path = reader.GetString(7),
                Status = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            });
        }
        return entries;
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

    public void DeleteProject(EntityId id)
    {
        using var tx = _connection.BeginTransaction();
        var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        var removed = cmd.ExecuteNonQuery();
        tx.Commit();
        if (removed == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = id.ToString(),
                Message = "No such project.",
            });
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

    /// <summary>Most recent jobs first (bounded list for operators and the workspace).</summary>
    public IReadOnlyList<JobRecord> LoadJobs(int limit = 100)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs ORDER BY rowid DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        using var reader = cmd.ExecuteReader();
        var jobs = new List<JobRecord>();
        while (reader.Read())
            jobs.Add(ContractJson.FromJson<JobRecord>(reader.GetString(0)));
        return jobs;
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

    /// <summary>
    /// Consistent database backup via SQLite's VACUUM INTO (safe while the connection is
    /// open; the target must not exist). Used by the CLI backup/restore workflow.
    /// </summary>
    public void BackupTo(string targetDatabasePath)
    {
        var full = Path.GetFullPath(targetDatabasePath);
        if (File.Exists(full)) File.Delete(full);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var cmd = _connection.CreateCommand();
        cmd.CommandText = "VACUUM INTO $target";
        cmd.Parameters.AddWithValue("$target", full);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
