using LeviathanText.Application;

namespace LeviathanText.Application.Tests;

public sealed class InscriptionInventoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-inventory-{Guid.NewGuid():N}");

    public InscriptionInventoryTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "records"));
        Directory.CreateDirectory(Path.Combine(_dir, "images"));
    }

    private void Record(string name, string body) =>
        File.WriteAllText(Path.Combine(_dir, "records", name + ".xml"), body);

    private static string EpiDoc(string text, bool withGraphic = true) => $"""
        <TEI xmlns="http://www.tei-c.org/ns/1.0">
          <teiHeader><fileDesc><titleStmt><title>{text}</title></titleStmt>
            <publicationStmt><availability><licence>CC BY 4.0</licence></availability></publicationStmt>
          </fileDesc></teiHeader>
          <facsimile>{(withGraphic ? "<graphic n=\"screen\" url=\"stone.jpg\"/>" : "")}</facsimile>
          <text><body><div type="edition" subtype="primary"><ab>
            <lb n="1"/>Ὀπετρηία Λητώι χαῖρε
            <lb n="2"/>χρηστὴ καὶ εὐσεβής
          </ab></div></body></text>
        </TEI>
        """;

    [Fact]
    public void Scan_counts_greek_lines_graphics_and_local_images()
    {
        Record("ISic000001", EpiDoc("Greek stone"));
        Record("ISic000002", EpiDoc("No image", withGraphic: false));
        Record("ISic000003", """
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body><div type="edition">
              <ab><lb/>Latin only text</ab></div></body></text></TEI>
            """);
        File.WriteAllBytes(Path.Combine(_dir, "images", "ISic000001.jpg"), [1, 2, 3]);

        var summary = InscriptionInventory.Scan(Path.Combine(_dir, "records"), Path.Combine(_dir, "images"));

        Assert.Equal(3, summary.Records);
        Assert.Equal(2, summary.WithGreekText);
        Assert.Equal(1, summary.WithActiveGraphic);
        Assert.Equal(1, summary.WithGreekAndGraphic); // only record 1 has both
        Assert.Equal(1, summary.WithLocalImage);
        Assert.Equal(5, summary.TotalEditionLines);

        var csv = InscriptionInventory.ToCsv(summary.Items);
        Assert.StartsWith("record_id,title,has_edition,greek_characters", csv);
        Assert.Contains("ISic000001", csv);
        Assert.Contains("stone.jpg,True,True,CC BY 4.0", csv); // screen graphic + local image, license kept
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
