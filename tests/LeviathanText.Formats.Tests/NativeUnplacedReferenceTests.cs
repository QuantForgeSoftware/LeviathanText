using System.Text.Json;
using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;

namespace LeviathanText.Formats.Tests;

public sealed class NativeUnplacedReferenceTests
{
    private static readonly string[] ReferencePaths =
    [
        "project/objects/0/surfaces/0/lines/0/regionId",
        "project/objects/0/surfaces/0/lines/0/frameRef",
        "project/objects/0/surfaces/0/regions/0/frameRef",
    ];
    public static TheoryData<string> References => new(ReferencePaths);
    public static TheoryData<string, string> InvalidReferences
    {
        get
        {
            var data = new TheoryData<string, string>();
            string[] invalid = ["\"\"", "\"col- \"", "\"COL-\"", "\"reg-\"", "\"cap-\"", "\"unknown-x\"", "\" col-\"", "1", "{}", "[]", "true"];
            foreach (string path in ReferencePaths) foreach (string json in invalid) data.Add(path, json);
            return data;
        }
    }
    public static TheoryData<string, string> InvalidRequiredIdentities
    {
        get
        {
            var data = new TheoryData<string, string>();
            string[] paths = ["project/id", "project/objects/0/id", "project/objects/0/surfaces/0/id",
                "project/objects/0/surfaces/0/captures/0/id", "project/objects/0/surfaces/0/regions/0/id", "project/objects/0/surfaces/0/lines/0/id"];
            foreach (string path in paths) foreach (string json in new[] { "null", "\"col-\"" }) data.Add(path, json);
            return data;
        }
    }

    private static NativeProject Unplaced(string path)
    {
        var original = SampleProject.Build(); var obj = original.Objects[0]; var surface = obj.Surfaces[0];
        var line = surface.Lines[0]; var region = surface.Regions[0];
        if (path.EndsWith("lines/0/regionId", StringComparison.Ordinal)) line = line with { RegionId = default };
        else if (path.EndsWith("lines/0/frameRef", StringComparison.Ordinal)) line = line with { FrameRef = default };
        else region = region with { FrameRef = default };
        return original with { Objects = [obj with { Surfaces = [surface with {
            Lines = surface.Lines.Select((value, index) => index == 0 ? line : value).ToArray(),
            Regions = surface.Regions.Select((value, index) => index == 0 ? region : value).ToArray(),
        }] }] };
    }
    private static (JsonNode Parent, string Key) Parent(JsonNode node, string path)
    {
        string[] parts = path.Split('/');
        foreach (string part in parts[..^1]) node = node is JsonArray array ? array[int.Parse(part)]! : node[part]!;
        return (node, parts[^1]);
    }
    private static string With(string original, string path, string json)
    {
        var node = JsonNode.Parse(original)!; var (parent, key) = Parent(node, path); parent[key] = JsonNode.Parse(json);
        return node.ToJsonString();
    }
    private static EntityId Reference(NativeProject project, string path)
    {
        var surface = project.Objects[0].Surfaces[0];
        return path.EndsWith("lines/0/regionId", StringComparison.Ordinal) ? surface.Lines[0].RegionId
            : path.EndsWith("lines/0/frameRef", StringComparison.Ordinal) ? surface.Lines[0].FrameRef : surface.Regions[0].FrameRef;
    }

    [Theory, MemberData(nameof(References))]
    public void Saving_an_explicit_unplaced_reference_can_reload_without_minting_or_assigning_identity(string path)
    {
        var project = Unplaced(path); string json = NativeJson.Save(project);
        var loaded = NativeJson.Load(json);
        Assert.Equal(default, Reference(loaded, path));
        Assert.Equal(project.Id, loaded.Id);
        Assert.Equal(ContractJson.ToJson(project.Objects[0].Surfaces[0].Revisions), ContractJson.ToJson(loaded.Objects[0].Surfaces[0].Revisions));
        Assert.Equal(json, NativeJson.Save(loaded));
        var (parent, key) = Parent(JsonNode.Parse(json)!, path); Assert.Null(parent[key]);
    }

    [Theory, MemberData(nameof(References))]
    public void Explicit_null_is_an_unplaced_reference_and_preserves_the_complete_document(string path)
    {
        string json = With(NativeJson.Save(SampleProject.Build()), path, "null");
        var loaded = NativeJson.Load(json);
        Assert.Equal(default, Reference(loaded, path));
        string canonical = NativeJson.Save(loaded);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(canonical)));
        Assert.Equal(canonical, NativeJson.Save(NativeJson.Load(canonical)));
    }

    [Theory, MemberData(nameof(References))]
    public void Exact_legacy_default_sentinel_is_recovered_only_as_absence_in_the_three_reference_fields(string path)
    {
        string valid = NativeJson.Save(SampleProject.Build()); string legacy = With(valid, path, "\"col-\"");
        var loaded = NativeJson.Load(legacy);
        Assert.Equal(default, Reference(loaded, path));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(With(valid, path, "null")), JsonNode.Parse(NativeJson.Save(loaded))));
        Assert.Contains("\"col-\"", legacy); // Loading never rewrites the caller's original rescue bytes.
    }

    [Theory, MemberData(nameof(InvalidReferences))]
    public void Other_malformed_reference_values_remain_refused(string path, string json)
    {
        string original = NativeJson.Save(SampleProject.Build());
        Assert.ThrowsAny<Exception>(() => NativeJson.Load(With(original, path, json)));
    }

    [Theory, MemberData(nameof(InvalidRequiredIdentities))]
    public void Required_entity_identity_is_not_relaxed_by_unplaced_reference_recovery(string path, string json)
    {
        Assert.ThrowsAny<Exception>(() => NativeJson.Load(With(NativeJson.Save(SampleProject.Build()), path, json)));
    }

    [Theory, MemberData(nameof(References))]
    public void Required_reference_property_presence_is_still_required(string path)
    {
        var node = JsonNode.Parse(NativeJson.Save(SampleProject.Build()))!;
        var (parent, key) = Parent(node, path); Assert.True(((JsonObject)parent).Remove(key));
        Assert.Throws<JsonException>(() => NativeJson.Load(node.ToJsonString()));
    }

    [Theory, MemberData(nameof(References))]
    public void Valid_known_kind_strings_are_not_reinterpreted_or_restricted_during_forensic_roundtrip(string path)
    {
        string json = With(NativeJson.Save(SampleProject.Build()), path, "\"col--\"");
        var loaded = NativeJson.Load(json);
        Assert.Equal(EntityId.Parse("col--"), Reference(loaded, path));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(NativeJson.Save(loaded))));
    }

    [Fact]
    public void Global_entity_parser_and_required_entity_json_remain_strict()
    {
        Assert.False(EntityId.TryParse("col-", out _));
        Assert.ThrowsAny<Exception>(() => ContractJson.FromJson<EntityId>("null"));
        Assert.ThrowsAny<Exception>(() => ContractJson.FromJson<EntityId>("\"col-\""));
        string json = NativeJson.Save(SampleProject.Build()); Assert.Equal(json, NativeJson.Save(NativeJson.Load(json)));
    }
}
