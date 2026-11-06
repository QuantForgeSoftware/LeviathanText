using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace LeviathanText.Api.Tests;

/// <summary>Independent generated HTTP/frame controls only; no model, corpus or fitting.</summary>
public sealed class CaptureDisplayIndependentTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public async Task Every_exif_orientation_returns_raw_non_square_png_without_native_mutation(int orientation)
    {
        using var fixture = new Fixture();
        var path = fixture.Jpeg(orientation);
        var project = fixture.Seed(path);
        var before = NativeJson.Save(fixture.Load(project.Id));
        var url = fixture.Url(project);
        using var descriptorResponse = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, descriptorResponse.StatusCode);
        var body = await descriptorResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(fixture.Directory, body);
        using var descriptor = JsonDocument.Parse(body);
        var d = descriptor.RootElement;
        Assert.Equal(37, d.GetProperty("sourceWidth").GetInt32());
        Assert.Equal(23, d.GetProperty("sourceHeight").GetInt32());
        Assert.Equal(37, d.GetProperty("displayWidth").GetInt32());
        Assert.Equal(23, d.GetProperty("displayHeight").GetInt32());
        Assert.Equal(1, d.GetProperty("scaleX").GetDouble());
        Assert.Equal(1, d.GetProperty("scaleY").GetDouble());
        Assert.Equal("raw", d.GetProperty("orientation").GetString());
        Assert.Equal("verified", d.GetProperty("recordedChecksumStatus").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(path)), d.GetProperty("observedSourceChecksum").GetProperty("digest").GetString());
        using var imageRequest = new HttpRequestMessage(HttpMethod.Get, url + "/image");
        imageRequest.Headers.TryAddWithoutValidation("If-Match", descriptorResponse.Headers.ETag!.ToString());
        using var imageResponse = await fixture.Client.SendAsync(imageRequest);
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", imageResponse.Headers.CacheControl!.ToString());
        var png = await imageResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(Hash(png), d.GetProperty("displayChecksum").GetProperty("digest").GetString());
        using var expected = SKBitmap.Decode(path);
        using var actual = SKBitmap.Decode(png);
        Assert.Equal(37, actual.Width); Assert.Equal(23, actual.Height);
        Assert.Equal(Pixels(expected), Pixels(actual));
        using var rawCodec = SKCodec.Create(path);
        Assert.Equal(orientation, (int)rawCodec.EncodedOrigin);
        using var outputCodec = SKCodec.Create(new MemoryStream(png));
        Assert.Equal(SKEncodedOrigin.TopLeft, outputCodec.EncodedOrigin);
        Assert.Equal(before, NativeJson.Save(fixture.Load(project.Id)));
    }

    [Fact]
    public async Task Same_pixels_with_changed_source_bytes_invalidate_the_descriptor()
    {
        using var fixture = new Fixture();
        var path = fixture.Jpeg(1);
        var project = fixture.Seed(path, unavailable: true);
        using var descriptor = await fixture.Client.GetAsync(fixture.Url(project));
        Assert.Equal(HttpStatusCode.OK, descriptor.StatusCode);
        var original = File.ReadAllBytes(path);
        File.WriteAllBytes(path, original.Concat(new byte[] { 0x55, 0x49, 0x30, 0x31 }).ToArray());
        using var request = new HttpRequestMessage(HttpMethod.Get, fixture.Url(project) + "/image");
        request.Headers.TryAddWithoutValidation("If-Match", descriptor.Headers.ETag!.ToString());
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(1, fixture.Load(project.Id).Version);
    }

    [Theory]
    [InlineData(false, "membership")]
    [InlineData(true, "membership")]
    [InlineData(false, "disabled")]
    [InlineData(true, "key")]
    [InlineData(false, "activation")]
    [InlineData(true, "activation")]
    public async Task Buffered_output_rechecks_access_after_real_render(bool image, string revoke)
    {
        using var fixture = new Fixture();
        var project = fixture.Seed(fixture.Jpeg(1));
        string? reader = null;
        if (revoke != "activation")
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            reader = store.CreateUser("generated-reader", UserRole.Reader, UserKeys.Hash("generated-reader-key")).Id;
            store.AddProjectMember(project.Id, reader);
            fixture.Client.DefaultRequestHeaders.Add("X-Api-Key", "generated-reader-key");
        }
        var before = NativeJson.Save(fixture.Load(project.Id));
        var url = fixture.Url(project);
        string? etag = null;
        if (image)
        {
            using var descriptor = await fixture.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, descriptor.StatusCode);
            etag = descriptor.Headers.ETag!.ToString();
        }
        fixture.Factory.Hold.Arm();
        using var request = new HttpRequestMessage(HttpMethod.Get, url + (image ? "/image" : ""));
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = fixture.Client.SendAsync(request, deadline.Token);
        try
        {
            await fixture.Factory.Hold.Entered.Task.WaitAsync(deadline.Token);
            using var scope = fixture.Factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            if (revoke == "membership") store.RemoveProjectMember(project.Id, reader!);
            else if (revoke == "disabled") store.DisableUser(reader!);
            else if (revoke == "key") store.UpdateUserKey(reader!, UserKeys.Hash("replacement-key"));
            else store.CreateUser("first-admin", UserRole.Admin, UserKeys.Hash("first-account-key"));
            fixture.Factory.Hold.Release.TrySetResult();
            using var response = await pending;
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.NotEqual("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(before, NativeJson.Save(fixture.Load(project.Id)));
        }
        finally { fixture.Factory.Hold.Release.TrySetResult(); }
    }

    [Fact]
    public async Task Conflicting_aliased_frame_metadata_is_refused_without_reading_other_capture()
    {
        using var fixture = new Fixture();
        var project = fixture.Seed(fixture.Jpeg(1));
        var owner = project.Objects.Single(); var surface = owner.Surfaces.Single(); var capture = surface.Captures.Single();
        var alias = capture with
        {
            Id = new(EntityKind.Capture, "conflicting-alias"), ImageLocation = Path.Combine(fixture.Directory, "must-not-read.png"),
            WidthPx = 99, Frame = capture.Frame with { ImageChecksum = new Checksum { Algorithm = "sha256", Digest = new string('f',64) } },
        };
        using (var scope = fixture.Factory.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<LocalStore>().SaveProject(project with { Objects = [owner with { Surfaces = [surface with { Captures = [capture, alias] }] }] });
        var before = NativeJson.Save(fixture.Load(project.Id));
        using var response = await fixture.Client.GetAsync(fixture.Url(project));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before, NativeJson.Save(fixture.Load(project.Id)));
    }

    [Fact]
    public async Task Aliased_known_dimensions_are_checked_against_observed_raw_frame_when_selected_dimensions_are_unknown()
    {
        using var fixture = new Fixture();
        var project = fixture.Seed(fixture.Jpeg(1));
        var owner = project.Objects.Single(); var surface = owner.Surfaces.Single();
        var capture = surface.Captures.Single() with { WidthPx = null, HeightPx = null };
        var alias = capture with { Id = new(EntityKind.Capture, "known-dimension-alias"), WidthPx = 99, HeightPx = 23,
            ImageLocation = Path.Combine(fixture.Directory, "must-not-read-dimensions.png") };
        using (var scope = fixture.Factory.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<LocalStore>().SaveProject(project with { Objects = [owner with { Surfaces = [surface with { Captures = [capture, alias] }] }] });
        var before = NativeJson.Save(fixture.Load(project.Id));
        using var response = await fixture.Client.GetAsync(fixture.Url(project));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before, NativeJson.Save(fixture.Load(project.Id)));
    }

    [Fact]
    public async Task Aliased_dimension_claim_added_after_render_is_rechecked_against_observed_raw_frame()
    {
        using var fixture = new Fixture();
        var project = fixture.Seed(fixture.Jpeg(1));
        var owner = project.Objects.Single(); var surface = owner.Surfaces.Single();
        var capture = surface.Captures.Single() with { WidthPx = null, HeightPx = null };
        using (var scope = fixture.Factory.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<LocalStore>().SaveProject(project with { Objects = [owner with { Surfaces = [surface with { Captures = [capture] }] }] });
        fixture.Factory.Hold.Arm();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = fixture.Client.GetAsync(fixture.Url(project), deadline.Token);
        string before;
        try
        {
            await fixture.Factory.Hold.Entered.Task.WaitAsync(deadline.Token);
            var current = fixture.Load(project.Id); owner = current.Objects.Single(); surface = owner.Surfaces.Single();
            var alias = capture with { Id = new(EntityKind.Capture, "after-render-dimension-alias"), WidthPx = 99, HeightPx = 23,
                ImageLocation = Path.Combine(fixture.Directory, "must-not-read-after-render.png") };
            using (var scope = fixture.Factory.Services.CreateScope())
                scope.ServiceProvider.GetRequiredService<LocalStore>().SaveProject(current with { Objects = [owner with { Surfaces = [surface with { Captures = [capture, alias] }] }] });
            before = NativeJson.Save(fixture.Load(project.Id));
            fixture.Factory.Hold.Release.TrySetResult();
            using var response = await pending;
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(before, NativeJson.Save(fixture.Load(project.Id)));
        }
        finally { fixture.Factory.Hold.Release.TrySetResult(); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static byte[] Pixels(SKBitmap bitmap)
    {
        var bytes = new byte[bitmap.Width * bitmap.Height * 4]; var i = 0;
        for (int y=0; y<bitmap.Height; y++) for (int x=0; x<bitmap.Width; x++)
        { var p=bitmap.GetPixel(x,y); bytes[i++]=p.Red; bytes[i++]=p.Green; bytes[i++]=p.Blue; bytes[i++]=p.Alpha; }
        return bytes;
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory("lt-display-independent-").FullName;
        public Factory Factory { get; }
        public HttpClient Client { get; }
        public Fixture() { Factory = new Factory(Path.Combine(Directory,"store.db")); Client = Factory.CreateClient(); }
        public string Jpeg(int orientation)
        {
            using var bitmap = new SKBitmap(37,23,SKColorType.Rgba8888,SKAlphaType.Premul);
            for (int y=0;y<23;y++) for(int x=0;x<37;x++)
                bitmap.SetPixel(x,y,new SKColor((byte)(x*6),(byte)(y*10),(byte)((x*13+y*17)%256),255));
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg,100);
            var jpeg = data.ToArray();
            // Owned tiny fixture metadata: Exif prefix, little-endian TIFF header,
            // one SHORT Orientation entry, and a zero next-IFD link. No encoder dependency.
            byte[] exif = [69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,
                18,1,3,0,1,0,0,0,(byte)orientation,0,0,0,0,0,0,0];
            var path = Path.Combine(Directory,$"orientation-{orientation}.jpg");
            using var output = File.Create(path);
            output.Write(jpeg.AsSpan(0,2));
            output.Write(new byte[] {255,225,0,34}); // APP1 length includes its two length bytes.
            output.Write(exif);
            output.Write(jpeg.AsSpan(2));
            return path;
        }
        public NativeProject Seed(string path, bool unavailable=false)
        {
            var cap = new EntityId(EntityKind.Capture,"raw-display");
            var project = new NativeProject
            {
                Id = new(EntityKind.Collection,"display-project"), Name="generated display", Objects=[new NativeObject
                {
                    Id=new(EntityKind.PhysicalObject,"display-object"), Surfaces=[new NativeSurface
                    {
                        Id=new(EntityKind.Surface,"display-surface"), Kind="page", Captures=[new NativeCapture
                        {
                            Id=cap,ImageLocation=path,WidthPx=37,HeightPx=23,
                            Frame=new CoordinateFrame {ImageRef=cap,ImageChecksum=new Checksum {Algorithm=unavailable?"unavailable":"sha256",Digest=unavailable?"generated":Hash(File.ReadAllBytes(path))}},
                        }],Regions=[],Lines=[],Revisions=[],
                    }],
                }],
            };
            using var scope=Factory.Services.CreateScope();
            return project with { Version=scope.ServiceProvider.GetRequiredService<LocalStore>().SaveProject(project) };
        }
        public NativeProject Load(EntityId id) { using var scope=Factory.Services.CreateScope(); return scope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(id)!; }
        public string Url(NativeProject project) => $"/api/v1/projects/{project.Id}/surfaces/{project.Objects[0].Surfaces[0].Id}/captures/{project.Objects[0].Surfaces[0].Captures[0].Id}/display";
        public void Dispose() { Client.Dispose(); Factory.Dispose(); System.IO.Directory.Delete(Directory,true); }
    }
    public sealed class HoldingRenderer : ICaptureDisplayService, IDisposable
    {
        private readonly CaptureDisplayService inner = new(); private int armed;
        public TaskCompletionSource Entered {get;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Interlocked.Exchange(ref armed,1);
        public async Task<CaptureDisplayResult> RenderAsync(NativeProject project, EntityId surface, EntityId capture, CancellationToken cancellationToken=default)
        {
            var result=await inner.RenderAsync(project,surface,capture,cancellationToken);
            try
            {
                if(Interlocked.Exchange(ref armed,0)==1) {Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken);}
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        public void Dispose() {Release.TrySetResult();inner.Dispose();}
    }
    public sealed class Factory(string database) : WebApplicationFactory<Program>
    {
        public HoldingRenderer Hold {get;} = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",database);
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>
            {["LeviathanText:Auth:ApiKey"]="",["LeviathanText:Auth:ProjectMembership"]="true"}));
            builder.ConfigureTestServices(services=> {services.RemoveAll<ICaptureDisplayService>();services.AddSingleton<ICaptureDisplayService>(Hold);});
        }
        protected override void Dispose(bool disposing) {base.Dispose(disposing);if(disposing)Hold.Dispose();}
    }
}
