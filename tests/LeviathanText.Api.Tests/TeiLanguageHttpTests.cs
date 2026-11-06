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

public sealed class TeiLanguageHttpTests
{
    [Fact]
    public async Task Invalid_declared_language_is_refused_before_image_decode_without_mutating_the_project()
    {
        var directory = Directory.CreateTempSubdirectory("lt-language-http-refusal-");
        try
        {
            using var factory = new Factory(Path.Combine(directory.FullName, "store.db")); using var client = factory.CreateClient();
            using var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "language refusal" });
            var identity = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = EntityId.Parse(identity.GetProperty("id").GetString()!, EntityKind.Collection);
            var xml = Path.Combine(directory.FullName, "source.xml");
            File.WriteAllText(xml, "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage><language ident='PRIVATE_LANGUAGE_" + new string('x', 257) + "'/></langUsage></profileDesc></teiHeader><text><body><div type='edition'><ab><lb/>α</ab></div></body></text></TEI>");
            var image = Path.Combine(directory.FullName, "not-an-image.png"); File.WriteAllText(image, "invalid image bytes");
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>(); var before = NativeJson.Save(store.LoadProject(id)!);
            using var response = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-tei", new { documentPath = xml, imagePath = image });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(ErrorCode.StageContractViolation, ContractJson.FromJson<LeviathanError>(body).Code);
            Assert.DoesNotContain("PRIVATE_LANGUAGE", body);
            Assert.Equal(before, NativeJson.Save(store.LoadProject(id)!));
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task Actual_import_export_keeps_header_declarations_separate_from_inherited_and_reset_line_languages()
    {
        var directory = Directory.CreateTempSubdirectory("lt-language-http-");
        try
        {
            using var factory = new Factory(Path.Combine(directory.FullName, "store.db"));
            using var client = factory.CreateClient();
            using var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "generated language metadata" });
            var identity = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = EntityId.Parse(identity.GetProperty("id").GetString()!, EntityKind.Collection);
            var source = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage><language ident='grc'/><language ident='la'/></langUsage></profileDesc></teiHeader><text xml:lang='grc'><body><div type='edition'><ab><lb/>α</ab><ab xml:lang=''><lb/>β</ab></div></body></text></TEI>";
            var xml = Path.Combine(directory.FullName, "source.xml"); File.WriteAllText(xml, source);
            var image = Path.Combine(directory.FullName, "page.png");
            using (var bitmap = new SKBitmap(10, 10))
            using (var png = bitmap.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(image, png.ToArray());
            using var imported = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-tei", new { documentPath = xml, imagePath = image });
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            var project = store.LoadProject(id)!; var owner = Assert.Single(project.Objects); var surface = Assert.Single(owner.Surfaces);
            Assert.Equal(new[] { "grc", "la" }, JsonSerializer.Deserialize<string[]>(owner.Metadata["tei:declaredLanguages"]));
            Assert.Equal(new[] { "grc" }, surface.Lines[0].Languages); Assert.Empty(surface.Lines[1].Languages);
            var before = NativeJson.Save(project);
            using var exported = await client.GetAsync($"/api/v1/projects/{id}/surfaces/{surface.Id}/tei?report=true");
            Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
            var payload = await exported.Content.ReadFromJsonAsync<JsonElement>();
            var parsed = TeiXml.Import(payload.GetProperty("xml").GetString()!).Document!;
            Assert.Equal(new[] { "grc", "la" }, parsed.DeclaredLanguages);
            Assert.Equal(new[] { "grc" }, parsed.Lines[0].Languages); Assert.Empty(parsed.Lines[1].Languages);
            Assert.Equal(before, NativeJson.Save(store.LoadProject(id)!));

            // A malformed stored field must be a controlled client contract refusal,
            // not an uncaught server error or silent metadata omission.
            var metadata = new Dictionary<string, string>(owner.Metadata) { ["tei:declaredLanguages"] = "not-json" };
            store.SaveProject(project with { Objects = [owner with { Metadata = metadata }] });
            var malformedBefore = NativeJson.Save(store.LoadProject(id)!);
            using var refused = await client.GetAsync($"/api/v1/projects/{id}/surfaces/{surface.Id}/tei?report=true");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Equal(ErrorCode.StageContractViolation, ContractJson.FromJson<LeviathanError>(await refused.Content.ReadAsStringAsync()).Code);
            Assert.Equal(malformedBefore, NativeJson.Save(store.LoadProject(id)!));
        }
        finally { directory.Delete(true); }
    }

    private sealed class Factory(string database) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", database);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["LeviathanText:Auth:ApiKey"] = "", ["LeviathanText:Auth:ProjectMembership"] = "false" }));
        }
    }
}
