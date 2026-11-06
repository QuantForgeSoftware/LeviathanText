using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure;

/// <summary>
/// SQLite + native-JSON local persistence (project-plan §4 "Storage and jobs"): metadata and
/// document payloads stored transactionally; the native project document (JSON) is the
/// record of record. Schema migrations are versioned by <see cref="SchemaVersion"/>; a
/// file written by a newer schema is refused, never silently upgraded.
/// </summary>
public sealed class LocalStore : IDisposable, IProjectStore, IPresenceStore, ISessionStore
{
    public const int SchemaVersion = 9;

    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly Lock _writes;

    public LocalStore(string databasePath) : this(CreateConnectionString(databasePath), initialize: true, new Lock())
    {
    }

    /// <summary>
    /// Open an existing current-schema store without creating directories, migrating its
    /// schema, or repairing persisted versions. Read-only connections also reject writes
    /// through independently opened peers. Use the normal constructor for initialization.
    /// </summary>
    public static LocalStore OpenExisting(string databasePath, bool readOnly = false) => new(
        new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString(), initialize: false, new Lock());

    private LocalStore(string connectionString, bool initialize, Lock writes)
    {
        _connectionString = connectionString;
        _writes = writes;
        _connection = new SqliteConnection(connectionString);
        try
        {
            _connection.Open();
            if (initialize)
            {
                Migrate();
            }
            else
            {
                // Let SQLite wait briefly for ordinary file contention before the
                // provider falls back to its coarse busy-retry interval.
                using var busy = _connection.CreateCommand();
                busy.CommandText = "PRAGMA busy_timeout = 50";
                busy.ExecuteNonQuery();
                var version = ReadSchemaVersion();
                if (version != SchemaVersion)
                    throw UnsupportedSchema(version, "Existing connections require an initialized store with the current schema.");
            }
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    private static string CreateConnectionString(string databasePath)
    {
        if (databasePath == ":memory:")
        {
            // A unique shared name keeps independently constructed stores isolated
            // while allowing peer connections to use the same in-memory database.
            return new SqliteConnectionStringBuilder
            {
                DataSource = $"leviathan-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
            }.ToString();
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    /// <summary>Open an independent connection to the already initialized database.</summary>
    public LocalStore OpenPeer()
    {
        var settings = new SqliteConnectionStringBuilder(_connectionString);
        if (settings.Mode is not (SqliteOpenMode.Memory or SqliteOpenMode.ReadOnly))
            settings.Mode = SqliteOpenMode.ReadWrite;
        return new LocalStore(settings.ToString(), initialize: false, _writes);
    }

    private int ReadSchemaVersion(SqliteTransaction? transaction = null)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void Migrate()
    {
        using var tx = _connection.BeginTransaction();
        var existing = ReadSchemaVersion(tx);

        if (existing > SchemaVersion)
            throw UnsupportedSchema(existing, "Store was created by a newer schema; refusing to open (downgrades are unsupported).");

        if (existing < SchemaVersion)
        {
            using var ddl = _connection.CreateCommand();
            ddl.Transaction = tx;
            ddl.CommandText = """
                CREATE TABLE IF NOT EXISTS projects (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    contract_version TEXT NOT NULL,
                    json TEXT NOT NULL,
                    version INTEGER NOT NULL DEFAULT 1
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
                CREATE TABLE IF NOT EXISTS runs (
                    id TEXT PRIMARY KEY,
                    kind TEXT NOT NULL,
                    name TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    created_by TEXT,
                    dataset_digest TEXT,
                    config_json TEXT NOT NULL,
                    metrics_json TEXT NOT NULL,
                    history_json TEXT NOT NULL,
                    artifacts_json TEXT NOT NULL,
                    notes TEXT
                );
                CREATE TABLE IF NOT EXISTS presence (
                    surface_id TEXT NOT NULL,
                    editor_id TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    PRIMARY KEY (surface_id, editor_id)
                );
                CREATE TABLE IF NOT EXISTS invites (
                    id TEXT PRIMARY KEY,
                    token_hash TEXT NOT NULL UNIQUE,
                    project_id TEXT NOT NULL,
                    role TEXT NOT NULL,
                    created_by TEXT,
                    created_at TEXT NOT NULL,
                    expires_at TEXT NOT NULL,
                    used_at TEXT,
                    used_by TEXT
                );
                CREATE TABLE IF NOT EXISTS edit_sessions (
                    id TEXT PRIMARY KEY,
                    user_id TEXT NOT NULL,
                    user_name TEXT NOT NULL,
                    node_id TEXT NOT NULL,
                    surface_id TEXT,
                    created_at TEXT NOT NULL,
                    last_seen_at TEXT NOT NULL,
                    expires_at TEXT NOT NULL,
                    revoked_at TEXT
                );
                """;
            ddl.ExecuteNonQuery();
            if (existing >= 1 && !ColumnExists("projects", "version"))
            {
                using var alter = _connection.CreateCommand();
                alter.Transaction = tx;
                alter.CommandText = "ALTER TABLE projects ADD COLUMN version INTEGER NOT NULL DEFAULT 1";
                alter.ExecuteNonQuery();
            }
            using var setVersion = _connection.CreateCommand();
            setVersion.Transaction = tx;
            setVersion.CommandText = $"PRAGMA user_version={SchemaVersion}";
            setVersion.ExecuteNonQuery();
        }
        // Earlier migrations left persisted rows at zero, which is reserved for
        // unversioned callers. Repair those rows even when the schema is already current
        // so every loaded document participates in the optimistic concurrency check.
        using var repairVersions = _connection.CreateCommand();
        repairVersions.Transaction = tx;
        repairVersions.CommandText = "UPDATE projects SET version = 1 WHERE version = 0";
        repairVersions.ExecuteNonQuery();
        tx.Commit();
    }

    private static LeviathanException UnsupportedSchema(int version, string message) => new(new LeviathanError
    {
        Code = ErrorCode.SchemaVersionUnsupported,
        Scope = "local-store",
        Details = new Dictionary<string, string>
        {
            ["storeSchema"] = version.ToString(),
            ["supportedSchema"] = SchemaVersion.ToString(),
        },
        Message = message,
    });

    // Request peers own their connections. Serialize only the short mutating SQL
    // across peers in this process so they do not spin in the provider's busy retry.
    private int ExecuteWrite(SqliteCommand command)
    {
        lock (_writes) return command.ExecuteNonQuery();
    }

    // ---- users (shared deployments) ----

    public UserAccount CreateUser(string name, UserRole role, string keyHash)
    {
        var id = $"usr-{Guid.NewGuid().ToString("N")[..12]}";
        var createdAt = DateTimeOffset.UtcNow;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (id, name, role, key_hash, created_at, disabled)
            VALUES ($id, $name, $role, $hash, $created, 0)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$role", role.ToString());
        cmd.Parameters.AddWithValue("$hash", keyHash);
        cmd.Parameters.AddWithValue("$created", createdAt.ToString("O"));
        try { ExecuteWrite(cmd); }
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
        using var cmd = _connection.CreateCommand();
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

    /// <summary>Whether any account exists, including disabled accounts.</summary>
    public bool HasUsers()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM users LIMIT 1";
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>Find a known principal by its exact ID, retaining disabled state for reauthorization.</summary>
    public UserAccount? FindUserById(string id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, role, created_at, disabled FROM users WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new UserAccount
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            Role = Enum.Parse<UserRole>(reader.GetString(2)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
            Disabled = reader.GetInt64(4) != 0,
        };
    }

    /// <summary>Authenticate a key hash; disabled accounts are treated as absent.</summary>
    public UserAccount? FindUserByKeyHash(string keyHash)
    {
        using var cmd = _connection.CreateCommand();
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
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET disabled = 1 WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        if (ExecuteWrite(cmd) == 0)
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = id, Message = "No such user." });
    }

    public void UpdateUserKey(string id, string keyHash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET key_hash = $hash WHERE id = $id";
        cmd.Parameters.AddWithValue("$hash", keyHash);
        cmd.Parameters.AddWithValue("$id", id);
        if (ExecuteWrite(cmd) == 0)
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = id, Message = "No such user." });
    }

    // ---- project membership ----

    public void AddProjectMember(EntityId projectId, string userId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO project_members (project_id, user_id, added_at) VALUES ($project, $user, $at)
            ON CONFLICT(project_id, user_id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$user", userId);
        cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        ExecuteWrite(cmd);
    }

    public void RemoveProjectMember(EntityId projectId, string userId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM project_members WHERE project_id = $project AND user_id = $user";
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$user", userId);
        ExecuteWrite(cmd);
    }

    public IReadOnlyList<string> LoadProjectMembers(EntityId projectId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT user_id FROM project_members WHERE project_id = $project ORDER BY user_id";
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = cmd.ExecuteReader();
        var members = new List<string>();
        while (reader.Read()) members.Add(reader.GetString(0));
        return members;
    }

    public bool IsProjectMember(EntityId projectId, string userId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM project_members WHERE project_id = $project AND user_id = $user LIMIT 1";
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$user", userId);
        return cmd.ExecuteScalar() is not null;
    }

    public bool ProjectExists(EntityId projectId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", projectId.ToString());
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>Find the unique project containing an indirect entity ID.</summary>
    public EntityId? FindProjectForEntity(EntityId entityId) => FindProjectForEntity(entityId, out _);

    public EntityId? FindProjectForEntity(EntityId entityId, out bool ambiguous)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, json FROM projects";
        using var reader = cmd.ExecuteReader();
        EntityId? match = null;
        ambiguous = false;
        while (reader.Read())
        {
            var project = NativeJson.Load(reader.GetString(1));
            bool found = project.Objects.SelectMany(o => o.Surfaces).Any(s =>
                s.Id == entityId || s.Lines.Any(l => l.Id == entityId) || s.Regions.Any(r => r.Id == entityId)
                || s.Annotations.Any(a => a.Id == entityId));
            if (!found) continue;
            if (match is not null)
            {
                ambiguous = true;
                return null;
            }
            match = project.Id;
        }
        return match;
    }

    // ---- experiment runs ----

    public void SaveRun(RunRecord run)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO runs (id, kind, name, created_at, created_by, dataset_digest,
                              config_json, metrics_json, history_json, artifacts_json, notes)
            VALUES ($id, $kind, $name, $created, $by, $digest, $config, $metrics, $history, $artifacts, $notes)
            ON CONFLICT(id) DO UPDATE SET kind = $kind, name = $name, created_at = $created,
                created_by = $by, dataset_digest = $digest, config_json = $config,
                metrics_json = $metrics, history_json = $history, artifacts_json = $artifacts, notes = $notes
            """;
        cmd.Parameters.AddWithValue("$id", run.Id);
        cmd.Parameters.AddWithValue("$kind", run.Kind);
        cmd.Parameters.AddWithValue("$name", run.Name);
        cmd.Parameters.AddWithValue("$created", run.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$by", (object?)run.CreatedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$digest", (object?)run.DatasetDigest ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$config", ContractJson.ToJson(run.Config));
        cmd.Parameters.AddWithValue("$metrics", ContractJson.ToJson(run.Metrics));
        cmd.Parameters.AddWithValue("$history", ContractJson.ToJson(run.History));
        cmd.Parameters.AddWithValue("$artifacts", ContractJson.ToJson(run.Artifacts));
        cmd.Parameters.AddWithValue("$notes", (object?)run.Notes ?? DBNull.Value);
        ExecuteWrite(cmd);
    }

    public IReadOnlyList<RunRecord> LoadRuns(string? kind = null, int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = kind is null
            ? "SELECT id, kind, name, created_at, created_by, dataset_digest, config_json, metrics_json, history_json, artifacts_json, notes FROM runs ORDER BY created_at DESC LIMIT $limit"
            : "SELECT id, kind, name, created_at, created_by, dataset_digest, config_json, metrics_json, history_json, artifacts_json, notes FROM runs WHERE kind = $kind ORDER BY created_at DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        if (kind is not null) cmd.Parameters.AddWithValue("$kind", kind);
        using var reader = cmd.ExecuteReader();
        var runs = new List<RunRecord>();
        while (reader.Read()) runs.Add(ReadRun(reader));
        return runs;
    }

    public RunRecord? LoadRun(string id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, kind, name, created_at, created_by, dataset_digest, config_json, metrics_json, history_json, artifacts_json, notes FROM runs WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadRun(reader) : null;
    }

    private static RunRecord ReadRun(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Kind = reader.GetString(1),
        Name = reader.GetString(2),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
        CreatedBy = reader.IsDBNull(4) ? null : reader.GetString(4),
        DatasetDigest = reader.IsDBNull(5) ? null : reader.GetString(5),
        Config = ContractJson.FromJson<Dictionary<string, string>>(reader.GetString(6)),
        Metrics = ContractJson.FromJson<Dictionary<string, double>>(reader.GetString(7)),
        History = ContractJson.FromJson<List<double>>(reader.GetString(8)),
        Artifacts = ContractJson.FromJson<List<string>>(reader.GetString(9)),
        Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
    };

    // ---- advisory presence ----

    public void TouchPresence(EntityId surfaceId, string editorId, DateTimeOffset at)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO presence (surface_id, editor_id, updated_at) VALUES ($surface, $editor, $at)
            ON CONFLICT(surface_id, editor_id) DO UPDATE SET updated_at = $at
            """;
        cmd.Parameters.AddWithValue("$surface", surfaceId.ToString());
        cmd.Parameters.AddWithValue("$editor", editorId);
        cmd.Parameters.AddWithValue("$at", at.ToString("O"));
        ExecuteWrite(cmd);
    }

    public IReadOnlyList<string> ListPresence(EntityId surfaceId, DateTimeOffset since)
    {
        using var prune = _connection.CreateCommand();
        prune.CommandText = "DELETE FROM presence WHERE surface_id = $surface AND updated_at < $since";
        prune.Parameters.AddWithValue("$surface", surfaceId.ToString());
        prune.Parameters.AddWithValue("$since", since.ToString("O"));
        ExecuteWrite(prune);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT editor_id FROM presence WHERE surface_id = $surface ORDER BY editor_id";
        cmd.Parameters.AddWithValue("$surface", surfaceId.ToString());
        using var reader = cmd.ExecuteReader();
        var editors = new List<string>();
        while (reader.Read()) editors.Add(reader.GetString(0));
        return editors;
    }

    public void LeavePresence(EntityId surfaceId, string editorId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM presence WHERE surface_id = $surface AND editor_id = $editor";
        cmd.Parameters.AddWithValue("$surface", surfaceId.ToString());
        cmd.Parameters.AddWithValue("$editor", editorId);
        ExecuteWrite(cmd);
    }

    // ---- editing sessions (advisory, server-issued) ----

    public EditSession CreateSession(string userId, string userName, string nodeId,
        EntityId? surfaceId, DateTimeOffset now, TimeSpan ttl)
    {
        var session = new EditSession
        {
            Id = $"ses-{Guid.NewGuid().ToString("N")[..16]}",
            UserId = userId,
            UserName = userName,
            NodeId = nodeId,
            SurfaceId = surfaceId,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now + ttl,
        };
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO edit_sessions
                (id, user_id, user_name, node_id, surface_id, created_at, last_seen_at, expires_at, revoked_at)
            VALUES ($id, $user, $name, $node, $surface, $created, $seen, $expires, NULL)
            """;
        cmd.Parameters.AddWithValue("$id", session.Id);
        cmd.Parameters.AddWithValue("$user", session.UserId);
        cmd.Parameters.AddWithValue("$name", session.UserName);
        cmd.Parameters.AddWithValue("$node", session.NodeId);
        cmd.Parameters.AddWithValue("$surface", session.SurfaceId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$created", session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$seen", session.LastSeenAt.ToString("O"));
        cmd.Parameters.AddWithValue("$expires", session.ExpiresAt.ToString("O"));
        ExecuteWrite(cmd);
        return session;
    }

    public EditSession? FindSession(string id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM edit_sessions WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadSession(reader) : null;
    }

    /// <summary>Refresh a live session. Returns null when it is missing, revoked or expired.</summary>
    public EditSession? TouchSession(string id, DateTimeOffset now, TimeSpan ttl)
    {
        var expires = now + ttl;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE edit_sessions
            SET last_seen_at = $now, expires_at = $expires
            WHERE id = $id AND revoked_at IS NULL AND expires_at > $now
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", now.ToString("O"));
        cmd.Parameters.AddWithValue("$expires", expires.ToString("O"));
        if (ExecuteWrite(cmd) == 0) return null;
        return FindSession(id);
    }

    public bool RevokeSession(string id, DateTimeOffset now)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE edit_sessions SET revoked_at = $now
            WHERE id = $id AND revoked_at IS NULL
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", now.ToString("O"));
        return ExecuteWrite(cmd) > 0;
    }

    public IReadOnlyList<EditSession> ListSessions(DateTimeOffset now, EntityId? surfaceId = null,
        bool includeExpired = false)
    {
        using var cmd = _connection.CreateCommand();
        var conditions = new List<string>();
        if (surfaceId is { } surface)
        {
            conditions.Add("surface_id = $surface");
            cmd.Parameters.AddWithValue("$surface", surface.ToString());
        }
        if (!includeExpired)
        {
            conditions.Add("revoked_at IS NULL AND expires_at > $now");
            cmd.Parameters.AddWithValue("$now", now.ToString("O"));
        }
        cmd.CommandText = "SELECT * FROM edit_sessions"
            + (conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "")
            + " ORDER BY last_seen_at DESC";
        using var reader = cmd.ExecuteReader();
        var sessions = new List<EditSession>();
        while (reader.Read()) sessions.Add(ReadSession(reader));
        return sessions;
    }

    private static EditSession ReadSession(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(reader.GetOrdinal("id")),
        UserId = reader.GetString(reader.GetOrdinal("user_id")),
        UserName = reader.GetString(reader.GetOrdinal("user_name")),
        NodeId = reader.GetString(reader.GetOrdinal("node_id")),
        SurfaceId = reader.IsDBNull(reader.GetOrdinal("surface_id"))
            ? null
            : EntityId.Parse(reader.GetString(reader.GetOrdinal("surface_id"))),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at")),
            System.Globalization.CultureInfo.InvariantCulture),
        LastSeenAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("last_seen_at")),
            System.Globalization.CultureInfo.InvariantCulture),
        ExpiresAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("expires_at")),
            System.Globalization.CultureInfo.InvariantCulture),
        RevokedAt = reader.IsDBNull(reader.GetOrdinal("revoked_at"))
            ? null
            : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("revoked_at")),
                System.Globalization.CultureInfo.InvariantCulture),
    };

    // ---- one-time project invitations ----

    public ProjectInvite CreateInvite(EntityId projectId, UserRole role, string tokenHash,
        DateTimeOffset expiresAt, string? createdBy = null)
    {
        var invite = new ProjectInvite
        {
            Id = $"inv-{Guid.NewGuid().ToString("N")[..12]}",
            ProjectId = projectId,
            Role = role,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO invites (id, token_hash, project_id, role, created_by, created_at, expires_at)
            VALUES ($id, $hash, $project, $role, $by, $created, $expires)
            """;
        cmd.Parameters.AddWithValue("$id", invite.Id);
        cmd.Parameters.AddWithValue("$hash", tokenHash);
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        cmd.Parameters.AddWithValue("$role", role.ToString());
        cmd.Parameters.AddWithValue("$by", (object?)createdBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", invite.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$expires", invite.ExpiresAt.ToString("O"));
        ExecuteWrite(cmd);
        return invite;
    }

    public ProjectInvite? FindInviteByTokenHash(string tokenHash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, role, created_by, created_at, expires_at, used_at, used_by
            FROM invites WHERE token_hash = $hash LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$hash", tokenHash);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadInvite(reader) : null;
    }

    public IReadOnlyList<ProjectInvite> ListInvites(EntityId projectId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, role, created_by, created_at, expires_at, used_at, used_by
            FROM invites WHERE project_id = $project ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = cmd.ExecuteReader();
        var invites = new List<ProjectInvite>();
        while (reader.Read()) invites.Add(ReadInvite(reader));
        return invites;
    }

    /// <summary>Revoke an unused invitation; used invitations remain as membership history.</summary>
    public bool DeleteInvite(EntityId projectId, string inviteId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM invites WHERE id = $id AND project_id = $project AND used_at IS NULL";
        cmd.Parameters.AddWithValue("$id", inviteId);
        cmd.Parameters.AddWithValue("$project", projectId.ToString());
        return ExecuteWrite(cmd) == 1;
    }

    /// <summary>Atomically consume an unused invitation; false means it was already used.</summary>
    public bool MarkInviteUsed(string inviteId, string userId, DateTimeOffset at)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE invites SET used_at = $at, used_by = $user WHERE id = $id AND used_at IS NULL";
        cmd.Parameters.AddWithValue("$at", at.ToString("O"));
        cmd.Parameters.AddWithValue("$user", userId);
        cmd.Parameters.AddWithValue("$id", inviteId);
        return ExecuteWrite(cmd) == 1;
    }

    private static ProjectInvite ReadInvite(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        ProjectId = EntityId.Parse(reader.GetString(1), EntityKind.Collection),
        Role = Enum.Parse<UserRole>(reader.GetString(2)),
        CreatedBy = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
        ExpiresAt = DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
        UsedAt = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture),
        UsedBy = reader.IsDBNull(7) ? null : reader.GetString(7),
    };

    // ---- auth audit ----

    public void AppendAuthAudit(AuthAuditEntry entry)
    {
        using var cmd = _connection.CreateCommand();
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
        ExecuteWrite(cmd);
    }

    public IReadOnlyList<AuthAuditEntry> LoadAuthAudit(int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
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

    private bool ColumnExists(string table, string column)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ---- projects ----

    /// <summary>
    /// Save a native project with optimistic concurrency. A loaded document carries the store
    /// version it was read at; saving a stale document fails with <c>revision-conflict</c>
    /// instead of silently replacing another writer's changes.
    /// </summary>
    public long SaveProject(NativeProject project) =>
        SaveProjectCore(project, enforceAuthorization: false, actorId: null, requireMembership: false);

    /// <summary>
    /// Save after checking the current principal in the same SQLite write transaction.
    /// A null actor represents previously admitted anonymous local mode and requires
    /// that no accounts exist. Bootstrap bypass is a caller decision, never an ID alias.
    /// </summary>
    public long SaveProjectAuthorized(NativeProject project, string? actorId, bool requireMembership) =>
        SaveProjectAuthorized(project, actorId, requireMembership, expectedKeyHash: null);

    /// <summary>Optionally require the admitted account credential inside the same write transaction.</summary>
    public long SaveProjectAuthorized(NativeProject project, string? actorId, bool requireMembership, string? expectedKeyHash) =>
        SaveProjectCore(project, enforceAuthorization: true, actorId, requireMembership, expectedKeyHash);

    private long SaveProjectCore(NativeProject project, bool enforceAuthorization, string? actorId, bool requireMembership,
        string? expectedKeyHash = null)
    {
        var json = NativeJson.Save(project with { Version = 0 });
        lock (_writes)
        {
            // Reserve the SQLite write lock before reading authorization. Revocation
            // through another connection/process must serialize with this whole save,
            // rather than landing between a membership read and project publication.
            using var tx = enforceAuthorization ? _connection.BeginTransaction(deferred: false)
                : _connection.BeginTransaction();
            if (enforceAuthorization) RequireProjectWriteAccess(project.Id, actorId, requireMembership, expectedKeyHash, tx);
            if (project.Version > 0)
            {
                using var update = _connection.CreateCommand();
                update.Transaction = tx;
                update.CommandText = """
                    UPDATE projects
                    SET name = $name, contract_version = $cv, json = $json, version = version + 1
                    WHERE id = $id AND version = $expected
                    """;
                update.Parameters.AddWithValue("$id", project.Id.ToString());
                update.Parameters.AddWithValue("$name", project.Name);
                update.Parameters.AddWithValue("$cv", ContractVersion.Current);
                update.Parameters.AddWithValue("$json", json);
                update.Parameters.AddWithValue("$expected", project.Version);
                if (update.ExecuteNonQuery() == 0)
                    ThrowConflict(project.Id, tx, project.Version);
                tx.Commit();
                return project.Version + 1;
            }

            using var insert = _connection.CreateCommand();
            insert.Transaction = tx;
            // Version 0 means the caller never loaded a persisted document (new project or a
            // legacy/unversioned caller). Keep the historical upsert for compatibility; any
            // documented read-modify-write path carries a non-zero version and is checked above.
            insert.CommandText = """
                INSERT INTO projects (id, name, contract_version, json, version) VALUES ($id, $name, $cv, $json, 1)
                ON CONFLICT(id) DO UPDATE SET
                    name = $name, contract_version = $cv, json = $json, version = projects.version + 1
                """;
            insert.Parameters.AddWithValue("$id", project.Id.ToString());
            insert.Parameters.AddWithValue("$name", project.Name);
            insert.Parameters.AddWithValue("$cv", ContractVersion.Current);
            insert.Parameters.AddWithValue("$json", json);
            insert.ExecuteNonQuery();
            using var next = _connection.CreateCommand();
            next.Transaction = tx;
            next.CommandText = "SELECT version FROM projects WHERE id = $id";
            next.Parameters.AddWithValue("$id", project.Id.ToString());
            var version = Convert.ToInt64(next.ExecuteScalar());
            tx.Commit();
            return version;
        }
    }

    private void RequireProjectWriteAccess(EntityId projectId, string? actorId, bool requireMembership, string? expectedKeyHash, SqliteTransaction tx)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            SELECT CASE WHEN $actor IS NULL THEN NOT EXISTS (SELECT 1 FROM users)
            ELSE EXISTS (
                SELECT 1 FROM users AS principal
                WHERE principal.id = $actor AND principal.disabled = 0
                    AND ($expectedKeyHash IS NULL OR principal.key_hash = $expectedKeyHash)
                    AND principal.role IN ('Annotator', 'Reviewer', 'Admin')
                    AND ($membership = 0 OR principal.role = 'Admin' OR EXISTS (
                        SELECT 1 FROM project_members
                        WHERE project_id = $project AND user_id = principal.id
                    ))
            ) END
            """;
        command.Parameters.AddWithValue("$actor", (object?)actorId ?? DBNull.Value);
        command.Parameters.AddWithValue("$expectedKeyHash", (object?)expectedKeyHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$membership", requireMembership ? 1 : 0);
        command.Parameters.AddWithValue("$project", projectId.ToString());
        if (Convert.ToInt64(command.ExecuteScalar()) != 1)
            throw new ProjectWriteAccessDeniedException();
    }

    private void ThrowConflict(EntityId id, SqliteTransaction tx, long expected)
    {
        using var current = _connection.CreateCommand();
        current.Transaction = tx;
        current.CommandText = "SELECT version FROM projects WHERE id = $id";
        current.Parameters.AddWithValue("$id", id.ToString());
        var value = current.ExecuteScalar();
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.RevisionConflict,
            Scope = id.ToString(),
            Details = new Dictionary<string, string>
            {
                ["expectedVersion"] = expected.ToString(),
                ["currentVersion"] = value?.ToString() ?? "none",
            },
            RemedyHint = RemedyHint.RebaseRevision,
            Message = "Project was changed by another writer; reload before saving.",
        });
    }

    public NativeProject? LoadProject(EntityId id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json, version FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var json = reader.GetString(0);
        var version = reader.GetInt64(1);
        return NativeJson.Load(json) with { Version = version };
    }

    public IReadOnlyList<(EntityId Id, string Name)> ListProjects()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM projects ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var list = new List<(EntityId, string)>();
        while (reader.Read())
            list.Add((EntityId.Parse(reader.GetString(0), EntityKind.Collection), reader.GetString(1)));
        return list;
    }

    public void DeleteProject(EntityId id)
    {
        lock (_writes)
        {
            using var tx = _connection.BeginTransaction();
            using var cmd = _connection.CreateCommand();
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
    }

    // ---- jobs ----

    private const string InsertJob = """
        INSERT INTO jobs (id, type, status, idempotency_key, retries, json) VALUES ($id, $type, $status, $key, $retries, $json)
        """;

    public void SaveJob(JobRecord job) => WriteJob(job);

    /// <summary>Insert a job, or return false when another writer has already reserved its idempotency key.</summary>
    public bool TryCreateJob(JobRecord job) => TryCreateJob(job, maxActivePerOwner: 0);

    /// <summary>Check the active-owner quota and reserve the key in the same SQLite write.</summary>
    public bool TryCreateJob(JobRecord job, int maxActivePerOwner)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = maxActivePerOwner <= 0
            ? InsertJob + " ON CONFLICT(idempotency_key) DO NOTHING"
            : """
            INSERT INTO jobs (id, type, status, idempotency_key, retries, json)
            SELECT $id, $type, $status, $key, $retries, $json
            WHERE (SELECT COUNT(*) FROM jobs
                   WHERE status IN ('Pending', 'Running') AND json_extract(json, '$.owner.id') = $owner) < $max
            ON CONFLICT(idempotency_key) DO NOTHING
            """;
        if (maxActivePerOwner > 0)
        {
            cmd.Parameters.AddWithValue("$owner", job.Owner.Id);
            cmd.Parameters.AddWithValue("$max", maxActivePerOwner);
        }
        BindJob(cmd, job);
        return ExecuteWrite(cmd) == 1;
    }

    public long CountActiveJobs(string ownerId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM jobs
            WHERE status IN ('Pending', 'Running') AND json_extract(json, '$.owner.id') = $owner
            """;
        cmd.Parameters.AddWithValue("$owner", ownerId);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>Independent renewal connection, no migration or version repair.
    /// SQLite waits are provider-bounded (one second per command), not a hard native deadline.</summary>
    public LocalStore OpenLeaseRenewalPeer()
    {
        var settings = new SqliteConnectionStringBuilder(_connectionString)
        {
            DefaultTimeout = 1,
            Pooling = false,
        };
        if (settings.Mode is not (SqliteOpenMode.Memory or SqliteOpenMode.ReadOnly))
            settings.Mode = SqliteOpenMode.ReadWrite;
        return new LocalStore(settings.ToString(), initialize: false, _writes);
    }

    /// <summary>One read and at most one CAS, never the generic unbounded transition loop.
    /// Call only on a renewal-owned peer connection. No automatic recovery is performed.</summary>
    public JobLeaseRenewalResult RenewJobLeaseOnce(EntityId jobId, EntityId workerId, string claimId,
        DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (string.IsNullOrWhiteSpace(claimId) || leaseDuration <= TimeSpan.Zero)
            throw new ArgumentException("Renewal requires a captured claim and positive duration.");
        try
        {
            string? previous;
            using (var read = _connection.CreateCommand())
            {
                read.CommandTimeout = 1;
                read.CommandText = "SELECT json FROM jobs WHERE id = $id";
                read.Parameters.AddWithValue("$id", jobId.ToString());
                previous = read.ExecuteScalar() as string;
            }
            if (previous is null) return JobLeaseRenewalResult.OwnershipLost;
            var current = ContractJson.FromJson<JobRecord>(previous);
            if (current.Status != JobStatus.Running || current.Lease is not { } lease
                || lease.Worker != workerId || lease.ClaimId != claimId)
                return JobLeaseRenewalResult.OwnershipLost;
            var expiry = now + leaseDuration;
            var replacement = current with { Lease = lease with
            {
                LastHeartbeat = lease.LastHeartbeat is { } last && last > now ? last : now,
                ExpiresAt = expiry > lease.ExpiresAt ? expiry : lease.ExpiresAt,
            } };
            // Do not wait behind a handler's process-local write transaction.
            if (!_writes.TryEnter()) return JobLeaseRenewalResult.Contended;
            try
            {
                using var update = _connection.CreateCommand();
                update.CommandTimeout = 1;
                update.CommandText = "UPDATE jobs SET json = $next WHERE id = $id AND json = $previous";
                update.Parameters.AddWithValue("$next", ContractJson.ToJson(replacement));
                update.Parameters.AddWithValue("$id", jobId.ToString());
                update.Parameters.AddWithValue("$previous", previous);
                return update.ExecuteNonQuery() == 1 ? JobLeaseRenewalResult.Renewed : JobLeaseRenewalResult.Contended;
            }
            finally { _writes.Exit(); }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            return JobLeaseRenewalResult.Contended;
        }
    }

    /// <summary>
    /// Apply a conditional transition atomically across store connections. The callback
    /// may be reevaluated after a competing write and must have no side effects.
    /// </summary>
    public JobRecord? UpdateJob(EntityId id, Func<JobRecord, JobRecord?> transition)
    {
        while (ReadJobJson(id) is { } previousJson)
        {
            var replacement = transition(ContractJson.FromJson<JobRecord>(previousJson));
            if (replacement is null) return null;
            if (replacement.Id != id)
                throw new InvalidOperationException("A job transition must preserve the job ID.");
            if (WriteJob(replacement, previousJson)) return replacement;
        }
        return null;
    }

    private bool WriteJob(JobRecord job, string? previousJson = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = previousJson is null
            ? InsertJob + " ON CONFLICT(id) DO UPDATE SET type = $type, status = $status, idempotency_key = $key, retries = $retries, json = $json"
            : """
            UPDATE jobs SET type = $type, status = $status, idempotency_key = $key, retries = $retries, json = $json
            WHERE id = $id AND json = $previous
            """;
        if (previousJson is not null) cmd.Parameters.AddWithValue("$previous", previousJson);
        BindJob(cmd, job);
        return ExecuteWrite(cmd) == 1;
    }

    private static void BindJob(SqliteCommand cmd, JobRecord job)
    {
        cmd.Parameters.AddWithValue("$id", job.Id.ToString());
        cmd.Parameters.AddWithValue("$type", job.Type);
        cmd.Parameters.AddWithValue("$status", job.Status.ToString());
        cmd.Parameters.AddWithValue("$key", (object?)job.IdempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$retries", job.RetriesUsed);
        cmd.Parameters.AddWithValue("$json", ContractJson.ToJson(job));
    }

    public JobRecord? LoadJob(EntityId id) => ReadJobJson(id) is { } json
        ? ContractJson.FromJson<JobRecord>(json) : null;

    private string? ReadJobJson(EntityId id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return cmd.ExecuteScalar() as string;
    }

    public JobRecord? FindJobByIdempotencyKey(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM jobs WHERE idempotency_key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return (cmd.ExecuteScalar() as string) is { } json ? ContractJson.FromJson<JobRecord>(json) : null;
    }

    /// <summary>Most recent jobs first (bounded list for operators and the workspace).</summary>
    public IReadOnlyList<JobRecord> LoadJobs(int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
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
        using var cmd = _connection.CreateCommand();
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
        if (File.Exists(full) || new FileInfo(full).LinkTarget is not null)
            throw new IOException("Backup database already exists.");
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "VACUUM INTO $target";
        cmd.Parameters.AddWithValue("$target", full);
        cmd.ExecuteNonQuery();
    }

    public static void VerifyBackupDatabase(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check(1)";
        if (!string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
            throw new InvalidDataException("Backup database failed SQLite verification.");
        command.CommandText = "PRAGMA user_version";
        var schema = Convert.ToInt64(command.ExecuteScalar());
        if (schema < 0 || schema > SchemaVersion)
            throw new InvalidDataException("Backup database uses an unsupported store schema.");

        // A current-schema file is not migrated on open: require its query shapes
        // now. Older stores retain the established minimum project shape; this
        // read-only verifier neither migrates nor claims full migration validation.
        if (schema == SchemaVersion)
        {
            (string Table, string Columns)[] shapes =
            [
                ("projects", "id,name,contract_version,json,version"),
                ("jobs", "id,type,status,idempotency_key,retries,json"),
                ("users", "id,name,role,key_hash,created_at,disabled"),
                ("auth_audit", "id,at,user_id,user_name,role,action,method,path,status"),
                ("project_members", "project_id,user_id,added_at"),
                ("runs", "id,kind,name,created_at,created_by,dataset_digest,config_json,metrics_json,history_json,artifacts_json,notes"),
                ("presence", "surface_id,editor_id,updated_at"),
                ("invites", "id,token_hash,project_id,role,created_by,created_at,expires_at,used_at,used_by"),
                ("edit_sessions", "id,user_id,user_name,node_id,surface_id,created_at,last_seen_at,expires_at,revoked_at"),
            ];
            foreach (var shape in shapes)
            {
                using var table = connection.CreateCommand();
                table.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name COLLATE NOCASE";
                table.Parameters.AddWithValue("$name", shape.Table);
                if (table.ExecuteScalar() is null)
                    throw new InvalidDataException("Backup current-schema table is missing or has an unsupported shape.");
                table.Parameters.Clear();
                table.CommandText = $"SELECT {shape.Columns} FROM {shape.Table} LIMIT 0";
                try { using var columns = table.ExecuteReader(); }
                catch (SqliteException error)
                { throw new InvalidDataException("Backup current-schema table has an unsupported query shape.", error); }
            }
            command.CommandText = "SELECT 1 FROM projects WHERE typeof(version) != 'integer' OR version < 0 LIMIT 1";
            if (command.ExecuteScalar() is not null)
                throw new InvalidDataException("Backup project version is not a nonnegative integer.");
            // Zero remains eligible for the existing normal-open repair, not a write here.
        }

        if (schema == 0)
        {
            // The established migrator cannot add this column to a preexisting
            // schema-zero projects table; refuse before replacing a healthy store.
            using var legacyVersion = connection.CreateCommand();
            legacyVersion.CommandText = "SELECT version FROM projects LIMIT 0";
            try { using var version = legacyVersion.ExecuteReader(); }
            catch (SqliteException error)
            { throw new InvalidDataException("Backup schema-zero projects lack the version column required for migration.", error); }
        }
        using (var minimum = connection.CreateCommand())
        {
            minimum.CommandText = "SELECT id, name, contract_version, json FROM projects LIMIT 0";
            try { using var shape = minimum.ExecuteReader(); }
            catch (SqliteException error)
            { throw new InvalidDataException("Backup projects do not have the required minimum query shape.", error); }
        }
        command.CommandText = "SELECT id, name, contract_version, json FROM projects";
        using var projects = command.ExecuteReader();
        while (projects.Read())
        {
            if (projects.IsDBNull(0) || projects.IsDBNull(1) || projects.IsDBNull(2) || projects.IsDBNull(3)
                || !EntityId.TryParse(projects.GetString(0), EntityKind.Collection, out var rowId)
                || !string.Equals(projects.GetString(2), ContractVersion.Current, StringComparison.Ordinal))
                throw new InvalidDataException("Backup project identity or contract metadata is invalid.");
            NativeProject project;
            try { project = NativeJson.Load(projects.GetString(3)); }
            catch (Exception error) when (error is System.Text.Json.JsonException or LeviathanException
                or FormatException or ArgumentException or InvalidOperationException)
            {
                throw new InvalidDataException("Backup contains an unreadable or unsupported native project payload.", error);
            }
            if (project is null || project.Id.Kind != EntityKind.Collection
                || !EntityId.TryParse(project.Id.ToString(), EntityKind.Collection, out var nativeId) || nativeId != rowId)
                throw new InvalidDataException("Backup native project identity does not match its database row.");
            VerifyNativeContainers(project);
            // Do not rewrite payloads, normalize versions or reject/deduplicate legacy inner IDs.
        }

        // Required JSON properties can still contain explicit nulls: nullable annotations
        // are not runtime validation. Refuse unusable traversal containers before restore
        // publication, without changing global NativeJson/forensic loading semantics.
        static void VerifyNativeContainers(NativeProject project)
        {
            RequireItems(project.Objects);
            foreach (var obj in project.Objects)
            {
                RequireMap(obj.ExternalRefs); RequireMap(obj.Metadata);
                RequireItems(obj.Attachments); RequireItems(obj.Fragments); RequireItems(obj.Surfaces);
                foreach (var fragment in obj.Fragments)
                {
                    RequireMap(fragment.ExternalRefs); RequireMap(fragment.Metadata); RequireItems(fragment.SurfaceIds);
                }
                foreach (var surface in obj.Surfaces)
                {
                    RequireMap(surface.ExternalRefs);
                    RequireItems(surface.Captures); RequireItems(surface.Regions); RequireItems(surface.Lines);
                    RequireItems(surface.Revisions); RequireItems(surface.Annotations); RequireItems(surface.GeometryEdits);
                    RequireItems(surface.ReviewDecisions); RequireItems(surface.AnnotationRetractions);
                    foreach (var region in surface.Regions)
                    {
                        RequireItems(region.Polygon); RequireItems(region.Languages); RequireTags(region.Tags);
                    }
                    foreach (var line in surface.Lines)
                    {
                        RequireItems(line.Polygon); RequireItems(line.Baseline); RequireItems(line.Languages); RequireTags(line.Tags);
                    }
                    foreach (var revision in surface.Revisions) RequireItems(revision.GraphemeConfidences);
                }
            }
        }

        static void RequireItems<T>(IEnumerable<T>? items)
        {
            if (items is null || items.Any(item => item is null))
                throw new InvalidDataException("Backup native project contains a null collection or collection entry.");
        }

        static void RequireMap(IReadOnlyDictionary<string, string>? map)
        {
            // Preserve scalar metadata values verbatim, including legacy null values.
            if (map is null) throw new InvalidDataException("Backup native project contains a null metadata container.");
        }

        static void RequireTags(IReadOnlyDictionary<string, IReadOnlyList<string>>? tags)
        {
            if (tags is null) throw new InvalidDataException("Backup native project contains a null tag container.");
            foreach (var values in tags.Values) RequireItems(values);
        }
    }

    public void Dispose() => _connection.Dispose();
}
