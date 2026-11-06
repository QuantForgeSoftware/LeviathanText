using System.Text.Json;
using System.Text.Json.Serialization;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Represents absent placement only on native line/region references. Required entity
/// identities continue to use the strict shared converter.
/// </summary>
public sealed class UnplacedEntityReferenceJsonConverter : JsonConverter<EntityId>
{
    public override bool HandleNull => true;

    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return default;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Native placement reference must be an EntityId string or null.");
        string value = reader.GetString()!;
        // Older native writers emitted the default struct's exact representation.
        // Recover only this known absence sentinel; all other strings stay strict.
        return string.Equals(value, "col-", StringComparison.Ordinal) ? default : EntityId.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        if (value == default) writer.WriteNullValue();
        else writer.WriteStringValue(value.ToString());
    }
}
