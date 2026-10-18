using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeviathanText.Contracts.Serialization;

/// <summary>
/// Native JSON conventions for contract records (interchange-and-api.md §2; the prose
/// contract is the source for the generated HTTP client). Conventions:
/// - camelCase property names; enums as camelCase strings; unknown enum strings are a
///   hard error when reading (schema-strict), integers are rejected.
/// - EntityId serializes as its string form ("ln-abc").
/// - ScalarRange serializes as {"start":n,"end":m} (half-open).
/// - Confidence serializes as "none" or {"value":x,"scale":"…"}.
/// </summary>
public static class ContractJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.Strict,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new EntityIdJsonConverter());
        options.Converters.Add(new ScalarRangeJsonConverter());
        options.Converters.Add(new ConfidenceJsonConverter());
        return options;
    }

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException($"JSON for {typeof(T).Name} deserialized to null.");

    public static T FromJson<T>(ReadOnlySpan<byte> utf8) => JsonSerializer.Deserialize<T>(utf8, Options)
        ?? throw new JsonException($"JSON for {typeof(T).Name} deserialized to null.");
}

public sealed class EntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException("EntityId must be a JSON string.");
        return EntityId.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

public sealed class ScalarRangeJsonConverter : JsonConverter<ScalarRange>
{
    public override ScalarRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("ScalarRange must be an object.");
        int start = -1, end = -1;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (start < 0 || end < 0) throw new JsonException("ScalarRange requires 'start' and 'end'.");
                return new ScalarRange(start, end);
            }
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Unexpected ScalarRange content.");
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "start": start = reader.GetInt32(); break;
                case "end": end = reader.GetInt32(); break;
                default: throw new JsonException($"Unknown ScalarRange member '{name}'.");
            }
        }
        throw new JsonException("Unterminated ScalarRange object.");
    }

    public override void Write(Utf8JsonWriter writer, ScalarRange value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("start", value.Start);
        writer.WriteNumber("end", value.End);
        writer.WriteEndObject();
    }
}

public sealed class ConfidenceJsonConverter : JsonConverter<Confidence>
{
    public override Confidence Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (reader.GetString() == "none") return Confidence.None;
            throw new JsonException("Confidence string value must be \"none\".");
        }
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Confidence must be \"none\" or an object.");

        double value = double.NaN;
        ConfidenceScale scale = default;
        bool hasScale = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (double.IsNaN(value) || !hasScale) throw new JsonException("Confidence requires 'value' and 'scale'.");
                return Confidence.Of(value, scale);
            }
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Unexpected Confidence content.");
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "value": value = reader.GetDouble(); break;
                case "scale": scale = JsonSerializer.Deserialize<ConfidenceScale>(ref reader, options); hasScale = true; break;
                default: throw new JsonException($"Unknown Confidence member '{name}'.");
            }
        }
        throw new JsonException("Unterminated Confidence object.");
    }

    public override void Write(Utf8JsonWriter writer, Confidence value, JsonSerializerOptions options)
    {
        if (value.IsNone)
        {
            writer.WriteStringValue("none");
            return;
        }
        writer.WriteStartObject();
        writer.WriteNumber("value", value.Value);
        writer.WritePropertyName("scale");
        JsonSerializer.Serialize(writer, value.Scale, options);
        writer.WriteEndObject();
    }
}
