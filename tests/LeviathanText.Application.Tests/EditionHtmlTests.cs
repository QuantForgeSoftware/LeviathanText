using System.Net;
using System.Text.RegularExpressions;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class EditionHtmlTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-edition-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(AnnotationKind.Translation)]
    [InlineData(AnnotationKind.StructuredExtraction)]
    [InlineData(AnnotationKind.LexicalAnnotation)]
    [InlineData(AnnotationKind.MorphologicalAnnotation)]
    public void Advisory_apparatus_preserves_and_escapes_the_saved_payload(AnnotationKind kind)
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        const string payload = "The <reading> & its \"interpretation\"";
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "advisory"),
            Kind = kind,
            AnchoredRevisionId = surface.Lines[0].DiplomaticRevisionId!.Value,
            Range = new ScalarRange(0, 1),
            AlternativeText = payload,
            Description = "provider metadata",
        };
        project = WithSurface(project, surface with { Annotations = [annotation] });

        var result = EditionHtml.Export(project, surface.Id, "Advisory", imageSource: "");

        Assert.Contains(WebUtility.HtmlEncode(payload), result.Html);
        Assert.DoesNotContain("<reading>", result.Html);
        Assert.Contains(kind.ToString(), result.Html);
    }

    [Fact]
    public void Local_capture_is_embedded_as_a_decodable_png_without_a_server_path()
    {
        var path = WritePng("local.png", 8, 6);
        var (project, surface) = WithImage(path);

        var result = EditionHtml.Export(project, surface.Id, "Portable");

        using var decoded = DecodeEmbeddedImage(result.Html);
        Assert.Equal(8, decoded.Width);
        Assert.Equal(6, decoded.Height);
        Assert.Equal(SKColors.CornflowerBlue, decoded.GetPixel(0, 0));
        Assert.DoesNotContain(path, result.Html);
        Assert.DoesNotContain(result.Report.Entries, e => e.Category == "image-omitted");
    }

    [Fact]
    public void Local_tiff_is_exported_as_a_browser_readable_png()
    {
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeviathanText.Imaging.Tests", "Fixtures", "sample.tif"));
        Assert.True(File.Exists(fixture));
        var (project, surface) = WithImage(fixture);

        var result = EditionHtml.Export(project, surface.Id, "TIFF");

        using var decoded = DecodeEmbeddedImage(result.Html);
        Assert.Equal(16, decoded.Width);
        Assert.Equal(12, decoded.Height);
        Assert.DoesNotContain(fixture, result.Html);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("oversize")]
    public void Unavailable_images_are_reported_without_leaking_paths_or_source_bytes(string scenario)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, scenario + ".png");
        const string sentinel = "NON_IMAGE_PRIVATE_BYTES";
        if (scenario == "corrupt") File.WriteAllText(path, sentinel);
        if (scenario == "oversize")
        {
            using var file = File.Create(path);
            file.SetLength(25L * 1024 * 1024 + 1);
        }
        var (project, surface) = WithImage(path);

        var result = EditionHtml.Export(project, surface.Id, "Unavailable");

        Assert.DoesNotContain("<img ", result.Html);
        Assert.DoesNotContain(path, result.Html);
        Assert.DoesNotContain(sentinel, result.Html);
        Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sentinel)), result.Html);
        Assert.Equal(1, Assert.Single(result.Report.Entries, e => e.Category == "image-omitted").Count);
    }

    [Fact]
    public void Large_local_capture_is_a_bounded_preview_with_explicit_loss_reporting()
    {
        var path = WritePng("large.png", 3000, 2000);
        var (project, surface) = WithImage(path);

        var result = EditionHtml.Export(project, surface.Id, "Preview");

        using var decoded = DecodeEmbeddedImage(result.Html);
        Assert.True((long)decoded.Width * decoded.Height <= 4_000_000);
        Assert.Equal(1.5, (double)decoded.Width / decoded.Height, 2);
        Assert.Equal(1, Assert.Single(result.Report.Entries, e => e.Category == "image-preview").Count);
    }

    [Fact]
    public void Explicit_empty_override_omits_a_local_image_and_relative_override_is_preserved()
    {
        var path = WritePng("overridden.png", 8, 6);
        var (project, surface) = WithImage(path);

        var omitted = EditionHtml.Export(project, surface.Id, "No image", imageSource: "");
        var relative = EditionHtml.Export(project, surface.Id, "Bundle image", imageSource: "../../images/cap-1.png");

        Assert.DoesNotContain("<img ", omitted.Html);
        Assert.DoesNotContain("data:image", omitted.Html);
        Assert.Equal(1, Assert.Single(omitted.Report.Entries, e => e.Category == "image-omitted").Count);
        Assert.Contains("src=\"../../images/cap-1.png\"", relative.Html);
        Assert.DoesNotContain("data:image", relative.Html);
        Assert.DoesNotContain(path, relative.Html);
    }

    [Fact]
    public void Remote_capture_remains_an_escaped_reference_without_fetching()
    {
        const string url = "https://example.invalid/scan.png?part=1&view=2";
        var (project, surface) = WithImage(url);

        var result = EditionHtml.Export(project, surface.Id, "Remote");

        Assert.Contains("src=\"https://example.invalid/scan.png?part=1&amp;view=2\"", result.Html);
    }

    [Fact]
    public void Whole_project_image_budget_is_shared_between_surfaces()
    {
        var path = WritePng("padded.png", 8, 6);
        // PNG readers ignore trailing bytes. A sparse file exercises the cumulative input
        // guard without constructing hundreds of megabytes of image data or HTML.
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
            file.SetLength(25L * 1024 * 1024);
        var (project, surface) = WithImage(path);
        project = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = Enumerable.Range(0, 11).Select(i => surface with
                {
                    Id = new EntityId(EntityKind.Surface, $"page-{i}"),
                    Captures = [surface.Captures[0] with
                    {
                        Id = new EntityId(EntityKind.Capture, $"image-{i}"),
                        Frame = surface.Captures[0].Frame with { ImageRef = new EntityId(EntityKind.Capture, $"image-{i}") },
                    }],
                    Regions = [], Lines = [], Revisions = [], Annotations = [],
                }).ToList(),
            }],
        };

        var result = EditionHtml.ExportProject(project, "Budget");

        Assert.Equal(10, Regex.Matches(result.Html, "data:image/png;base64,").Count);
        Assert.Equal(1, Assert.Single(result.Report.Entries, e => e.Category == "image-omitted").Count);
        Assert.DoesNotContain(path, result.Html);
    }

    private string WritePng(string name, int width, int height)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static SKBitmap DecodeEmbeddedImage(string html)
    {
        var match = Regex.Match(html, "src=\"data:image/png;base64,([^\"]+)\"");
        Assert.True(match.Success, "Expected an embedded PNG instead of a server filesystem path.");
        return SKBitmap.Decode(Convert.FromBase64String(match.Groups[1].Value));
    }

    private static (NativeProject Project, NativeSurface Surface) WithImage(string path)
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        surface = surface with { Captures = [surface.Captures[0] with { ImageLocation = path }] };
        return (WithSurface(project, surface), surface);
    }

    private static NativeProject WithSurface(NativeProject project, NativeSurface surface) => project with
    {
        Objects = [project.Objects[0] with { Surfaces = [surface] }],
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Edition_html_rejects_active_image_schemes()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var capture = surface.Captures[0] with { ImageLocation = "javascript:alert(1)" };
        var modified = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces = [surface with { Captures = [capture] }],
                },
            ],
        };

        var result = EditionHtml.Export(modified, surface.Id, "safe");

        Assert.DoesNotContain("javascript:", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No capture attached.", result.Html);
    }

    [Fact]
    public void Edition_html_renders_l1_l2_and_escaped_apparatus()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];
        var l1 = surface.Revisions.Single(r => r.Id == line.DiplomaticRevisionId);
        var l2 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-edition-l2"),
            ScopeId = line.Id,
            Layer = TextLayer.L2Editorial,
            Author = SampleProjectBuilder.Agent,
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "αβγ editorial",
        };
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-edition"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = l1.Id,
            Range = new ScalarRange(0, 1),
            Description = "<damage>",
        };
        var modified = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces =
                    [
                        surface with
                        {
                            Lines = [line with { Direction = TextDirection.Rtl }],
                            Revisions = [.. surface.Revisions, l2],
                            Annotations = [annotation],
                        },
                    ],
                },
            ],
        };

        var result = EditionHtml.Export(modified, surface.Id, "Test <edition>");

        Assert.Contains("<title>Test &lt;edition&gt;</title>", result.Html);
        Assert.Contains(l1.Text, result.Html);
        Assert.Contains(l2.Text, result.Html);
        Assert.Contains("Unclear", result.Html);
        Assert.Contains("&lt;damage&gt;", result.Html);
        Assert.Contains("dir=\"rtl\"", result.Html);
        Assert.Contains("edition-html", result.Report.Format);
        Assert.Contains(result.Report.Entries, entry => entry.Category == "presentation-only");

        var projectEdition = EditionHtml.ExportProject(modified, "Whole project");
        Assert.Contains("Whole project", projectEdition.Html);
        Assert.Contains("surface-section", projectEdition.Html);
        Assert.Contains(l1.Text, projectEdition.Html);
        Assert.Contains(projectEdition.Report.Entries, entry => entry.Category == "presentation-only");
    }
}
