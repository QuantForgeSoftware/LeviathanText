using System.Text;
using System.Xml.Linq;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Inventory of an ISicily-style EpiDoc repository for the inscription pilot (todo INS-01):
/// which records have an edition, usable Greek text, an active (non-commented) graphic, and a
/// locally available image. The scan validates the manifest's counts; it does not fetch images
/// or grant rights.
/// </summary>
public static class InscriptionInventory
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";

    public sealed record Record(
        string Id,
        string Title,
        bool HasEdition,
        int GreekCharacters,
        int EditionLines,
        IReadOnlyList<string> GraphicUrls,
        bool HasScreenGraphic,
        bool LocalImageAvailable,
        string License);

    public sealed record Summary(
        int Records,
        int WithEdition,
        int WithGreekText,
        int WithActiveGraphic,
        int WithScreenGraphic,
        int WithGreekAndGraphic,
        int WithLocalImage,
        int TotalEditionLines,
        IReadOnlyList<Record> Items);

    public static Summary Scan(string recordDirectory, string? imageDirectory = null, int limit = 0)
    {
        if (!Directory.Exists(recordDirectory))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = recordDirectory,
                Message = "Record directory not found.",
            });

        var files = Directory.GetFiles(recordDirectory, "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (limit > 0) files = [.. files.Take(limit)];

        var items = new List<Record>();
        foreach (var file in files)
            items.Add(ScanRecord(file, imageDirectory));

        return new Summary(
            items.Count,
            items.Count(i => i.HasEdition),
            items.Count(i => i.GreekCharacters > 0),
            items.Count(i => i.GraphicUrls.Count > 0),
            items.Count(i => i.HasScreenGraphic),
            items.Count(i => i.GreekCharacters > 0 && i.GraphicUrls.Count > 0),
            items.Count(i => i.LocalImageAvailable),
            items.Sum(i => i.EditionLines),
            items);
    }

    public static string ToCsv(IReadOnlyList<Record> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("record_id,title,has_edition,greek_characters,edition_lines,graphic_urls,has_screen_graphic,local_image,license");
        foreach (var item in items)
        {
            builder.Append(Escape(item.Id)).Append(',')
                .Append(Escape(item.Title)).Append(',')
                .Append(item.HasEdition).Append(',')
                .Append(item.GreekCharacters).Append(',')
                .Append(item.EditionLines).Append(',')
                .Append(Escape(string.Join('|', item.GraphicUrls))).Append(',')
                .Append(item.HasScreenGraphic).Append(',')
                .Append(item.LocalImageAvailable).Append(',')
                .Append(Escape(item.License))
                .AppendLine();
        }
        return builder.ToString();
    }

    private static Record ScanRecord(string path, string? imageDirectory)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        try
        {
            var doc = XDocument.Load(path, LoadOptions.None);
            var edition = doc.Descendants(Tei + "div")
                .FirstOrDefault(d => (string?)d.Attribute("type") == "edition");
            string editionText = edition?.Value ?? "";
            int lines = 0;
            if (edition is not null)
            {
                try
                {
                    // Count Greek letters from the parsed edition lines, not the div text:
                    // the div also contains notes/apparatus and punctuation.
                    var parsed = EpiDocImport.Parse(File.ReadAllText(path));
                    lines = parsed.Lines.Count;
                    editionText = string.Join(" ", parsed.Lines.Select(l => l.Text));
                }
                catch (Exception)
                {
                    lines = edition.Descendants(Tei + "lb").Count();
                }
            }
            int greek = editionText.Count(c =>
                char.IsLetter(c) && (c is >= '\u0370' and <= '\u03FF' or >= '\u1F00' and <= '\u1FFF'));

            var graphics = doc.Descendants(Tei + "graphic")
                .Select(g => (Url: (string?)g.Attribute("url") ?? "", N: (string?)g.Attribute("n")))
                .Where(g => !string.IsNullOrWhiteSpace(g.Url))
                .ToList();
            var urls = graphics.Select(g => g.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            bool screen = graphics.Any(g => string.Equals(g.N, "screen", StringComparison.OrdinalIgnoreCase));

            bool local = false;
            if (imageDirectory is not null)
            {
                var candidates = new List<string> { Path.Combine(imageDirectory, id + ".jpg"), Path.Combine(imageDirectory, id + ".png"), Path.Combine(imageDirectory, id + "_w800.jpg") };
                candidates.AddRange(urls.Select(u => Path.Combine(imageDirectory, Path.GetFileName(u))));
                local = candidates.Any(File.Exists);
            }

            var title = doc.Descendants(Tei + "titleStmt").Descendants(Tei + "title")
                .Select(e => e.Value.Trim()).FirstOrDefault(v => v.Length > 0) ?? "";
            var license = doc.Descendants(Tei + "licence").Select(e => e.Value.Trim())
                .FirstOrDefault(v => v.Length > 0) ?? "";

            return new Record(id, title, edition is not null, greek, lines, urls, screen, local, license);
        }
        catch (Exception)
        {
            return new Record(id, "", false, 0, 0, [], false, false, "");
        }
    }

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}
