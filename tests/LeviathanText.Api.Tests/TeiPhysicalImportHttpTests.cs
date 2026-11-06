using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace LeviathanText.Api.Tests;

public sealed class TeiPhysicalImportHttpTests
{
    private static string Document(string content) => "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'>" + content + "</div></body></text></TEI>";

    [Theory]
    [InlineData("alternative")]
    [InlineData("context")]
    [InlineData("oversized")]
    [InlineData("xml")]
    public async Task Refused_TEI_returns_422_before_image_decode_and_preserves_exact_project(string kind)
    {
        var directory = Directory.CreateTempSubdirectory("lt-tei-physical-http-");
        try
        {
            using var factory = new Factory(Path.Combine(directory.FullName, "store.db"));
            using var client = factory.CreateClient();
            var id = await Create(client);
            var source = kind switch
            {
                "alternative" => Document("<ab><lb/><app><rdg type='orig'>A<lb/>B</rdg><rdg type='corr'>C</rdg></app></ab>"),
                "context" => Document("<ab data-tei-source-context='!'><lb/>A</ab>"),
                "oversized" => Document("<ab data-tei-source-context='" + new string('x', 65537) + "'><lb/>A</ab>"),
                _ => "<TEI><private-source-token>",
            };
            var xml = Path.Combine(directory.FullName, "source.xml"); File.WriteAllText(xml, source);
            var image = Path.Combine(directory.FullName, "existing-invalid.png"); File.WriteAllText(image, "not image bytes");
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            var before = NativeJson.Save(store.LoadProject(id)!);
            using var response = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-tei", new { documentPath = xml, imagePath = image });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("private-source-token", body);
            Assert.DoesNotContain(directory.FullName, body);
            Assert.Equal(ErrorCode.StageContractViolation, ContractJson.FromJson<LeviathanError>(body).Code);
            Assert.Equal(before, NativeJson.Save(store.LoadProject(id)!));
            Assert.Equal(1, store.LoadProject(id)!.Version);
            Assert.Empty(store.LoadProject(id)!.Objects);
            Assert.DoesNotContain(Directory.EnumerateFiles(directory.FullName, "*", SearchOption.AllDirectories), path => path.Contains("derivative", StringComparison.OrdinalIgnoreCase));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task Native_HTTP_import_export_preserves_physical_context_and_continuity()
    {
        var directory = Directory.CreateTempSubdirectory("lt-tei-physical-http-success-");
        try
        {
            using var factory = new Factory(Path.Combine(directory.FullName, "store.db"));
            using var client = factory.CreateClient(); var id = await Create(client);
            var source = Document("<lb xml:id='one' n='1'/><ab n='V1' xml:lang='grc'>A</ab><ab n='V2' xml:lang='la'>B<lb xml:id='two' n='2' break='no'/>C</ab>");
            var xml = Path.Combine(directory.FullName, "source.xml"); File.WriteAllText(xml, source);
            var image = Path.Combine(directory.FullName, "page.png");
            using (var bitmap = new SKBitmap(20, 20))
            using (var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(image, encoded.ToArray());
            using var response = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-tei", new { documentPath = xml, imagePath = image });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var imported = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, imported.GetProperty("lineCount").GetInt32());
            var surfaceId = imported.GetProperty("surfaceId").GetString()!;
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            var project = store.LoadProject(id)!; var surface = Assert.Single(Assert.Single(project.Objects).Surfaces);
            Assert.Equal(2, project.Version);
            Assert.Equal(new[] { "grc", "la" }, surface.Lines[0].Languages);
            Assert.Equal(source, Assert.Single(Assert.Single(project.Objects).Attachments).Content);
            var context = Assert.Single(surface.Lines[0].Tags["tei-source-context"]);
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(context));
            Assert.Contains("V1", decoded); Assert.Contains("V2", decoded);
            var before = NativeJson.Save(project);
            using var exported = await client.GetAsync($"/api/v1/projects/{id}/surfaces/{surfaceId}/tei?report=true");
            Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
            var result = await exported.Content.ReadFromJsonAsync<JsonElement>();
            var parsed = TeiXml.Import(result.GetProperty("xml").GetString()!);
            Assert.Equal(new[] { "AB", "C" }, parsed.Revisions.Select(r => r.Text));
            Assert.Contains("tei-source-context=" + context, parsed.Document!.Lines[0].Tags);
            Assert.Contains("tei-lb-break=no", parsed.Document.Lines[1].Tags);
            Assert.Contains("line-languages", result.GetProperty("lossReport").GetRawText());
            Assert.Equal(before, NativeJson.Save(store.LoadProject(id)!));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static async Task<EntityId> Create(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/projects", new { name = "generated physical TEI" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return EntityId.Parse(json.GetProperty("id").GetString()!, EntityKind.Collection);
    }
    private sealed class Factory(string database) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", database);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            { ["LeviathanText:Auth:ApiKey"] = "", ["LeviathanText:Auth:ProjectMembership"] = "false" }));
        }
    }
}
