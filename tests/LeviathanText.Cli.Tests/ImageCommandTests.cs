using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

/// <summary>
/// Process-level contracts for the image-only CLI commands. These exercise the same
/// entry point as the installed wrapper and assert file output, not just helper methods.
/// </summary>
public sealed class ImageCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-cli-tests", Guid.NewGuid().ToString("N"));

    public ImageCommandTests() => Directory.CreateDirectory(_root);

    private string PathOf(string name) => Path.Combine(_root, name);

    private int Run(params string[] args) =>
        LeviathanText.Cli.Program.Main(["--store", PathOf("store.db"), .. args]);

    [Fact]
    public void Binarize_color_writes_black_and_white_png()
    {
        var input = PathOf("color.png");
        var output = PathOf("bw.png");
        using (var bitmap = new SKBitmap(48, 24))
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(160, 30, 30));
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(new SKRect(4, 8, 44, 16), paint);
            SavePng(bitmap, input);
        }

        Assert.Equal(0, Run("image", "binarize", input, output));

        using var result = SKBitmap.Decode(output);
        Assert.NotNull(result);
        foreach (var pixel in result.Pixels)
            Assert.True(pixel.Red is 0 or 255 && pixel.Green is 0 or 255 && pixel.Blue is 0 or 255,
                $"non-B/W pixel {pixel}");
    }

    [Fact]
    public void Binarize_already_bw_image_keeps_dimensions_and_values()
    {
        var input = PathOf("bw-input.png");
        var output = PathOf("bw-output.png");
        using (var bitmap = new SKBitmap(20, 10))
        {
            bitmap.Erase(SKColors.White);
            for (int y = 3; y < 7; y++)
                for (int x = 2; x < 18; x++)
                    bitmap.SetPixel(x, y, SKColors.Black);
            SavePng(bitmap, input);
        }

        Assert.Equal(0, Run("image", "binarize", input, output));

        using var source = SKBitmap.Decode(input);
        using var result = SKBitmap.Decode(output);
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                Assert.Equal(source.GetPixel(x, y), result.GetPixel(x, y));
    }

    [Fact]
    public void Segment_json_reports_baselines_and_requested_direction()
    {
        var input = PathOf("page.png");
        var output = PathOf("lines.json");
        WritePageWithBars(input);

        Assert.Equal(0, Run("image", "segment", input, output, "--format", "json", "--direction", "horizontal-rl"));

        using var json = JsonDocument.Parse(File.ReadAllText(output));
        var root = json.RootElement;
        Assert.Equal("baselines", root.GetProperty("type").GetString());
        Assert.Equal("horizontal-rl", root.GetProperty("text_direction").GetString());
        Assert.True(root.GetProperty("lines").GetArrayLength() > 0);
        Assert.Equal(input, Path.Combine(_root, root.GetProperty("image").GetString()!));
    }

    [Fact]
    public void Segment_page_and_alto_write_parseable_output()
    {
        var input = PathOf("page-formats.png");
        var page = PathOf("lines.page.xml");
        var alto = PathOf("lines.alto.xml");
        WritePageWithBars(input);

        Assert.Equal(0, Run("image", "segment", input, page, "--format", "page"));
        Assert.Equal(0, Run("image", "segment", input, alto, "--format", "alto"));

        var pageText = File.ReadAllText(page);
        Assert.Contains("PcGts", pageText);
        Assert.Contains("TextLine", pageText);
        var altoText = File.ReadAllText(alto);
        Assert.Contains("alto", altoText.ToLowerInvariant());
        Assert.Contains("TextLine", altoText);
    }

    [Fact]
    public void Image_commands_refuse_to_overwrite_the_source()
    {
        var input = PathOf("overwrite.png");
        WritePageWithBars(input);

        Assert.Equal(1, Run("image", "binarize", input, input));
        Assert.Equal(1, Run("image", "segment", input, input));
    }

    [Fact]
    public void Missing_image_is_reported_as_a_nonzero_cli_failure()
    {
        Assert.NotEqual(0, Run("image", "binarize", PathOf("missing.png"), PathOf("out.png")));
    }

    [Fact]
    public void Ocr_line_mode_writes_text_from_a_verified_package()
    {
        var input = PathOf("ocr-line.png");
        var output = PathOf("ocr-line.txt");
        var package = CreatePackage();
        WritePageWithBars(input);

        Assert.Equal(0, Run("image", "ocr", input, output, "--package", package, "--mode", "line", "--format", "text"));
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void Ocr_segment_mode_emits_line_records_and_page_formats()
    {
        var input = PathOf("ocr-segment.png");
        var jsonPath = PathOf("ocr.json");
        var pagePath = PathOf("ocr.page.xml");
        var altoPath = PathOf("ocr.alto.xml");
        var package = CreatePackage();
        WritePageWithBars(input);

        Assert.Equal(0, Run("image", "ocr", input, jsonPath, "--package", package, "--mode", "segment", "--format", "json"));
        using var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
        Assert.Equal("ocr", json.RootElement.GetProperty("type").GetString());
        Assert.True(json.RootElement.GetProperty("lines").GetArrayLength() > 0);

        Assert.Equal(0, Run("image", "ocr", input, pagePath, "--package", package, "--mode", "segment", "--format", "page"));
        Assert.Contains("PcGts", File.ReadAllText(pagePath));
        Assert.Equal(0, Run("image", "ocr", input, altoPath, "--package", package, "--mode", "segment", "--format", "alto"));
        Assert.Contains("alto", File.ReadAllText(altoPath).ToLowerInvariant());
    }

    [Fact]
    public void Ocr_without_a_package_fails_clearly()
    {
        var input = PathOf("ocr-missing-package.png");
        WritePageWithBars(input);

        Assert.NotEqual(0, Run("image", "ocr", input, PathOf("ocr.txt"), "--mode", "line"));
    }

    private string CreatePackage()
    {
        var directory = PathOf("package");
        Directory.CreateDirectory(directory);
        var labels = new LabelSet(["α", "β"]);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        model.save(CheckpointStore.ModelPath(directory, "tiny", ".best"));
        CheckpointStore.WriteMetadata(directory, "tiny", new CheckpointMetadata
        {
            Epoch = 1,
            GlobalStep = 1,
            BestValidationCer = 1.0,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels,
            ConfigHash = "test",
            HiddenSize = 8,
            InputHeightPixels = CrnnModel.InputHeight,
            WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
            TorchVersion = "test",
            CreatedAt = DateTimeOffset.UtcNow,
        }, ".best");
        return directory;
    }

    private static void WritePageWithBars(string path)
    {
        using var bitmap = new SKBitmap(180, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        for (int i = 0; i < 3; i++)
            canvas.DrawRect(new SKRect(15, 15 + i * 25, 165, 23 + i * 25), paint);
        SavePng(bitmap, path);
    }

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
