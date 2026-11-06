using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

/// <summary>Generated persistence checks; no image, model, labels or external store.</summary>
public sealed class UnplacedNativePersistenceTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-unplaced-persistence-").FullName;
    private static readonly EntityId ProjectId = new(EntityKind.Collection, "unplaced-persistence");
    private static readonly EntityId CaptureId = new(EntityKind.Capture, "generated");
    private static readonly EntityId RegionId = new(EntityKind.Region, "generated");
    private string FileFor(string name) => Path.Combine(root, name + ".db");
    private static NativeProject Project(string absent) => new()
    {
        Id = ProjectId, Name = "Generated unplaced persistence",
        Objects = [new NativeObject
        {
            Id = new(EntityKind.PhysicalObject, "generated"),
            Surfaces = [new NativeSurface
            {
                Id = new(EntityKind.Surface, "generated"), Kind = "page",
                Captures = [new NativeCapture { Id = CaptureId, ImageLocation = "", Frame = new CoordinateFrame
                { ImageRef = CaptureId, ImageChecksum = new Checksum { Algorithm = "sha256", Digest = new string('a', 64) } } }],
                Regions = [new NativeRegion { Id = RegionId, FrameRef = absent == "region-frame" ? default : CaptureId }],
                Lines = [new NativeLine { Id = new(EntityKind.Line, "generated"),
                    RegionId = absent == "line-region" ? default : RegionId,
                    FrameRef = absent == "line-frame" ? default : CaptureId }],
            }],
        }],
    };
    private static JsonNode ReferenceParent(JsonNode node, string reference) => reference == "region-frame"
        ? node["project"]!["objects"]![0]!["surfaces"]![0]!["regions"]![0]!
        : node["project"]!["objects"]![0]!["surfaces"]![0]!["lines"]![0]!;
    private static string ReferenceName(string reference) => reference == "line-region" ? "regionId" : "frameRef";
    private static EntityId Reference(NativeProject project, string reference)
    {
        var surface = project.Objects[0].Surfaces[0];
        return reference == "region-frame" ? surface.Regions[0].FrameRef
            : reference == "line-region" ? surface.Lines[0].RegionId : surface.Lines[0].FrameRef;
    }
    private static void ReplaceRawJson(string path, string json)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET json=$json WHERE id=$id";
        command.Parameters.AddWithValue("$json", json); command.Parameters.AddWithValue("$id", ProjectId.ToString());
        Assert.Equal(1, command.ExecuteNonQuery());
    }
    private static string ReadRawJson(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM projects WHERE id=$id"; command.Parameters.AddWithValue("$id", ProjectId.ToString());
        return (string)command.ExecuteScalar()!;
    }

    [Theory]
    [InlineData("line-region")]
    [InlineData("line-frame")]
    [InlineData("region-frame")]
    public void Save_close_reopen_and_read_only_load_preserve_explicit_absence_without_repair(string reference)
    {
        var path = FileFor("source");
        using (var store = new LocalStore(path)) store.SaveProject(Project(reference));
        var bytes = File.ReadAllBytes(path); var raw = ReadRawJson(path);
        Assert.Null(ReferenceParent(JsonNode.Parse(raw)!, reference)[ReferenceName(reference)]);
        using (var store = LocalStore.OpenExisting(path, readOnly: true))
        {
            var loaded = store.LoadProject(ProjectId)!;
            Assert.Equal(default, Reference(loaded, reference));
            Assert.Equal(raw, NativeJson.Save(loaded with { Version = 0 }));
            Assert.Empty(loaded.Objects[0].Surfaces[0].Lines[0].Polygon);
        }
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(raw, ReadRawJson(path));
        using (var store = LocalStore.OpenExisting(path)) Assert.Equal(default, Reference(store.LoadProject(ProjectId)!, reference));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("line-region", false)] [InlineData("line-frame", false)] [InlineData("region-frame", false)]
    [InlineData("line-region", true)] [InlineData("line-frame", true)] [InlineData("region-frame", true)]
    public void Backup_verify_and_fresh_file_restore_preserve_source_payload_and_legacy_bytes(string reference, bool legacy)
    {
        var source = FileFor("source");
        using (var store = new LocalStore(source)) store.SaveProject(Project("none"));
        if (legacy)
        {
            var node = JsonNode.Parse(ReadRawJson(source))!;
            ReferenceParent(node, reference)[ReferenceName(reference)] = "col-";
            ReplaceRawJson(source, node.ToJsonString());
        }
        else
        {
            using var store = LocalStore.OpenExisting(source);
            store.SaveProject(Project(reference) with { Version = store.LoadProject(ProjectId)!.Version });
        }
        var sourceRaw = ReadRawJson(source); var sourceBytes = File.ReadAllBytes(source);
        using (var readOnly = LocalStore.OpenExisting(source, readOnly: true))
            Assert.Equal(default, Reference(readOnly.LoadProject(ProjectId)!, reference));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source)); Assert.Equal(sourceRaw, ReadRawJson(source));
        var backup = FileFor("backup");
        using (var store = LocalStore.OpenExisting(source)) store.BackupTo(backup);
        Assert.Equal(sourceBytes, File.ReadAllBytes(source)); Assert.Equal(sourceRaw, ReadRawJson(source));
        var backupBytes = File.ReadAllBytes(backup);
        LocalStore.VerifyBackupDatabase(backup);
        Assert.Equal(backupBytes, File.ReadAllBytes(backup)); Assert.Equal(sourceRaw, ReadRawJson(backup));
        // This covers store-level verified snapshot restoration, not CLI force/rollback publication.
        var restored = FileFor("restored"); File.Copy(backup, restored, overwrite: false);
        using (var store = LocalStore.OpenExisting(restored, readOnly: true))
            Assert.Equal(default, Reference(store.LoadProject(ProjectId)!, reference));
        Assert.Equal(backupBytes, File.ReadAllBytes(restored)); Assert.Equal(sourceRaw, ReadRawJson(restored));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
    }

    [Theory]
    [InlineData("line-region", "col- ")] [InlineData("line-frame", "COL-")] [InlineData("region-frame", "cap-")]
    [InlineData("required-line", "col-")] [InlineData("required-project", "col-")]
    public void Malformed_or_required_identity_remains_refused_without_source_rewrite(string reference, string value)
    {
        var source = FileFor("bad"); using (var store = new LocalStore(source)) store.SaveProject(Project("none"));
        var node = JsonNode.Parse(ReadRawJson(source))!;
        if (reference == "required-project") node["project"]!["id"] = value;
        else if (reference == "required-line") ReferenceParent(node, "line-frame")["id"] = value;
        else ReferenceParent(node, reference)[ReferenceName(reference)] = value;
        ReplaceRawJson(source, node.ToJsonString()); var bytes = File.ReadAllBytes(source); var raw = ReadRawJson(source);
        using (var store = LocalStore.OpenExisting(source, readOnly: true)) Assert.ThrowsAny<Exception>(() => store.LoadProject(ProjectId));
        Assert.Throws<InvalidDataException>(() => LocalStore.VerifyBackupDatabase(source));
        Assert.Equal(bytes, File.ReadAllBytes(source)); Assert.Equal(raw, ReadRawJson(source));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
