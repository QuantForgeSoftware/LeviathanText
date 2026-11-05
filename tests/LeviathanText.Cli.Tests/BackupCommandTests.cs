using System.Security.Cryptography;
using System.Text.Json;

namespace LeviathanText.Cli.Tests;

public sealed class BackupCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-backup-cli", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Restore_uses_selected_store_without_creating_it_before_validation()
    {
        var backup = CreateBackup();
        var destination = Path.Combine(_root, "restored", "store.db");

        Assert.Equal(0, Program.Main(["--store", destination, "restore", backup]));
        Assert.Equal(File.ReadAllBytes(Path.Combine(backup, "store.db")), File.ReadAllBytes(destination));
        Assert.Equal(1, Program.Main(["--store", destination, "restore", backup]));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    public void Restore_rejects_image_names_with_directory_components(string name)
    {
        var backup = CreateBackup();
        Directory.CreateDirectory(Path.Combine(backup, "images"));
        var outside = Path.Combine(backup, "outside.png");
        File.WriteAllText(outside, "outside the image directory");
        File.WriteAllText(Path.Combine(backup, "backup-manifest.json"), JsonSerializer.Serialize(new
        {
            storeChecksum = Hash(Path.Combine(backup, "store.db")),
            images = new[] { new { name, sha256 = Hash(outside), size = new FileInfo(outside).Length } },
        }));
        var destination = Path.Combine(_root, "restored", "store.db");

        Assert.Equal(1, Program.Main(["--store", destination, "restore", backup]));
        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(Path.Combine(_root, "restored", "outside.png")));
    }

    [Fact]
    public void Restore_keeps_existing_database_when_image_target_is_obstructed()
    {
        var backup = CreateBackupWithImage();
        var destination = Path.Combine(_root, "restored", "store.db");
        Assert.Equal(0, Program.Main(["--store", destination, "project", "new", "keep me"]));
        File.WriteAllText(Path.Combine(_root, "restored", "images"), "blocks image directory");
        var before = File.ReadAllBytes(destination);

        Assert.Equal(1, Program.Main(["--store", destination, "restore", backup, "--force"]));
        Assert.Equal(before, File.ReadAllBytes(destination));
    }

    [Fact]
    public void Backup_does_not_overwrite_an_existing_snapshot()
    {
        var backup = CreateBackup();
        var store = Path.Combine(_root, "source", "store.db");
        var before = File.ReadAllBytes(Path.Combine(backup, "store.db"));
        Assert.Equal(0, Program.Main(["--store", store, "project", "new", "later state"]));

        Assert.Equal(1, Program.Main(["--store", store, "backup", backup]));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(backup, "store.db")));
    }

    [Fact]
    public void Force_restore_replaces_snapshot_entries_and_keeps_unlisted_cache_files()
    {
        var backup = CreateBackupWithImage();
        var destination = Path.Combine(_root, "restored", "store.db");
        Assert.Equal(0, Program.Main(["--store", destination, "project", "new", "replace me"]));
        var cache = Path.Combine(_root, "restored", "images");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "test.png"), "old cache");
        File.WriteAllText(Path.Combine(cache, "unlisted.png"), "preserve");

        Assert.Equal(0, Program.Main(["--store", destination, "restore", backup, "--force"]));
        Assert.Equal(File.ReadAllBytes(Path.Combine(backup, "store.db")), File.ReadAllBytes(destination));
        Assert.Equal("cached bytes", File.ReadAllText(Path.Combine(cache, "test.png")));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(cache, "unlisted.png")));
    }

    [Fact]
    public void Restore_respects_explicit_target_after_force()
    {
        var backup = CreateBackup();
        var selected = Path.Combine(_root, "selected", "store.db");
        var requested = Path.Combine(_root, "requested", "store.db");
        Assert.Equal(0, Program.Main(["--store", selected, "project", "new", "keep me"]));
        var before = File.ReadAllBytes(selected);

        Assert.Equal(0, Program.Main(["--store", selected, "restore", backup, "--force", "--into", requested]));
        Assert.Equal(before, File.ReadAllBytes(selected));
        Assert.Equal(File.ReadAllBytes(Path.Combine(backup, "store.db")), File.ReadAllBytes(requested));
    }

    [Fact]
    public void Restore_refuses_checksum_valid_non_database_bytes()
    {
        var backup = CreateBackup();
        var invalid = Path.Combine(backup, "store.db");
        File.WriteAllText(invalid, "not a database");
        File.WriteAllText(Path.Combine(backup, "backup-manifest.json"), JsonSerializer.Serialize(new
        {
            storeChecksum = Hash(invalid),
            images = Array.Empty<object>(),
        }));
        var destination = Path.Combine(_root, "selected", "store.db");
        Assert.Equal(0, Program.Main(["--store", destination, "project", "new", "keep me"]));
        var before = File.ReadAllBytes(destination);

        Assert.Equal(1, Program.Main(["--store", destination, "restore", backup, "--force"]));
        Assert.Equal(before, File.ReadAllBytes(destination));
    }

    private string CreateBackupWithImage()
    {
        var images = Path.Combine(_root, "source", "images");
        Directory.CreateDirectory(images);
        File.WriteAllText(Path.Combine(images, "test.png"), "cached bytes");
        return CreateBackup();
    }

    private string CreateBackup()
    {
        var store = Path.Combine(_root, "source", "store.db");
        var backup = Path.Combine(_root, "backup");
        Assert.Equal(0, Program.Main(["--store", store, "project", "new", "backup test"]));
        Assert.Equal(0, Program.Main(["--store", store, "backup", backup]));
        return backup;
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
