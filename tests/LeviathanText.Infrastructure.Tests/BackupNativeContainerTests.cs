using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

public sealed class BackupNativeContainerTests : IDisposable
{
    readonly string root = Directory.CreateTempSubdirectory("lt-backup-containers-").FullName;
    static NativeProject Fixture() {
        var frame = new EntityId(EntityKind.Capture, "generated"); var lineId = new EntityId(EntityKind.Line, "generated");
        return new NativeProject { Id = new(EntityKind.Collection, "generated"), Name = "generated", Objects = [new NativeObject {
            Id = new(EntityKind.PhysicalObject, "generated"),
            Fragments = [new NativeFragment { Id = new(EntityKind.Fragment, "generated"), SurfaceIds = [new(EntityKind.Surface, "generated")] }],
            Surfaces = [new NativeSurface { Id = new(EntityKind.Surface, "generated"), Kind = "generated",
                Captures = [new NativeCapture { Id = frame, ImageLocation = "/generated/not-opened", Frame = new CoordinateFrame {
                    ImageRef = frame, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "generated" } } }],
                Regions = [new NativeRegion { Id = new(EntityKind.Region, "generated"), FrameRef = default }],
                Lines = [new NativeLine { Id = lineId, RegionId = default, FrameRef = default }],
                Revisions = [new TextRevision { Id = new(EntityKind.TranscriptionRevision, "generated"), ScopeId = lineId,
                    Layer = TextLayer.L1Diplomatic, Text = "GENERATED", CreatedAt = DateTimeOffset.UnixEpoch, Author = new AgentRef { Id = "generated", IsModel = false } }],
            }] }] };
    }
    [Theory]
    [InlineData("objects")]
    [InlineData("objects/0")]
    [InlineData("objects/0/surfaces")]
    [InlineData("objects/0/surfaces/0")]
    [InlineData("objects/0/externalRefs")]
    [InlineData("objects/0/metadata")]
    [InlineData("objects/0/attachments")]
    [InlineData("objects/0/fragments")]
    [InlineData("objects/0/fragments/0")]
    [InlineData("objects/0/fragments/0/surfaceIds")]
    [InlineData("objects/0/surfaces/0/externalRefs")]
    [InlineData("objects/0/surfaces/0/captures")]
    [InlineData("objects/0/surfaces/0/captures/0")]
    [InlineData("objects/0/surfaces/0/regions")]
    [InlineData("objects/0/surfaces/0/regions/0")]
    [InlineData("objects/0/surfaces/0/regions/0/polygon")]
    [InlineData("objects/0/surfaces/0/regions/0/languages")]
    [InlineData("objects/0/surfaces/0/lines")]
    [InlineData("objects/0/surfaces/0/lines/0")]
    [InlineData("objects/0/surfaces/0/lines/0/polygon")]
    [InlineData("objects/0/surfaces/0/lines/0/baseline")]
    [InlineData("objects/0/surfaces/0/lines/0/languages")]
    [InlineData("objects/0/surfaces/0/lines/0/tags")]
    [InlineData("objects/0/surfaces/0/revisions")]
    [InlineData("objects/0/surfaces/0/revisions/0")]
    [InlineData("objects/0/surfaces/0/revisions/0/graphemeConfidences")]
    [InlineData("objects/0/surfaces/0/annotations")]
    [InlineData("objects/0/surfaces/0/reviewDecisions")]
    [InlineData("objects/0/surfaces/0/annotationRetractions")]
    [InlineData("objects/0/surfaces/0/geometryEdits")]
    public void Explicit_null_container_or_entry_is_refused_read_only(string path) {
        string file = Path.Combine(root, "source.db"); using (var store = new LocalStore(file)) store.SaveProject(Fixture());
        var document = JsonNode.Parse(NativeJson.Save(Fixture()))!; JsonNode parent = document["project"]!;
        string[] parts = path.Split('/');
        foreach (var part in parts[..^1]) parent = parent is JsonArray a ? a[int.Parse(part)]! : parent[part]!;
        if (parent is JsonArray array) array[int.Parse(parts[^1])] = null; else parent[parts[^1]] = null;
        string payload = document.ToJsonString(); Assert.NotNull(NativeJson.Load(payload));
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString())) {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE projects SET json=$payload";
            command.Parameters.AddWithValue("$payload", payload); command.ExecuteNonQuery(); }
        byte[] before = File.ReadAllBytes(file);
        Assert.Throws<InvalidDataException>(() => LocalStore.VerifyBackupDatabase(file));
        Assert.Equal(before, File.ReadAllBytes(file));
    }
    [Fact]
    public void Optional_unplaced_values_and_absent_default_collections_are_not_invented_or_rejected() {
        string file = Path.Combine(root, "legacy.db"); var project = Fixture();
        var document = JsonNode.Parse(NativeJson.Save(project))!;
        var obj = document["project"]!["objects"]![0]!.AsObject(); obj.Remove("attachments"); obj.Remove("externalRefs");
        obj["metadata"]!["legacy-null-value"] = null;
        var surface = obj["surfaces"]![0]!.AsObject(); surface.Remove("annotations"); surface.Remove("geometryEdits");
        surface["lines"]![0]!["direction"] = null; surface["lines"]![0]!["diplomaticRevisionId"] = null;
        using (var store = new LocalStore(file)) store.SaveProject(project);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString())) {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE projects SET json=$payload";
            command.Parameters.AddWithValue("$payload", document.ToJsonString()); command.ExecuteNonQuery(); }
        byte[] before = File.ReadAllBytes(file); LocalStore.VerifyBackupDatabase(file); Assert.Equal(before, File.ReadAllBytes(file));
    }
    [Theory]
    [InlineData("tag-list")]
    [InlineData("tag-item")]
    [InlineData("language-item")]
    public void Nested_list_values_and_items_are_checked_without_reinterpreting_metadata(string change) {
        var project = Fixture(); var obj = project.Objects[0]; var surface = obj.Surfaces[0]; var line = surface.Lines[0];
        line = change == "language-item" ? line with { Languages = [null!] }
            : line with { Tags = new Dictionary<string, IReadOnlyList<string>> { ["generated"] = change == "tag-list" ? null! : [null!] } };
        project = project with { Objects = [obj with { Surfaces = [surface with { Lines = [line] }] }] };
        string file = Path.Combine(root, "nested.db"); using (var store = new LocalStore(file)) store.SaveProject(project);
        byte[] before = File.ReadAllBytes(file);
        Assert.Throws<InvalidDataException>(() => LocalStore.VerifyBackupDatabase(file));
        Assert.Equal(before, File.ReadAllBytes(file));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
