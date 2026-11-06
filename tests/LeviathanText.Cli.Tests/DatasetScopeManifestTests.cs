using System.Text.Json.Nodes;
using LeviathanText.Training;

namespace LeviathanText.Cli.Tests;

/// <summary>Managed serialization controls only: no models, labels, dataset loader or fitting.</summary>
public sealed class DatasetScopeManifestTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("lt-dataset-scope-").FullName;

    private static TrainingRunManifest Manifest(int? locked) => new()
    {
        ModelName = "invented-scope", ConfigHash = "config", NumClasses = 3, HiddenSize = 8,
        WidthDownsampleFactor = 4, InputHeightPixels = 32, Dropout = 0, AugmentTraining = false,
        WeightDecay = 0, TrainLines = 2, DevLines = 1, TestLockedLines = locked,
        LoadedPartitions = locked is null ? DatasetPartitionAccess.TrainDev : DatasetPartitionAccess.TrainDevAndLocked,
        SplitHash = "split", LabelSetHash = "labels", CheckpointDigest = "", BestValidationCer = .5,
        EpochsCompleted = 0, CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Theory]
    [InlineData(null)] [InlineData(0)] [InlineData(7)]
    public void Unloaded_count_stays_distinct_from_a_loaded_zero_or_nonzero_count(int? count)
    {
        var original = Manifest(count); var path = TrainingRunManifestWriter.Write(_root, original);
        var reloaded = TrainingRunManifestWriter.Read(path);
        Assert.Equal(count, reloaded.TestLockedLines);
        Assert.Equal(original.LoadedPartitions, reloaded.LoadedPartitions);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.True(json.ContainsKey("TestLockedLines"));
        if (count is null) Assert.Null(json["TestLockedLines"]);
        else Assert.Equal(count, json["TestLockedLines"]!.GetValue<int>());
    }

    [Fact]
    public void Legacy_numeric_count_without_scope_remains_readable_without_invented_scope()
    {
        var path = TrainingRunManifestWriter.Write(_root, Manifest(7));
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); json.Remove("LoadedPartitions");
        File.WriteAllText(path, json.ToJsonString());
        var reloaded = TrainingRunManifestWriter.Read(path);
        Assert.Equal(7, reloaded.TestLockedLines); Assert.Null(reloaded.LoadedPartitions);
    }

    [Theory]
    [InlineData(DatasetPartitionAccess.TrainDev, 0)]
    [InlineData(DatasetPartitionAccess.TrainDevAndLocked, null)]
    [InlineData(DatasetPartitionAccess.TrainDevAndLocked, -1)]
    [InlineData((DatasetPartitionAccess)99, null)]
    public void Contradictory_explicit_scope_is_refused_before_overwriting_a_manifest(DatasetPartitionAccess scope, int? count)
    {
        var path = TrainingRunManifestWriter.Write(_root, Manifest(null)); var before = File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(_root,
            Manifest(count) with { LoadedPartitions = scope }));
        Assert.Equal(before, File.ReadAllBytes(path));
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["LoadedPartitions"] = (int)scope; json["TestLockedLines"] = count;
        File.WriteAllText(path, json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Read(path));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
