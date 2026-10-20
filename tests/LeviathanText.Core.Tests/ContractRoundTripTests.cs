using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Tests.Fixtures;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class ContractRoundTripTests
{
    private static readonly GreekFixture Fixture = GreekFixture.Load();

    [Fact]
    public void Fixture_annotations_roundtrip_through_contract_json()
    {
        var revisionId = new EntityId(EntityKind.TranscriptionRevision, "rfix");
        foreach (var line in Fixture.Lines)
        {
            var annotations = line.Annotations
                .Select(a => a with { AnchoredRevisionId = revisionId })
                .ToArray();

            var json = ContractJson.ToJson(annotations);
            var parsed = ContractJson.FromJson<Annotation[]>(json);

            Assert.Equal(annotations, parsed);
        }
    }

    [Fact]
    public void Fixture_line_revision_roundtrips_and_keeps_scalar_offsets_stable()
    {
        foreach (var line in Fixture.Lines)
        {
            var revision = new TextRevision
            {
                Id = new EntityId(EntityKind.TranscriptionRevision, "rfix"),
                ScopeId = new EntityId(EntityKind.Line, "ln-" + line.Id),
                Layer = TextLayer.L1Diplomatic,
                Author = new AgentRef { Id = "fixture", IsModel = false },
                CreatedAt = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero),
                Text = line.Text,
            };

            var parsed = ContractJson.FromJson<TextRevision>(ContractJson.ToJson(revision));
            // GraphemeConfidences is an empty collection on both sides; normalize the instance
            // so record equality does not compare list instances.
            Assert.Equal(
                revision with { GraphemeConfidences = Array.Empty<GraphemeConfidenceSpan>() },
                parsed with { GraphemeConfidences = Array.Empty<GraphemeConfidenceSpan>() });
            Assert.Empty(parsed.GraphemeConfidences);

            // Content-bearing offsets remain valid against the round-tripped text — no normalization drift.
            // Empty ranges (e.g. a Lost gap anchored at the line start) have no slice and are valid as-is.
            Assert.Equal(ScalarText.ScalarLength(line.Text), ScalarText.ScalarLength(parsed.Text));
            foreach (var annotation in line.Annotations.Where(a => !a.Range.IsEmpty))
            {
                var slice = ScalarText.Slice(parsed.Text, annotation.Range.Start, annotation.Range.End);
                Assert.False(string.IsNullOrEmpty(slice));
            }
        }
    }
}
