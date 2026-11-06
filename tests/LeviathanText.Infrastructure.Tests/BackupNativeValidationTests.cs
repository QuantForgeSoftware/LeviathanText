using System.Security.Cryptography;
using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

public sealed class BackupNativeValidationTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-backup-native-").FullName;
    private string PathFor(string name) => Path.Combine(root, name + ".db");
    private static NativeProject Project(bool duplicateIds = false) => new()
    {
        Id = new(EntityKind.Collection, "generated-backup"), Name = "Generated backup",
        Objects = duplicateIds ? [new NativeObject
        {
            Id = new(EntityKind.PhysicalObject, "generated"),
            Surfaces = [new NativeSurface { Id = new(EntityKind.Surface, "same"), Kind = "page", Captures = [], Regions = [], Lines = [] },
                new NativeSurface { Id = new(EntityKind.Surface, "same"), Kind = "page", Captures = [], Regions = [], Lines = [] }],
        }] : [],
    };
    private static void Sql(string path, string sql, string? payload = null)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
        if (payload is not null) command.Parameters.AddWithValue("$payload", payload);
        command.ExecuteNonQuery();
    }
    private string Seed(bool duplicates = false)
    {
        var path = PathFor("source"); using var store = new LocalStore(path); store.SaveProject(Project(duplicates)); return path;
    }
    private static byte[] Hash(string path) => SHA256.HashData(File.ReadAllBytes(path));

    [Theory]
    [InlineData("malformed")]
    [InlineData("null-envelope")]
    [InlineData("null-project")]
    [InlineData("wrong-native-id")]
    [InlineData("wrong-native-kind")]
    [InlineData("invalid-row-id")]
    [InlineData("row-contract")]
    [InlineData("native-contract")]
    public void Invalid_native_payload_or_identity_is_refused_without_rewriting_the_snapshot(string change)
    {
        var path = Seed(); var document = JsonNode.Parse(NativeJson.Save(Project()))!;
        switch (change)
        {
            case "malformed": Sql(path, "UPDATE projects SET json=$payload", "{"); break;
            case "null-envelope": Sql(path, "UPDATE projects SET json=$payload", "null"); break;
            case "null-project": document["project"] = null; Sql(path, "UPDATE projects SET json=$payload", document.ToJsonString()); break;
            case "wrong-native-id": document["project"]!["id"] = "col-different"; Sql(path, "UPDATE projects SET json=$payload", document.ToJsonString()); break;
            case "wrong-native-kind": document["project"]!["id"] = "surf-generated"; Sql(path, "UPDATE projects SET json=$payload", document.ToJsonString()); break;
            case "invalid-row-id": Sql(path, "UPDATE projects SET id='invalid'"); break;
            case "row-contract": Sql(path, "UPDATE projects SET contract_version='future'"); break;
            case "native-contract": document["contractVersion"] = "future"; Sql(path, "UPDATE projects SET json=$payload", document.ToJsonString()); break;
        }
        var before = Hash(path);
        Assert.Throws<InvalidDataException>(() => LocalStore.VerifyBackupDatabase(path));
        Assert.Equal(before, Hash(path));
    }
    [Theory]
    [InlineData("missing-users")]
    [InlineData("missing-version")]
    [InlineData("negative-version")]
    [InlineData("text-version")]
    [InlineData("negative-schema")]
    [InlineData("old7-missing-name")]
    [InlineData("schema0-missing-version")]
    public void Broken_query_shapes_and_invalid_versions_are_refused_read_only(string change)
    {
        var path = Seed();
        Sql(path, change switch
        {
            "missing-users" => "DROP TABLE users",
            "missing-version" => "ALTER TABLE projects DROP COLUMN version",
            "negative-version" => "UPDATE projects SET version=-1",
            "text-version" => "UPDATE projects SET version='invalid'",
            "old7-missing-name" => "ALTER TABLE projects DROP COLUMN name; PRAGMA user_version=7",
            "schema0-missing-version" => "ALTER TABLE projects DROP COLUMN version; PRAGMA user_version=0",
            _ => "PRAGMA user_version=-1",
        });
        var before = Hash(path); Assert.Throws<InvalidDataException>(() => LocalStore.VerifyBackupDatabase(path));
        Assert.Equal(before, Hash(path));
    }
    [Fact]
    public void Current_zero_version_and_legacy_duplicate_inner_ids_are_preserved_not_repaired()
    {
        var path = Seed(duplicates: true); Sql(path, "UPDATE projects SET version=0"); var before = File.ReadAllBytes(path);
        LocalStore.VerifyBackupDatabase(path); Assert.Equal(before, File.ReadAllBytes(path));
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT version FROM projects";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }
    [Fact]
    public void Current_query_shapes_keep_SQLite_identifier_case_compatibility()
    {
        var path = Seed(); Sql(path, "ALTER TABLE users RENAME TO temporary_users; ALTER TABLE temporary_users RENAME TO USERS");
        var before = File.ReadAllBytes(path); LocalStore.VerifyBackupDatabase(path); Assert.Equal(before, File.ReadAllBytes(path));
    }
    [Fact]
    public void Genuine_v7_without_version_column_is_verified_without_migration_then_opens_normally()
    {
        var path = PathFor("legacy"); var native = JsonNode.Parse(NativeJson.Save(Project()))!; native["project"]!.AsObject().Remove("version");
        Sql(path, $"CREATE TABLE projects(id TEXT PRIMARY KEY,name TEXT NOT NULL,contract_version TEXT NOT NULL,json TEXT NOT NULL); PRAGMA user_version=7; INSERT INTO projects VALUES('col-generated-backup','Generated backup','{ContractVersion.Current}',$payload)", native.ToJsonString());
        var before = File.ReadAllBytes(path); LocalStore.VerifyBackupDatabase(path); Assert.Equal(before, File.ReadAllBytes(path));
        using var migrated = new LocalStore(path); Assert.Equal("Generated backup", migrated.LoadProject(Project().Id)!.Name);
    }
    [Fact]
    public void Minimum_schema_zero_with_existing_version_keeps_its_supported_migration_path()
    {
        var path = PathFor("zero");
        Sql(path, $"CREATE TABLE projects(id TEXT PRIMARY KEY,name TEXT NOT NULL,contract_version TEXT NOT NULL,json TEXT NOT NULL,version INTEGER NOT NULL); PRAGMA user_version=0; INSERT INTO projects VALUES('col-generated-backup','Generated backup','{ContractVersion.Current}',$payload,1)", NativeJson.Save(Project()));
        var before = File.ReadAllBytes(path); LocalStore.VerifyBackupDatabase(path); Assert.Equal(before, File.ReadAllBytes(path));
        using var migrated = new LocalStore(path); Assert.Equal("Generated backup", migrated.LoadProject(Project().Id)!.Name);
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
