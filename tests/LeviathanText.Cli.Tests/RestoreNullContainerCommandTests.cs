using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Cli.Tests;

// Generated local SQLite/cache bytes only; invokes restore, never training or models.
public sealed class RestoreNullContainerCommandTests : IDisposable
{
    readonly string root = Directory.CreateTempSubdirectory("lt-restore-null-container-").FullName;
    static NativeProject Project(string value) => new() { Id = new(EntityKind.Collection, value), Name = value, Objects = [] };
    static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    [Theory]
    [InlineData("objects-null")]
    [InlineData("object-entry")]
    [InlineData("surfaces-null")]
    [InlineData("surface-entry")]
    [InlineData("lines-null")]
    public void Checksum_valid_null_graph_cannot_replace_force_target_database_or_cache(string change)
    {
        string backup = Directory.CreateDirectory(Path.Combine(root, "backup")).FullName;
        string source = Path.Combine(backup, "store.db");
        using (var store = new LocalStore(source)) store.SaveProject(Project("incoming"));
        var document = JsonNode.Parse(NativeJson.Save(Project("incoming")))!;
        var surface = new JsonObject { ["id"] = "surf-generated", ["kind"] = "generated",
            ["captures"] = new JsonArray(), ["regions"] = new JsonArray(), ["lines"] = new JsonArray() };
        var obj = new JsonObject { ["id"] = "obj-generated", ["surfaces"] = new JsonArray(surface) };
        document["project"]!["objects"] = new JsonArray(obj);
        if (change == "objects-null") document["project"]!["objects"] = null;
        if (change == "object-entry") document["project"]!["objects"]![0] = null;
        if (change == "surfaces-null") obj["surfaces"] = null;
        if (change == "surface-entry") obj["surfaces"]![0] = null;
        if (change == "lines-null") surface["lines"] = null;
        string payload = document.ToJsonString(); Assert.NotNull(NativeJson.Load(payload));
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Pooling = false }.ToString())) {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE projects SET json=$payload";
            command.Parameters.AddWithValue("$payload", payload); command.ExecuteNonQuery(); }
        byte[] incomingCache = [0, 255, 128, 1];
        string sourceCache = Directory.CreateDirectory(Path.Combine(backup, "images")).FullName;
        File.WriteAllBytes(Path.Combine(sourceCache, "same.cache"), incomingCache);
        File.WriteAllText(Path.Combine(backup, "backup-manifest.json"), JsonSerializer.Serialize(new {
            storeChecksum = Hash(File.ReadAllBytes(source)), images = new[] { new { name = "same.cache", sha256 = Hash(incomingCache), size = incomingCache.Length } } }));
        string destination = Path.Combine(root, "live", "store.db");
        using (var store = new LocalStore(destination)) store.SaveProject(Project("keep"));
        string cache = Directory.CreateDirectory(Path.Combine(root, "live", "images")).FullName;
        byte[] expectedCache = [9, 8, 7, 6]; File.WriteAllBytes(Path.Combine(cache, "same.cache"), expectedCache);
        File.WriteAllText(Path.Combine(cache, "unrelated.cache"), "KEEP");
        byte[] sourceBefore = File.ReadAllBytes(source), targetBefore = File.ReadAllBytes(destination);
        int exit = Program.Main(["--store", destination, "restore", backup, "--force"]);
        Assert.True(exit == 1, $"exit={exit}; databasePreserved={Hash(targetBefore) == Hash(File.ReadAllBytes(destination))}; cachePreserved={Hash(expectedCache) == Hash(File.ReadAllBytes(Path.Combine(cache, "same.cache")))}");
        Assert.Equal(targetBefore, File.ReadAllBytes(destination)); Assert.Equal(sourceBefore, File.ReadAllBytes(source));
        Assert.Equal(expectedCache, File.ReadAllBytes(Path.Combine(cache, "same.cache")));
        Assert.Equal("KEEP", File.ReadAllText(Path.Combine(cache, "unrelated.cache"))); Assert.Equal(2, Directory.GetFiles(cache).Length);
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
