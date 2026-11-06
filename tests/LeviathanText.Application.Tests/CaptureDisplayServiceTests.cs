using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Application.Tests;

public sealed class CaptureDisplayServiceTests : IDisposable
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Alias_dimensions_are_verified_against_observed_raw_pixels_when_selected_dimensions_are_unknown(bool width)
    {
        var project = Project(Image(32, 16, false), null, null); var surface = project.Objects[0].Surfaces[0]; var capture = surface.Captures[0];
        var alias = capture with { Id = new(EntityKind.Capture, "dimension-alias"), WidthPx = width ? 99 : 32,
            HeightPx = width ? 16 : 99, ImageLocation = Path.Combine(_root, "alias-must-not-be-read") };
        var aliased = project with { Objects = [project.Objects[0] with { Surfaces = [surface with { Captures = [capture, alias] }] }] };
        var before = NativeJson.Save(aliased); using var display = new CaptureDisplayService();
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(aliased, SurfaceId, CaptureId));
        Assert.Equal(ErrorCode.FrameUnregistered, error.Error.Code); Assert.Equal(before, NativeJson.Save(aliased));
        Assert.DoesNotContain(alias.ImageLocation, error.Message);
    }

    [Fact]
    public async Task Queued_cancellation_and_double_dispose_do_not_release_someone_elses_admission()
    {
        var project = Project(Image(32, 16, false), 32, 16); using var display = new CaptureDisplayService();
        using var first = await display.RenderAsync(project, SurfaceId, CaptureId);
        using var canceled = new CancellationTokenSource(); var waiting = display.RenderAsync(project, SurfaceId, CaptureId, canceled.Token);
        canceled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        var third = display.RenderAsync(project, SurfaceId, CaptureId);
        await Assert.ThrowsAsync<TimeoutException>(() => third.WaitAsync(TimeSpan.FromMilliseconds(100)));
        first.Dispose(); using var admitted = await third.WaitAsync(TimeSpan.FromSeconds(5));
        first.Dispose(); var fourth = display.RenderAsync(project, SurfaceId, CaptureId);
        await Assert.ThrowsAsync<TimeoutException>(() => fourth.WaitAsync(TimeSpan.FromMilliseconds(100)));
        admitted.Dispose(); using var last = await fourth.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Only_verified_compatible_aliases_can_share_a_native_coordinate_frame()
    {
        var project = Project(Image(32, 16, false), 32, 16); var surface = project.Objects[0].Surfaces[0]; var capture = surface.Captures[0];
        var alias = capture with { Id = new(EntityKind.Capture, "alias"), ImageLocation = Path.Combine(_root, "not-read") };
        var compatible = project with { Objects = [project.Objects[0] with { Surfaces = [surface with { Captures = [capture, alias] }] }] };
        using var display = new CaptureDisplayService(); using var result = await display.RenderAsync(compatible, SurfaceId, CaptureId); result.Dispose();
        var unverified = compatible with { Objects = [compatible.Objects[0] with { Surfaces = [surface with { Captures = [capture, alias with { Frame = alias.Frame with { ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } } }] }] }] };
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(unverified, SurfaceId, CaptureId)); Assert.Equal(ErrorCode.FrameUnregistered, error.Error.Code);
    }

    [Theory]
    [InlineData("png-prefix")] [InlineData("jpeg-prefix")] [InlineData("png-empty")]
    public async Task Truncated_format_signatures_are_actionable_refusals_without_native_mutation(string kind)
    {
        byte[] bytes = kind switch { "jpeg-prefix" => [255,216], "png-empty" => [137,80,78,71,13,10,26,10], _ => [137,80,78,71] };
        var project = Project(bytes, null, null); var before = NativeJson.Save(project); using var display = new CaptureDisplayService();
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(project, SurfaceId, CaptureId));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code); Assert.Contains("PNG/JPEG", error.Message); Assert.Equal(before, NativeJson.Save(project));
    }

    [Fact]
    public async Task PNG_alpha_is_preserved_in_normalized_raw_preview()
    {
        using var bitmap = new SKBitmap(2, 1); bitmap.SetPixel(0, 0, new SKColor(0, 0, 0, 128)); bitmap.SetPixel(1, 0, SKColors.Transparent);
        using var source = bitmap.Encode(SKEncodedImageFormat.Png, 100); var project = Project(source.ToArray(), 2, 1);
        using var display = new CaptureDisplayService(); using var rendered = await display.RenderAsync(project, SurfaceId, CaptureId);
        using var result = SKBitmap.Decode(rendered.PngBytes); Assert.Equal(128, result.GetPixel(0, 0).Alpha); Assert.Equal(0, result.GetPixel(1, 0).Alpha);
    }

    private readonly string _root = Directory.CreateTempSubdirectory("lt-capture-display-").FullName;
    private static readonly EntityId ProjectId = new(EntityKind.Collection, "display");
    private static readonly EntityId SurfaceId = new(EntityKind.Surface, "display");
    private static readonly EntityId CaptureId = new(EntityKind.Capture, "display");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capture_only_PNG_or_JPEG_is_normalized_without_native_or_source_mutation(bool jpeg)
    {
        var bytes = Image(32, 16, jpeg); var project = Project(bytes, 32, 16); var before = NativeJson.Save(project);
        using var display = new CaptureDisplayService();
        using var result = await display.RenderAsync(project, SurfaceId, CaptureId);
        Assert.Equal(1, result.Descriptor.SchemaVersion); Assert.Equal("raw", result.Descriptor.Orientation);
        Assert.Equal("verified", result.Descriptor.RecordedChecksumStatus);
        Assert.Equal(Hash(bytes), result.Descriptor.ObservedSourceChecksum.Digest);
        Assert.Equal(Hash(result.PngBytes), result.Descriptor.DisplayChecksum.Digest);
        Assert.Equal(32, result.Descriptor.SourceWidth); Assert.Equal(16, result.Descriptor.SourceHeight);
        Assert.Equal(1, result.Descriptor.ScaleX); Assert.Equal(1, result.Descriptor.ScaleY);
        Assert.Equal("image/png", result.Descriptor.MediaType); Assert.Equal(CaptureId.ToString(), result.Descriptor.SourceFrameRef);
        Assert.Equal(before, NativeJson.Save(project)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_root, "source")));
        Assert.Empty(project.Objects[0].Surfaces[0].Lines);
        using var source = SKBitmap.Decode(bytes); using var png = SKBitmap.Decode(result.PngBytes);
        Assert.Equal(source.GetPixel(0, 0), png.GetPixel(0, 0)); Assert.Equal(source.GetPixel(31, 15), png.GetPixel(31, 15));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public async Task All_EXIF_orientations_keep_nonsquare_raw_frame_and_fiducials(int orientation)
    {
        var jpeg = Image(32, 16, true); var bytes = WithExif(jpeg, orientation); var project = Project(bytes, 32, 16);
        using var display = new CaptureDisplayService(); using var result = await display.RenderAsync(project, SurfaceId, CaptureId);
        Assert.Equal((32, 16), (result.Descriptor.SourceWidth, result.Descriptor.SourceHeight));
        using var raw = SKBitmap.Decode(jpeg); using var png = SKBitmap.Decode(result.PngBytes);
        Assert.Equal((32, 16), (png.Width, png.Height));
        foreach (var (x, y) in new[] { (1, 1), (30, 1), (1, 14), (30, 14) }) Assert.Equal(raw.GetPixel(x, y), png.GetPixel(x, y));
        Assert.Equal("raw", result.Descriptor.Orientation);
    }

    [Fact]
    public async Task Rounded_preview_dimensions_have_exact_independent_inverse_scales()
    {
        var project = Project(Image(3001, 1500, false), 3001, 1500);
        using var display = new CaptureDisplayService(); using var result = await display.RenderAsync(project, SurfaceId, CaptureId); var d = result.Descriptor;
        Assert.True(d.DisplayWidth <= 2048 && d.DisplayHeight <= 2048 && (long)d.DisplayWidth * d.DisplayHeight <= 4_000_000);
        Assert.Equal((double)d.DisplayWidth / 3001, d.ScaleX); Assert.Equal((double)d.DisplayHeight / 1500, d.ScaleY);
        Assert.Equal(3001, d.DisplayWidth / d.ScaleX, 8); Assert.Equal(1500, d.DisplayHeight / d.ScaleY, 8);
        using var bitmap = SKBitmap.Decode(result.PngBytes); Assert.Equal((d.DisplayWidth, d.DisplayHeight), (bitmap.Width, bitmap.Height));
    }

    [Fact]
    public async Task Unknown_legacy_checksum_and_dimensions_stay_explicitly_unavailable()
    {
        var project = Project(Image(32, 16, false), null, null, unavailable: true); var before = NativeJson.Save(project);
        using var display = new CaptureDisplayService(); using var result = await display.RenderAsync(project, SurfaceId, CaptureId);
        Assert.Equal("unavailable", result.Descriptor.RecordedChecksumStatus);
        Assert.Equal("unavailable", result.Descriptor.RecordedSourceChecksum.Algorithm);
        Assert.Equal("sha256", result.Descriptor.ObservedSourceChecksum.Algorithm);
        Assert.Equal((32, 16), (result.Descriptor.SourceWidth, result.Descriptor.SourceHeight)); Assert.Equal(before, NativeJson.Save(project));
    }

    [Fact]
    public async Task Same_pixels_different_encoded_source_changes_derivative_identity()
    {
        var bytes = Image(32, 16, true); using var display = new CaptureDisplayService();
        using var first = await display.RenderAsync(Project(bytes, 32, 16, true), SurfaceId, CaptureId);
        first.Dispose();
        using var second = await display.RenderAsync(Project(WithExif(bytes, 2), 32, 16, true), SurfaceId, CaptureId);
        Assert.Equal(first.PngBytes, second.PngBytes); Assert.NotEqual(first.Descriptor.DerivativeId, second.Descriptor.DerivativeId);
    }

    [Theory]
    [InlineData("checksum")] [InlineData("width")] [InlineData("height")]
    public async Task Changed_source_or_known_dimensions_refuse_without_mutation(string kind)
    {
        var project = Project(Image(32, 16, false), kind == "width" ? 31 : 32, kind == "height" ? 15 : 16);
        var before = NativeJson.Save(project); if (kind == "checksum") File.WriteAllBytes(Path.Combine(_root, "source"), Image(33, 16, false));
        using var display = new CaptureDisplayService(); var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(project, SurfaceId, CaptureId));
        Assert.Equal(kind == "checksum" ? ErrorCode.ChecksumMismatch : ErrorCode.FrameUnregistered, error.Error.Code);
        Assert.Equal(before, NativeJson.Save(project)); Assert.DoesNotContain(_root, error.Message);
    }

    [Theory]
    [InlineData(12001, 1)] [InlineData(4001, 4000)]
    public async Task Valid_compressed_oversized_PNG_is_refused_before_bitmap_decode(int width, int height)
    {
        var project = Project(WhitePng(width, height), width, height); using var display = new CaptureDisplayService();
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(project, SurfaceId, CaptureId)); Assert.Equal(ErrorCode.RejectedArchive, error.Error.Code);
    }

    [Fact]
    public async Task Encoded_limit_cancellation_and_parallel_admission_leave_service_usable()
    {
        var project = Project(Image(32, 16, false), 32, 16); using var display = new CaptureDisplayService();
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => display.RenderAsync(project, SurfaceId, CaptureId, canceled.Token));
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => { using var result = await display.RenderAsync(project, SurfaceId, CaptureId); return result.Descriptor.DerivativeId; }));
        Assert.Single(results.Distinct());
        using (var file = File.OpenWrite(Path.Combine(_root, "source"))) file.SetLength(CaptureDisplayService.MaxEncodedBytes + 1L);
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(project, SurfaceId, CaptureId)); Assert.Equal(ErrorCode.RejectedArchive, error.Error.Code);
    }

    [Theory]
    [InlineData("remote")] [InlineData("relative")] [InlineData("missing")] [InlineData("symlink")] [InlineData("tiff")] [InlineData("malformed")]
    public async Task Unsupported_or_untrusted_sources_have_safe_actionable_refusals(string kind)
    {
        var project = Project(kind == "tiff" ? "II*\0tiff-not-a-PNG"u8.ToArray() : kind == "malformed" ? "invalid-image"u8.ToArray() : Image(32, 16, false), 32, 16);
        var capture = project.Objects[0].Surfaces[0].Captures[0];
        if (kind is "remote" or "relative") project = Replace(project, capture with { ImageLocation = kind == "remote" ? "https://example.invalid/private.jpg" : "../private.png" });
        if (kind == "missing") File.Delete(capture.ImageLocation);
        if (kind == "symlink") { var link = Path.Combine(_root, "link"); File.CreateSymbolicLink(link, capture.ImageLocation); project = Replace(project, capture with { ImageLocation = link }); }
        using var display = new CaptureDisplayService(); var before = NativeJson.Save(project);
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(project, SurfaceId, CaptureId));
        Assert.DoesNotContain(_root, error.Message); Assert.DoesNotContain("https://", error.Message); Assert.Equal(before, NativeJson.Save(project));
    }

    [Fact]
    public async Task Duplicate_and_cross_surface_capture_id_resolution_never_first_matches()
    {
        var project = Project(Image(32, 16, false), 32, 16); var surface = project.Objects[0].Surfaces[0];
        using var display = new CaptureDisplayService();
        var duplicate = project with { Objects = [project.Objects[0] with { Surfaces = [surface with { Captures = [surface.Captures[0], surface.Captures[0]] }] }] };
        var error = await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(duplicate, SurfaceId, CaptureId)); Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        await Assert.ThrowsAsync<LeviathanException>(() => display.RenderAsync(project, new(EntityKind.Surface, "other"), CaptureId));
    }

    private NativeProject Project(byte[] bytes, int? width, int? height, bool unavailable = false)
    {
        var path = Path.Combine(_root, "source"); File.WriteAllBytes(path, bytes);
        var capture = new NativeCapture { Id = CaptureId, ImageLocation = path, WidthPx = width, HeightPx = height,
            Frame = new CoordinateFrame { ImageRef = CaptureId, ImageChecksum = new Checksum { Algorithm = unavailable ? "unavailable" : "sha256", Digest = unavailable ? "pending" : Hash(bytes) } } };
        return new NativeProject { Id = ProjectId, Name = "generated display", Objects = [new NativeObject { Id = new(EntityKind.PhysicalObject, "display"), Surfaces = [new NativeSurface { Id = SurfaceId, Kind = "page", Captures = [capture], Regions = [], Lines = [] }] }] };
    }
    private static NativeProject Replace(NativeProject p, NativeCapture c) => p with { Objects = [p.Objects[0] with { Surfaces = [p.Objects[0].Surfaces[0] with { Captures = [c] }] }] };
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static byte[] Image(int width, int height, bool jpeg)
    {
        using var bitmap = new SKBitmap(width, height); using (var canvas = new SKCanvas(bitmap)) { canvas.Clear(SKColors.White); using var red = new SKPaint { Color = SKColors.Red }; using var blue = new SKPaint { Color = SKColors.Blue }; canvas.DrawRect(0, 0, width / 2f, height / 2f, red); canvas.DrawRect(width / 2f, height / 2f, width / 2f, height / 2f, blue); }
        using var encoded = bitmap.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 100); return encoded.ToArray();
    }
    private static byte[] WithExif(byte[] jpeg, int orientation)
    {
        byte[] payload = [69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,(byte)orientation,0,0,0,0,0,0,0];
        return [.. jpeg.AsSpan(0, 2).ToArray(), 255,225,0,(byte)(payload.Length + 2), .. payload, .. jpeg.AsSpan(2).ToArray()];
    }
    private static byte[] WhitePng(int width, int height)
    {
        using var output = new MemoryStream(); output.Write(new byte[] {137,80,78,71,13,10,26,10});
        void Chunk(string type, byte[] bytes) { var t = System.Text.Encoding.ASCII.GetBytes(type); Span<byte> n = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(n, bytes.Length); output.Write(n); output.Write(t); output.Write(bytes); uint crc = uint.MaxValue; foreach (var b in t.Concat(bytes)) { crc ^= b; for (int k=0;k<8;k++) crc=(crc>>1)^((crc&1)==0?0:0xedb88320); } BinaryPrimitives.WriteUInt32BigEndian(n,~crc); output.Write(n); }
        var ihdr = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(ihdr, width); BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height); ihdr[8]=8; ihdr[9]=2; Chunk("IHDR",ihdr);
        using var packed = new MemoryStream(); using (var zip = new ZLibStream(packed, CompressionLevel.Fastest, true)) { var row = new byte[1+width*3]; Array.Fill(row,(byte)255); row[0]=0; for(int y=0;y<height;y++) zip.Write(row); } Chunk("IDAT",packed.ToArray()); Chunk("IEND",[]); return output.ToArray();
    }
    public void Dispose() => Directory.Delete(_root, true);
}
