using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.Data.Sqlite;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class CaptureRelocationCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-relocation-cli", Guid.NewGuid().ToString("N"));
    private static readonly EntityId ProjectId = new(EntityKind.Collection, "relocation-cli");
    private static readonly EntityId SurfaceId = new(EntityKind.Surface, "page");
    private string SourceStore => Path.Combine(_root, "source", "store.db");
    private string OldImages => Path.Combine(_root, "source", "images");
    private string MovedImages => Path.Combine(_root, "moved-images");

    [Fact]
    public void Restored_project_rebinds_moved_images_and_exports_original_text_geometry_and_bytes()
    {
        var original = Seed();
        var imageBytes = File.ReadAllBytes(Path.Combine(OldImages, "page.png"));
        var backup = Path.Combine(_root, "backup");
        var restoredStore = Path.Combine(_root, "restored", "store.db");
        Assert.Equal(0, Run(SourceStore, "backup", backup).Exit);
        Assert.Equal(0, Run(restoredStore, "restore", backup).Exit);
        Directory.Move(Path.Combine(_root, "restored", "images"), MovedImages);
        Directory.Delete(OldImages, true);

        var result = Run(restoredStore, "migrate", "rebind-images", ProjectId.ToString(), OldImages, MovedImages);

        Assert.Equal(0, result.Exit);
        using var report = JsonDocument.Parse(result.Output);
        Assert.True(report.RootElement.GetProperty("Applied").GetBoolean());
        Assert.Single(report.RootElement.GetProperty("Captures").EnumerateArray());
        var restored = Load(restoredStore);
        Assert.Equal(original.Version + 1, restored.Version);
        var beforeObject = Assert.Single(original.Objects);
        var afterObject = Assert.Single(restored.Objects);
        var beforeSurface = Assert.Single(beforeObject.Surfaces);
        var afterSurface = Assert.Single(afterObject.Surfaces);
        var beforeCapture = Assert.Single(beforeSurface.Captures);
        var afterCapture = Assert.Single(afterSurface.Captures);
        Assert.Equal(ContractJson.ToJson(beforeCapture with { ImageLocation = Path.Combine(MovedImages, "page.png") }),
            ContractJson.ToJson(afterCapture));
        Assert.Equal(ContractJson.ToJson(beforeSurface with { Captures = afterSurface.Captures }),
            ContractJson.ToJson(afterSurface));
        Assert.Equal(beforeObject.Attachments.Count + 1, afterObject.Attachments.Count);
        Assert.Equal(JsonSerializer.Serialize(beforeObject.Attachments),
            JsonSerializer.Serialize(afterObject.Attachments.Take(beforeObject.Attachments.Count).ToArray()));
        var receipt = afterObject.Attachments[^1];
        Assert.Equal("capture-relocation", receipt.Role);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(receipt.Content))), receipt.Sha256);
        using var receiptJson = JsonDocument.Parse(receipt.Content);
        Assert.Equal("recorded-sha256", receiptJson.RootElement.GetProperty("captures")[0].GetProperty("Verification").GetString());

        var diplomatic = Run(restoredStore, "export", "diplomatic", ProjectId.ToString(), SurfaceId.ToString());
        Assert.Equal(0, diplomatic.Exit);
        Assert.Contains("Ἐν ἀρχῇ", diplomatic.Output);
        var exportedPage = Run(restoredStore, "export", "page", ProjectId.ToString(), SurfaceId.ToString());
        Assert.Equal(0, exportedPage.Exit);
        Assert.Contains("4,4 36,4 36,20 4,20", exportedPage.Output);
        Assert.Contains("4,18 36,18", exportedPage.Output);
        Assert.Contains("Ἐν ἀρχῇ", exportedPage.Output);

        var bundlePath = Path.Combine(_root, "relocated.zip");
        Assert.Equal(0, Run(restoredStore, "export", "bundle", ProjectId.ToString(), bundlePath, "--images").Exit);
        using var bundle = ZipFile.OpenRead(bundlePath);
        var image = Assert.Single(bundle.Entries, entry => entry.FullName.StartsWith("images/", StringComparison.Ordinal));
        using var imageStream = image.Open();
        using var copy = new MemoryStream();
        imageStream.CopyTo(copy);
        Assert.Equal(imageBytes, copy.ToArray());
        using var projectReader = new StreamReader(bundle.GetEntry("project.json")!.Open());
        Assert.Equal(NativeJson.Save(restored), NativeJson.Save(NativeJson.Load(projectReader.ReadToEnd())));
    }

    [Fact]
    public void Dry_run_verifies_the_moved_tree_without_changing_project_or_receipts()
    {
        var original = Seed();
        Directory.Move(OldImages, MovedImages);

        var result = Run(SourceStore, "migrate", "rebind-images", ProjectId.ToString(), OldImages, MovedImages, "--dry-run");

        Assert.Equal(0, result.Exit);
        using var report = JsonDocument.Parse(result.Output);
        Assert.False(report.RootElement.GetProperty("Applied").GetBoolean());
        Assert.Single(report.RootElement.GetProperty("Captures").EnumerateArray());
        Assert.Equal(NativeJson.Save(original), NativeJson.Save(Load(SourceStore)));
    }

    [Fact]
    public void Missing_store_dry_run_does_not_create_a_store_or_its_directory()
    {
        Directory.CreateDirectory(MovedImages);
        var missingStore = Path.Combine(_root, "missing-store", "store.db");

        Assert.Equal(1, Run(missingStore, "migrate", "rebind-images", ProjectId.ToString(), OldImages, MovedImages, "--dry-run").Exit);

        Assert.False(File.Exists(missingStore));
        Assert.False(Directory.Exists(Path.GetDirectoryName(missingStore)));
    }

    [Fact]
    public void Empty_existing_file_dry_run_does_not_initialize_a_database()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(MovedImages);
        var emptyStore = Path.Combine(_root, "empty.db");
        File.WriteAllBytes(emptyStore, []);

        Assert.Equal(2, Run(emptyStore, "migrate", "rebind-images", ProjectId.ToString(), OldImages, MovedImages, "--dry-run").Exit);

        Assert.Empty(File.ReadAllBytes(emptyStore));
    }

    [Fact]
    public void Dry_run_does_not_upgrade_an_actual_legacy_store()
    {
        var original = Seed();
        Directory.Move(OldImages, MovedImages);
        var legacyStore = Path.Combine(_root, "legacy.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = legacyStore, Pooling = false,
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE projects (id TEXT PRIMARY KEY, name TEXT NOT NULL, contract_version TEXT NOT NULL, json TEXT NOT NULL);
                PRAGMA user_version = 1;
                INSERT INTO projects (id, name, contract_version, json) VALUES ($id, $name, $contract, $json);
                """;
            command.Parameters.AddWithValue("$id", ProjectId.ToString());
            command.Parameters.AddWithValue("$name", original.Name);
            command.Parameters.AddWithValue("$contract", ContractVersion.Current);
            command.Parameters.AddWithValue("$json", NativeJson.Save(original));
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(legacyStore);

        Assert.Equal(2, Run(legacyStore, "migrate", "rebind-images", ProjectId.ToString(), OldImages, MovedImages, "--dry-run").Exit);

        Assert.Equal(before, File.ReadAllBytes(legacyStore));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--dry-run=true")]
    [InlineData("--dry-run --extra")]
    public void Malformed_flags_fail_before_creating_a_store(string flags)
    {
        var missingStore = Path.Combine(_root, "missing-store", "store.db");
        string[] arguments = ["migrate", "rebind-images", ProjectId.ToString(), OldImages, MovedImages, .. flags.Split(' ')];

        Assert.Equal(1, Run(missingStore, arguments).Exit);

        Assert.False(File.Exists(missingStore));
        Assert.False(Directory.Exists(Path.GetDirectoryName(missingStore)));
    }

    private NativeProject Seed()
    {
        Directory.CreateDirectory(OldImages);
        var image = Path.Combine(OldImages, "page.png");
        using (var bitmap = new SKBitmap(40, 24))
        {
            bitmap.Erase(SKColors.White);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(image);
            data.SaveTo(stream);
        }
        var captureId = new EntityId(EntityKind.Capture, "capture");
        var regionId = new EntityId(EntityKind.Region, "region");
        var lineId = new EntityId(EntityKind.Line, "line");
        var revisionId = new EntityId(EntityKind.TranscriptionRevision, "revision");
        var project = new NativeProject
        {
            Id = ProjectId, Name = "generated relocation fixture", Objects = [new NativeObject
            {
                Id = new(EntityKind.PhysicalObject, "object"),
                Attachments = [new NativeAttachment
                {
                    Id = "original", Name = "source.txt", MediaType = "text/plain", Role = "original-source",
                    Content = "source retained", Sha256 = Convert.ToHexStringLower(SHA256.HashData("source retained"u8)),
                }],
                Surfaces = [new NativeSurface
                {
                    Id = SurfaceId, Kind = "codex-page",
                    Captures = [new NativeCapture
                    {
                        Id = captureId, ImageLocation = image, WidthPx = 40, HeightPx = 24,
                        RightsEvidenceRef = "generated-test-fixture",
                        Frame = new CoordinateFrame
                        {
                            ImageRef = captureId, ImageChecksum = new Checksum
                            {
                                Algorithm = "sha256", Digest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(image))),
                            },
                        },
                    }],
                    Regions = [new NativeRegion
                    {
                        Id = regionId, FrameRef = captureId, ReadingOrderIndex = 0, LayoutSource = LayoutSource.Manual,
                        Polygon = [new(0, 0), new(39, 0), new(39, 23), new(0, 23)],
                    }],
                    Lines = [new NativeLine
                    {
                        Id = lineId, RegionId = regionId, FrameRef = captureId, DiplomaticRevisionId = revisionId,
                        Polygon = [new(4, 4), new(36, 4), new(36, 20), new(4, 20)], Baseline = [new(4, 18), new(36, 18)],
                    }],
                    Revisions = [new TextRevision
                    {
                        Id = revisionId, ScopeId = lineId, Layer = TextLayer.L1Diplomatic,
                        Author = new AgentRef { Id = "test-fixture", IsModel = false },
                        CreatedAt = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero), Text = "Ἐν ἀρχῇ",
                    }],
                }],
            }],
        };
        using var store = new LocalStore(SourceStore);
        store.SaveProject(project);
        return store.LoadProject(ProjectId)!;
    }

    private static NativeProject Load(string path)
    {
        using var store = new LocalStore(path);
        return store.LoadProject(ProjectId)!;
    }

    private static (int Exit, string Output) Run(string store, params string[] arguments)
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            return (Program.Main(["--store", store, .. arguments]), output.ToString());
        }
        finally { Console.SetOut(original); }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
