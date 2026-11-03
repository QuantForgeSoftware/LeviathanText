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
                var alter = _connection.CreateCommand();
                alter.Transaction = tx;
                alter.CommandText = "ALTER TABLE projects ADD COLUMN version INTEGER NOT NULL DEFAULT 1";
                alter.ExecuteNonQuery();
            }
            var setVersion = _connection.CreateCommand();
            setVersion.Transaction = tx;
            setVersion.CommandText = $"PRAGMA user_version={SchemaVersion}";
            setVersion.ExecuteNonQuery();
        }
        // Earlier migrations left persisted rows at zero, which is reserved for
        // unversioned callers. Repair those rows even when the schema is already current
        // so every loaded document participates in the optimistic concurrency check.
        var repairVersions = _connection.CreateCommand();
        repairVersions.Transaction = tx;
        repairVersions.CommandText = "UPDATE projects SET version = 1 WHERE version = 0";
        repairVersions.ExecuteNonQuery();
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

    // ---- experiment runs ----

    public void SaveRun(RunRecord run)
    {
        var cmd = _connection.CreateCommand();
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
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<RunRecord> LoadRuns(string? kind = null, int limit = 100)
    {
        var cmd = _connection.CreateCommand();
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
        var cmd = _connection.CreateCommand();
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
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO presence (surface_id, editor_id, updated_at) VALUES ($surface, $editor, $at)
            ON CONFLICT(surface_id, editor_id) DO UPDATE SET updated_at = $at
            """;
        cmd.Parameters.AddWithValue("$surface", surfaceId.ToString());
        cmd.Parameters.AddWithValue("$editor", editorId);
        cmd.Parameters.AddWithValue("$at", at.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<string> ListPresence(EntityId surfaceId, DateTimeOffset since)
    {
        var prune = _connection.CreateCommand();
        prune.CommandText = "DELETE FROM presence WHERE surface_id = $surface AND updated_at < $since";
        prune.Parameters.AddWithValue("$surface", surfaceId.ToString());
        prune.Parameters.AddWithValue("$since", since.ToString("O"));
        prune.ExecuteNonQuery();

        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT editor_id FROM presence WHERE surface_id = $surface ORDER BY editor_id";
        cmd.Parameters.AddWithValue("$surface", surfaceId.ToString());
        using var reader = cmd.ExecuteReader();
        var editors = new List<string>();
        while (reader.Read()) editors.Add(reader.GetString(0));
        return editors;
    }

    public void LeavePresence(EntityId surfaceId, string editorId)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM presence WHERE surface_id = $surface AND editor_id = $editor";
        cmd.Parameters.AddWithValue("$surface", surfaceId.ToString());
        cmd.Parameters.AddWithValue("$editor", editorId);
        cmd.ExecuteNonQuery();
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
        var cmd = _connection.CreateCommand();
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
        cmd.ExecuteNonQuery();
        return session;
    }

    public EditSession? FindSession(string id)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM edit_sessions WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadSession(reader) : null;
    }

    /// <summary>Refresh a live session. Returns null when it is missing, revoked or expired.</summary>
    public EditSession? TouchSession(string id, DateTimeOffset now, TimeSpan ttl)
    {
        var expires = now + ttl;
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE edit_sessions
            SET last_seen_at = $now, expires_at = $expires
            WHERE id = $id AND revoked_at IS NULL AND expires_at > $now
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", now.ToString("O"));
        cmd.Parameters.AddWithValue("$expires", expires.ToString("O"));
        if (cmd.ExecuteNonQuery() == 0) return null;
        return FindSession(id);
    }

    public bool RevokeSession(string id, DateTimeOffset now)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE edit_sessions SET revoked_at = $now
            WHERE id = $id AND revoked_at IS NULL
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", now.ToString("O"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<EditSession> ListSessions(DateTimeOffset now, EntityId? surfaceId = null,
        bool includeExpired = false)
    {
        var cmd = _connection.CreateCommand();
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
        var cmd = _connection.CreateCommand();
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
        cmd.ExecuteNonQuery();
        return invite;
    }

    public ProjectInvite? FindInviteByTokenHash(string tokenHash)
    {
        var cmd = _connection.CreateCommand();
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
        var cmd = _connection.CreateCommand();
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

    /// <summary>Atomically consume an unused invitation; false means it was already used.</summary>
    public bool MarkInviteUsed(string inviteId, string userId, DateTimeOffset at)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE invites SET used_at = $at, used_by = $user WHERE id = $id AND used_at IS NULL";
        cmd.Parameters.AddWithValue("$at", at.ToString("O"));
        cmd.Parameters.AddWithValue("$user", userId);
        cmd.Parameters.AddWithValue("$id", inviteId);
        return cmd.ExecuteNonQuery() == 1;
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

    private bool ColumnExists(string table, string column)
    {
        var cmd = _connection.CreateCommand();
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
    public long SaveProject(NativeProject project)
    {
        var json = NativeJson.Save(project with { Version = 0 });
        using var tx = _connection.BeginTransaction();
        if (project.Version > 0)
        {
            var update = _connection.CreateCommand();
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

        var insert = _connection.CreateCommand();
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
        var next = _connection.CreateCommand();
        next.Transaction = tx;
        next.CommandText = "SELECT version FROM projects WHERE id = $id";
        next.Parameters.AddWithValue("$id", project.Id.ToString());
        var version = Convert.ToInt64(next.ExecuteScalar());
        tx.Commit();
        return version;
    }

    private void ThrowConflict(EntityId id, SqliteTransaction tx, long expected)
    {
        var current = _connection.CreateCommand();
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
        var cmd = _connection.CreateCommand();
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
