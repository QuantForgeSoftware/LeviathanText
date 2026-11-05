using System.Text.Json;
using System.Text.Json.Nodes;

namespace LeviathanText.Cli;

/// <summary>Credential references only; secret resolution stays inside the transport.</summary>
internal static class VlmCredentialOptions
{
    internal static void Validate(string? name)
    {
        static bool Letter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        if (name is not null && (name.Length is < 1 or > 128 || !(Letter(name[0]) || name[0] == '_')
            || name.Any(c => !(Letter(c) || c is >= '0' and <= '9' || c == '_'))))
            throw new ArgumentException("Credential environment-variable reference is invalid.");
    }

    internal static byte[] AddRunReference(byte[] bytes, string name, JsonSerializerOptions options)
    {
        var run = JsonNode.Parse(bytes)!.AsObject();
        run["authentication"] = new JsonObject { ["mode"] = "bearer-environment", ["environmentVariable"] = name };
        return JsonSerializer.SerializeToUtf8Bytes(run, options);
    }

    internal static void ValidateRunReference(JsonElement run)
    {
        var entries = run.EnumerateObject().Where(p => p.Name == "authentication").ToArray();
        if (entries.Length == 0) return;
        if (entries.Length != 1 || entries[0].Value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid authentication reference metadata.");
        var authentication = entries[0].Value;
        var expected = new HashSet<string>(["mode", "environmentVariable"], StringComparer.Ordinal);
        foreach (var property in authentication.EnumerateObject())
            if (!expected.Remove(property.Name)) throw new InvalidDataException("Unknown or duplicate authentication reference metadata.");
        if (expected.Count != 0 || authentication.GetProperty("mode").ValueKind != JsonValueKind.String
            || authentication.GetProperty("mode").GetString() != "bearer-environment"
            || authentication.GetProperty("environmentVariable").ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Invalid authentication reference metadata.");
        try { Validate(authentication.GetProperty("environmentVariable").GetString()); }
        catch (ArgumentException) { throw new InvalidDataException("Invalid authentication environment-variable reference."); }
        // Offline validation deliberately never resolves the environment reference.
    }

    internal static void WriteScore<T>(Stream stream, T score, JsonSerializerOptions options,
        IReadOnlyDictionary<(string Mode, int Repetition), int> redactions)
    {
        if (redactions.Count == 0)
        {
            // Preserve every pre-auth, unredacted score's exact serialized bytes.
            JsonSerializer.Serialize(stream, score, options);
            return;
        }
        var node = JsonSerializer.SerializeToNode(score, options)!.AsObject();
        node["credentialRedaction"] = new JsonObject
        {
            ["redactedResultCount"] = redactions.Values.Sum(),
            ["textFidelity"] = "Redacted representations were scored; their text is not byte-exact model content. Secrets were not reconstructed.",
            ["requestDigestScope"] = "Request body only; authorization headers are excluded.",
        };
        foreach (var report in node["reports"]!.AsArray())
        {
            var key = (report!["mode"]!.GetValue<string>(), report["repetition"]!.GetValue<int>());
            report["credentialsRedactedCount"] = redactions.GetValueOrDefault(key);
        }
        JsonSerializer.Serialize(stream, node, options);
    }
}
