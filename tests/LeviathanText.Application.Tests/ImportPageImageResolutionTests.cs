using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public class ImportPageImageResolutionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-page-images-{Guid.NewGuid():N}");

    [Fact]
    public void Import_resolves_an_image_from_a_sibling_images_directory()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "xml"));
        Directory.CreateDirectory(Path.Combine(_dir, "images"));
        using (var surface = SKSurface.Create(new SKImageInfo(100, 60)))
        {
            surface.Canvas.Clear(SKColors.White);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(_dir, "images", "page.png"));
            data.SaveTo(stream);
        }
        var xmlPath = Path.Combine(_dir, "xml", "page.xml");
        File.WriteAllText(xmlPath, """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="page.png" imageWidth="100" imageHeight="60">
                <TextRegion id="region 1"><Coords points="0,0 100,0 100,60 0,60"/>
                  <TextLine id="line (1)"><Coords points="10,10 90,10 90,40 10,40"/>
                    <TextEquiv><Unicode>αβγ</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);

        using var store = new LocalStore(Path.Combine(_dir, "store.db"));
        var projects = new ProjectService(store);
        var project = projects.Create("image-resolution");
        var imported = new InterchangeService(projects).ImportPage(project, xmlPath);

        var capture = Assert.Single(imported.Imported.Surfaces[0].Captures);
        Assert.Equal(Path.Combine(_dir, "images", "page.png"), capture.ImageLocation);
        Assert.Equal(100, capture.WidthPx);
        Assert.Equal(60, capture.HeightPx);
        var region = Assert.Single(imported.Imported.Surfaces[0].Regions);
        var line = Assert.Single(imported.Imported.Surfaces[0].Lines);
        Assert.Equal("region_1", region.Id.Suffix.Split('-')[0]);
        Assert.Equal("line__1_", line.Id.Suffix.Split('-')[0]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
