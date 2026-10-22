using LeviathanText.Training;
using SkiaSharp;
using Xunit;

namespace LeviathanText.ML.Tests;

public class BaselineDatasetTests
{
    [Fact]
    public void Loader_builds_normalized_image_and_multiclass_target()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-baseline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using (var image = new SKBitmap(64, 40))
        {
            image.Erase(SKColors.White);
            for (int y = 8; y < 32; y++)
                for (int x = 6; x < 58; x++)
                    image.SetPixel(x, y, SKColors.Black);
            using var encoded = SKImage.FromBitmap(image);
            using var data = encoded.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(directory, "p.png"));
            data.SaveTo(stream);
        }
        File.WriteAllText(Path.Combine(directory, "p.xml"), """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="p.png" imageWidth="64" imageHeight="40">
                <TextRegion id="r1"><Coords points="4,4 60,4 60,36 4,36"/>
                  <TextLine id="l1"><Coords points="8,16 56,16 56,28 8,28"/></TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);

        var classes = SegmentationClassMap.Create(
            [("_start", 0), ("_end", 1)],
            [("$par", 2)],
            [("$par", 3)]);
        var result = BaselineDataset.Load(directory, classes, new BaselineDataset.Options
        {
            TargetWidth = 32,
            TargetHeight = 24,
        });

        var sample = Assert.Single(result.Samples);
        Assert.Empty(result.Issues);
        Assert.Equal("p", sample.Page);
        Assert.Equal(1, sample.Channels);
        Assert.Equal(32, sample.Width);
        Assert.Equal(24, sample.Height);
        Assert.Equal(32 * 24, sample.Pixels.Length);
        Assert.Equal(4, sample.Target.GetLength(0));
        Assert.Equal(24, sample.Target.GetLength(1));
        Assert.Equal(32, sample.Target.GetLength(2));
        Assert.Contains(sample.Target.Cast<float>(), value => value == 1);
    }

    [Fact]
    public void Missing_image_is_reported_not_silently_skipped()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-baseline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "missing.xml"), """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="missing.png" imageWidth="10" imageHeight="10"/>
            </PcGts>
            """);
        var classes = SegmentationClassMap.Create([("_start", 0)], [], []);

        var result = BaselineDataset.Load(directory, classes);

        Assert.Empty(result.Samples);
        Assert.Contains(result.Issues, issue => issue.Page == "missing" && issue.Message.Contains("No image"));
    }
}
