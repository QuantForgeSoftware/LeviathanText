using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests.Fixtures;

/// <summary>
/// Loader for the invented illustrative fixtures (greek-lines.json). Anchor-based
/// annotations are resolved to scalar ranges here, so fixture text can be edited
/// without hand-maintaining offsets.
/// </summary>
public sealed record GreekFixture
{
    public required string ContractVersion { get; init; }
    public required string Note { get; init; }
    public required IReadOnlyList<GreekFixtureLine> Lines { get; init; }

    public static GreekFixture Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "greek-lines.json");
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<FixtureDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Fixture deserialized to null.");
        return ToDomain(dto);
    }

    private sealed record FixtureDto(string ContractVersion, string Note, IReadOnlyList<LineDto> Lines);

    private sealed record LineDto(string Id, string Material, string ExampleRef, string Text, IReadOnlyList<AnnotationDto> Annotations);

    private sealed record AnnotationDto(
        string Kind, string? Anchor, string? AnchorType, int? Index, int Occurrence,
        string? Description, string? GapExtent, string? AlternativeText, string? HandLabel);

    private static GreekFixture ToDomain(FixtureDto dto) => new()
    {
        ContractVersion = dto.ContractVersion,
        Note = dto.Note,
        Lines = [.. dto.Lines.Select(l => new GreekFixtureLine
        {
            Id = l.Id,
            Material = l.Material,
            ExampleRef = l.ExampleRef,
            Text = l.Text,
            Annotations = [.. l.Annotations.Select(a => ToAnnotation(l.Text, a))],
        })],
    };

    private static Annotation ToAnnotation(string text, AnnotationDto dto)
    {
        var range = dto.AnchorType switch
        {
            "scalarIndex" => new ScalarRange(dto.Index!.Value, dto.Index.Value),
            _ => ResolveAnchor(text, dto.Anchor!, dto.Occurrence),
        };

        return new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, $"a-{dto.Kind.ToLowerInvariant()}"),
            Kind = Enum.Parse<AnnotationKind>(dto.Kind, ignoreCase: false),
            AnchoredRevisionId = default, // bound to the fixture line's revision by the consumer
            Range = range,
            GapExtent = dto.GapExtent is null ? null : Enum.Parse<GapExtent>(dto.GapExtent),
            AlternativeText = dto.AlternativeText,
            HandLabel = dto.HandLabel,
            Description = dto.Description,
        };
    }

    private static ScalarRange ResolveAnchor(string text, string anchor, int occurrence)
    {
        int utf16Index = -1;
        for (int seen = 0; seen <= occurrence; seen++)
        {
            utf16Index = text.IndexOf(anchor, utf16Index + 1, StringComparison.Ordinal);
            if (utf16Index < 0) throw new InvalidDataException($"Anchor '{anchor}' occurrence {occurrence} not found.");
        }
        int startScalar = ScalarText.Utf16IndexToScalar(text, utf16Index);
        return new ScalarRange(startScalar, startScalar + ScalarText.ScalarLength(anchor));
    }
}

public sealed record GreekFixtureLine
{
    public required string Id { get; init; }
    public required string Material { get; init; }
    public required string ExampleRef { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<Annotation> Annotations { get; init; }
}
