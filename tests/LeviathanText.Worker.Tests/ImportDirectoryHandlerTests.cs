using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Worker.Tests;

/// <summary>
/// Durable bulk import: a directory of PAGE files becomes surfaces in the target project;
/// one malformed file is reported without losing the rest of the batch.
/// </summary>
public sealed class ImportDirectoryHandlerTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-import-{Guid.NewGuid():N}.db");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-import-src-{Guid.NewGuid():N}");

    public ImportDirectoryHandlerTests()
    {
        Directory.CreateDirectory(_dir);
        WritePage("page-a", "ln-a", "αβγ");
        WritePage("page-b", "ln-b", "δεζ");
        File.WriteAllText(Path.Combine(_dir, "page-broken.xml"), "<not-a-page/>");
    }

    private void WritePage(string stem, string lineId, string text)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawRect(new SKRect(10, 20, 190, 40), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var stream = File.OpenWrite(Path.Combine(_dir, stem + ".png")))
            data.SaveTo(stream);

        File.WriteAllText(Path.Combine(_dir, stem + ".xml"), $"""
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="{stem}.png" imageWidth="200" imageHeight="100">
                <TextRegion id="r-{stem}">
                  <Coords points="0,0 200,0 200,100 0,100"/>
                  <TextLine id="{lineId}">
                    <Coords points="10,20 190,20 190,40 10,40"/>
                    <TextEquiv><Unicode>{text}</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);
    }

    private JobRecord Job(EntityId projectId) => new()
    {
        Id = new EntityId(EntityKind.Job, "j-import"),
        Type = ImportDirectoryHandler.JobType,
        Inputs = [new StageArtifact(projectId, new Checksum { Algorithm = "page-xml-directory", Digest = "dir:" + _dir })],
        Owner = new AgentRef { Id = "test", IsModel = false },
        Status = JobStatus.Pending,
    };

    [Fact]
    public void Imports_good_files_and_reports_the_bad_one()
    {
        using var store = new LocalStore(_db);
        var project = new ProjectService(store).Create("bulk-import");
        var jobs = new MemoryJobStore();
        jobs.Save(Job(project.Id));
        var host = new WorkerHost(jobs, [new ImportDirectoryHandler(store)]);

        var finished = host.RunNext();
        Assert.NotNull(finished);
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        Assert.Equal("imported=2;failed=1", Assert.Single(finished.Outputs).Artifact.Digest);

        var surfaces = store.LoadProject(project.Id)!.Objects.SelectMany(o => o.Surfaces).ToList();
        Assert.Equal(2, surfaces.Count);
        Assert.All(surfaces, s => Assert.Single(s.Lines));
        Assert.Contains(surfaces, s => s.Lines[0].DiplomaticRevisionId is not null
            && s.Revisions.Any(r => r.Id == s.Lines[0].DiplomaticRevisionId && r.Text == "αβγ"));
    }

    [Fact]
    public void Empty_directory_fails_without_touching_the_project()
    {
        using var store = new LocalStore(_db);
        var project = new ProjectService(store).Create("empty-import");
        var empty = Path.Combine(_dir, "empty");
        Directory.CreateDirectory(empty);

        var job = Job(project.Id) with
        {
            Id = new EntityId(EntityKind.Job, "j-import-empty"),
            Inputs = [new StageArtifact(project.Id, new Checksum
            {
                Algorithm = "page-xml-directory",
                Digest = "dir:" + empty,
            })],
        };
        var jobs = new MemoryJobStore();
        jobs.Save(job);
        var host = new WorkerHost(jobs, [new ImportDirectoryHandler(store)]) { MaxRetries = 0 };

        var finished = host.RunNext();
        Assert.Equal(JobStatus.Failed, finished!.Status);
        Assert.Empty(store.LoadProject(project.Id)!.Objects);
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
