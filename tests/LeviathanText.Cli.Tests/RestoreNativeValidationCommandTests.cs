using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Cli.Tests;

public sealed class RestoreNativeValidationCommandTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-restore-native-cli-").FullName;
    private static NativeProject Project(string suffix, bool duplicateIds = false) => new()
    {
        Id = new(EntityKind.Collection, suffix), Name = "Generated " + suffix,
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
        if (payload is not null) command.Parameters.AddWithValue("$payload", payload); command.ExecuteNonQuery();
    }
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Manifest(string backup, bool image)
    {
        var source = Path.Combine(backup, "images", "same.cache");
        object[] images = image ? [new { name = "same.cache", sha256 = Hash(source), size = new FileInfo(source).Length }] : Array.Empty<object>();
        File.WriteAllText(Path.Combine(backup, "backup-manifest.json"), JsonSerializer.Serialize(new
        {
            storeChecksum = Hash(Path.Combine(backup, "store.db")), images,
        }));
    }
    private string Backup(bool duplicates = false)
    {
        var backup = Path.Combine(root, "backup"); Directory.CreateDirectory(backup);
        using (var store = new LocalStore(Path.Combine(backup, "store.db"))) store.SaveProject(Project("incoming", duplicates));
        Directory.CreateDirectory(Path.Combine(backup, "images"));
        File.WriteAllBytes(Path.Combine(backup, "images", "same.cache"), [0, 255, 1, 2]); Manifest(backup, image: true); return backup;
    }
    [Theory]
    [InlineData("malformed")]
    [InlineData("null-project")]
    [InlineData("wrong-native-id")]
    [InlineData("row-contract")]
    [InlineData("native-contract")]
    [InlineData("missing-users")]
    [InlineData("missing-version")]
    [InlineData("negative-version")]
    [InlineData("negative-schema")]
    [InlineData("old7-missing-name")]
    [InlineData("schema0-missing-version")]
    public void Checksum_valid_but_unreadable_backup_never_replaces_healthy_force_target_or_images(string change)
    {
        var backup = Backup(); var database = Path.Combine(backup, "store.db");
        var native = JsonNode.Parse(NativeJson.Save(Project("incoming")))!;
        switch (change)
        {
            case "malformed": Sql(database, "UPDATE projects SET json=$payload", "{"); break;
            case "null-project": native["project"] = null; Sql(database, "UPDATE projects SET json=$payload", native.ToJsonString()); break;
            case "wrong-native-id": native["project"]!["id"] = "col-different"; Sql(database, "UPDATE projects SET json=$payload", native.ToJsonString()); break;
            case "row-contract": Sql(database, "UPDATE projects SET contract_version='future'"); break;
            case "native-contract": native["contractVersion"] = "future"; Sql(database, "UPDATE projects SET json=$payload", native.ToJsonString()); break;
            case "missing-users": Sql(database, "DROP TABLE users"); break;
            case "missing-version": Sql(database, "ALTER TABLE projects DROP COLUMN version"); break;
            case "negative-version": Sql(database, "UPDATE projects SET version=-1"); break;
            case "negative-schema": Sql(database, "PRAGMA user_version=-1"); break;
            case "old7-missing-name": Sql(database, "ALTER TABLE projects DROP COLUMN name; PRAGMA user_version=7"); break;
            case "schema0-missing-version": Sql(database, "ALTER TABLE projects DROP COLUMN version; PRAGMA user_version=0"); break;
        }
        Manifest(backup, image: true); // Deliberately valid checksum: semantic validation must reject it.
        var sourceBefore = File.ReadAllBytes(database);
        var destination = Path.Combine(root, "live", "store.db");
        using (var store = new LocalStore(destination)) store.SaveProject(Project("keep"));
        var cache = Path.Combine(root, "live", "images"); Directory.CreateDirectory(cache);
        var image = Path.Combine(cache, "same.cache"); byte[] original = [9, 8, 0, 128]; File.WriteAllBytes(image, original);
        var unrelated = Path.Combine(cache, "unlisted.cache"); File.WriteAllText(unrelated, "preserve");
        var targetBefore = File.ReadAllBytes(destination);
        var exit = Program.Main(["--store", destination, "restore", backup, "--force"]);
        var observed = JsonSerializer.Serialize(new
        {
            exit, targetBeforeSha256 = Convert.ToHexStringLower(SHA256.HashData(targetBefore)), targetAfterSha256 = Hash(destination),
            imageBeforeSha256 = Convert.ToHexStringLower(SHA256.HashData(original)), imageAfterSha256 = Hash(image),
        });
        Assert.True(exit == 1, observed); // Fail-before records actual replacement hashes, not checksum tampering.
        Assert.Equal(targetBefore, File.ReadAllBytes(destination)); Assert.Equal(sourceBefore, File.ReadAllBytes(database));
        Assert.Equal(original, File.ReadAllBytes(image)); Assert.Equal("preserve", File.ReadAllText(unrelated));
        Assert.Equal(2, Directory.GetFiles(cache).Length);
        Assert.DoesNotContain(Directory.GetDirectories(Path.Combine(root, "live")), path => Path.GetFileName(path).StartsWith(".lt-restore-", StringComparison.Ordinal));
    }
    [Fact]
    public void Genuine_old_v7_is_restored_byte_identically_before_deliberate_later_migration()
    {
        var backup = Path.Combine(root, "old"); Directory.CreateDirectory(backup); var source = Path.Combine(backup, "store.db");
        var document = JsonNode.Parse(NativeJson.Save(Project("old")))!; document["project"]!.AsObject().Remove("version");
        Sql(source, $"CREATE TABLE projects(id TEXT PRIMARY KEY,name TEXT NOT NULL,contract_version TEXT NOT NULL,json TEXT NOT NULL); PRAGMA user_version=7; INSERT INTO projects VALUES('col-old','Generated old','{ContractVersion.Current}',$payload)", document.ToJsonString());
        Manifest(backup, image: false); var before = File.ReadAllBytes(source); var destination = Path.Combine(root, "restored", "store.db");
        Assert.Equal(0, Program.Main(["--store", destination, "restore", backup]));
        Assert.Equal(before, File.ReadAllBytes(destination)); Assert.Equal(before, File.ReadAllBytes(source));
        using var migrated = new LocalStore(destination); Assert.Equal("Generated old", migrated.LoadProject(Project("old").Id)!.Name);
    }
    [Fact]
    public void Existing_duplicate_inner_identity_is_preserved_for_explicit_recovery_not_silently_dropped()
    {
        var backup = Backup(duplicates: true); var source = Path.Combine(backup, "store.db"); var before = File.ReadAllBytes(source);
        var destination = Path.Combine(root, "restored", "store.db");
        Assert.Equal(0, Program.Main(["--store", destination, "restore", backup]));
        Assert.Equal(before, File.ReadAllBytes(destination)); Assert.Equal(before, File.ReadAllBytes(source));
        using var store = new LocalStore(destination); var surfaces = store.LoadProject(Project("incoming").Id)!.Objects[0].Surfaces;
        Assert.Equal(2, surfaces.Count); Assert.Equal(surfaces[0].Id, surfaces[1].Id);
    }
    [Fact]
    public void Schema_zero_with_version_is_restored_without_migration_then_opens_normally()
    {
        var backup = Path.Combine(root, "zero"); Directory.CreateDirectory(backup); var source = Path.Combine(backup, "store.db");
        Sql(source, $"CREATE TABLE projects(id TEXT PRIMARY KEY,name TEXT NOT NULL,contract_version TEXT NOT NULL,json TEXT NOT NULL,version INTEGER NOT NULL); PRAGMA user_version=0; INSERT INTO projects VALUES('col-zero','Generated zero','{ContractVersion.Current}',$payload,1)", NativeJson.Save(Project("zero")));
        Manifest(backup, image: false); var before = File.ReadAllBytes(source); var destination = Path.Combine(root, "restored", "store.db");
        Assert.Equal(0, Program.Main(["--store", destination, "restore", backup]));
        Assert.Equal(before, File.ReadAllBytes(destination)); Assert.Equal(before, File.ReadAllBytes(source));
        using var migrated = new LocalStore(destination); Assert.Equal("Generated zero", migrated.LoadProject(Project("zero").Id)!.Name);
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
