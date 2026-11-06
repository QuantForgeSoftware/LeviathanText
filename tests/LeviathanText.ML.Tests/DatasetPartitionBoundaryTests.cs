using LeviathanText.Contracts;
using LeviathanText.Imaging;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;

namespace LeviathanText.ML.Tests;

// Generated PAGE strings, virtual filenames and tiny untrained rasters only. No corpus/model/optimizer IO.
public sealed class DatasetPartitionBoundaryTests
{
    [Fact]
    public void TrainDev_skips_reserved_content_before_ReadXml_and_Decode_preserving_original_indices()
    {
        using var io = new RecordingIo(refuseReserved: true);
        var snapshot = P01LineDataset.LoadWithIo("/virtual/generated", DatasetPartitionAccess.TrainDev, io.Adapter, ".png", targetHeight: 8);
        Assert.Equal(DatasetPartitionAccess.TrainDev, snapshot.LoadedPartitions);
        Assert.Equal(5, snapshot.TotalLines);
        Assert.Equal(4, snapshot.Train.Count);
        Assert.Single(snapshot.Dev);
        Assert.Empty(snapshot.TestLocked);
        Assert.Equal(new[] { "02", "03", "04", "06", "07" }, io.Reads);
        Assert.Equal(io.Reads, io.Decodes);
        Assert.Equal(7, snapshot.WitnessSplitMap.Count);
        Assert.Equal("test", snapshot.WitnessSplitMap["00"]);
        Assert.Equal("test", snapshot.WitnessSplitMap["05"]);
        Assert.Equal("dev", snapshot.WitnessSplitMap["07"]);
        Assert.Equal("train", snapshot.WitnessSplitMap["06"]);
        Assert.False(snapshot.WitnessSplitMap.ContainsKey("01"));
        Assert.Equal(new[] { "unannotated" }, snapshot.UnannotatedImages);
        Assert.All(io.Bitmaps, bitmap => Assert.Equal(IntPtr.Zero, bitmap.Handle));
    }

    [Fact]
    public void Explicit_generated_locked_access_retains_full_metadata_and_loads_reserved_content()
    {
        using var io = new RecordingIo(refuseReserved: false);
        var snapshot = P01LineDataset.LoadWithIo("/virtual/generated", DatasetPartitionAccess.TrainDevAndLocked, io.Adapter, ".png", targetHeight: 8);
        Assert.Equal(DatasetPartitionAccess.TrainDevAndLocked, snapshot.LoadedPartitions);
        Assert.Equal(7, snapshot.TotalLines);
        Assert.Equal(2, snapshot.TestLocked.Count);
        Assert.Equal(new[] { "00", "02", "03", "04", "05", "06", "07" }, io.Reads);
        Assert.Equal(io.Reads, io.Decodes);
        Assert.All(io.Bitmaps, bitmap => Assert.Equal(IntPtr.Zero, bitmap.Handle));
    }

    [Theory]
    [InlineData(-1, 5, 6)]
    [InlineData(2, 5, 6)]
    [InlineData(0, 0, 6)]
    [InlineData(0, -1, 6)]
    [InlineData(0, 5, 0)]
    [InlineData(0, 5, -1)]
    public void Invalid_access_or_divisors_fail_before_any_IO(int access, int testEveryNth, int devEveryNth)
    {
        int calls = 0;
        T Deny<T>() { calls++; throw new Exception("Unexpected IO"); }
        var io = new P01LineDataset.DatasetIo(_ => Deny<bool>(), (_, _) => Deny<string[]>(), _ => Deny<bool>(),
            _ => Deny<string>(), _ => Deny<ImageDecoder.DecodedImage>());
        Assert.Throws<ArgumentOutOfRangeException>(() => P01LineDataset.LoadWithIo("unused", (DatasetPartitionAccess)access,
            io, testEveryNth: testEveryNth, devEveryNth: devEveryNth));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Later_generated_failure_releases_all_prior_decoded_rasters_without_reserved_reads()
    {
        using var io = new RecordingIo(refuseReserved: true, failAt: "03");
        Assert.Throws<InvalidDataException>(() => P01LineDataset.LoadWithIo("/virtual/generated", DatasetPartitionAccess.TrainDev,
            io.Adapter, ".png", targetHeight: 8));
        Assert.Equal(new[] { "02", "03" }, io.Reads);
        Assert.Single(io.Bitmaps);
        Assert.All(io.Bitmaps, bitmap => Assert.Equal(IntPtr.Zero, bitmap.Handle));
    }

    [Fact]
    public void Existing_public_Load_signatures_default_to_TrainDev_without_opening_generated_reserved_content()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-generated-partition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // Deliberately invalid: reaching this generated reserved label is observable without a real dataset.
            File.WriteAllText(Path.Combine(directory, "00.xml"), "RESERVED-GENERATED-SENTINEL-NOT-XML");
            File.WriteAllText(Path.Combine(directory, "00.png"), "RESERVED-GENERATED-SENTINEL-NOT-PNG");
            File.WriteAllText(Path.Combine(directory, "01.xml"), Page);
            using (var bitmap = new SKBitmap(8, 8))
            {
                bitmap.Erase(SKColors.White);
                using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(Path.Combine(directory, "01.png"));
                png.SaveTo(stream);
            }
            var compiledSignature = P01LineDataset.Load(directory, ".png", 5, 6, 8);
            var optionalSignature = P01LineDataset.Load(directory, imageExtension: ".png", targetHeight: 8);
            foreach (var snapshot in new[] { compiledSignature, optionalSignature })
            {
                Assert.Equal(DatasetPartitionAccess.TrainDev, snapshot.LoadedPartitions);
                Assert.Empty(snapshot.TestLocked);
                Assert.Single(snapshot.Dev);
                Assert.Equal("test", snapshot.WitnessSplitMap["00"]);
            }
            Assert.ThrowsAny<Exception>(() => P01LineDataset.LoadPartitions(directory, DatasetPartitionAccess.TrainDevAndLocked,
                ".png", targetHeight: 8));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Legacy_manual_snapshot_does_not_claim_loaded_partition_scope()
    {
        var snapshot = new P01LineDataset.Snapshot([], [], [], [], 0, new Dictionary<string, string>());
        Assert.Null(snapshot.LoadedPartitions);
    }

    private const string Page = """
        <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
          <Page imageFilename="generated.png" imageWidth="8" imageHeight="8">
            <TextRegion id="r"><Coords points="0,0 7,0 7,7 0,7"/>
              <TextLine id="l"><Coords points="0,0 7,0 7,7 0,7"/>
                <TextEquiv><Unicode>generated</Unicode></TextEquiv>
              </TextLine>
            </TextRegion>
          </Page>
        </PcGts>
        """;

    private sealed class RecordingIo : IDisposable
    {
        internal List<string> Reads { get; } = [];
        internal List<string> Decodes { get; } = [];
        internal List<SKBitmap> Bitmaps { get; } = [];
        internal P01LineDataset.DatasetIo Adapter { get; }
        internal RecordingIo(bool refuseReserved, string? failAt = null)
        {
            string[] xml = Enumerable.Range(0, 8).Select(i => $"/virtual/generated/{i:00}.xml").Reverse().ToArray();
            Adapter = new(_ => true,
                (_, pattern) => pattern == "*.xml" ? xml : ["/virtual/generated/02.png", "/virtual/generated/unannotated.png"],
                path => Path.GetFileNameWithoutExtension(path) != "01" && Path.GetFileNameWithoutExtension(path) != "unannotated",
                path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    Reads.Add(name);
                    if (refuseReserved && name is "00" or "05") throw new Exception("Reserved generated XML read");
                    if (name == failAt) throw new InvalidDataException("Generated later-witness failure");
                    return Page;
                },
                path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    Decodes.Add(name);
                    if (refuseReserved && name is "00" or "05") throw new Exception("Reserved generated image decode");
                    var bitmap = new SKBitmap(8, 8);
                    bitmap.Erase(SKColors.White);
                    Bitmaps.Add(bitmap);
                    return new ImageDecoder.DecodedImage(bitmap, new Checksum { Algorithm = "unavailable", Digest = "generated" }, 0);
                });
        }
        public void Dispose() { foreach (var bitmap in Bitmaps) bitmap.Dispose(); }
    }
}
