using System.Text;
using System.Text.Json;
using System.Buffers;
using System.Globalization;

namespace LeviathanText.AI;

/// <summary>Wire parsing only. It never trims, normalizes, repairs or judges a reading.</summary>
internal sealed class VisionResponseParser
{
    private readonly StringBuilder _line = new();
    private readonly StringBuilder _eventData = new();
    private bool _done;
    private bool _invalid;
    private bool _seenContent;
    public StringBuilder Text { get; } = new();
    public string? FinishReason { get; private set; }
    public string? ModelId { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }

    // A valid SSE generation may emit no content deltas at all (for example a blank
    // image). JSON completion messages still require their explicit content string.
    public bool IsComplete(bool sse) => !_invalid && FinishReason == "stop" && (sse ? _done : _seenContent);

    public void FeedSse(ReadOnlySpan<char> characters)
    {
        foreach (var character in characters)
        {
            if (character == '\n')
            {
                ConsumeLine(_line.ToString().TrimEnd('\r'));
                _line.Clear();
            }
            else _line.Append(character);
        }
    }

    private void ConsumeLine(string line)
    {
        if (line.Length == 0) { ConsumeEvent(); return; }
        if (line.StartsWith(':')) return;
        if (!line.StartsWith("data:", StringComparison.Ordinal)) return;
        var value = line.AsSpan(5);
        if (value.StartsWith(" ")) value = value[1..];
        if (_eventData.Length != 0) _eventData.Append('\n');
        _eventData.Append(value);
    }

    private void ConsumeEvent()
    {
        if (_eventData.Length == 0) return;
        var data = _eventData.ToString();
        _eventData.Clear();
        if (data == "[DONE]") { _done = true; return; }
        if (_done) _invalid = true;
        ReadCompletion(Encoding.UTF8.GetBytes(data), true);
    }

    public void EndSse()
    {
        // An unterminated final event is retained as partial evidence, not a successful stream.
        if (_line.Length == 0 && _eventData.Length == 0) return;
        _invalid = true;
        ReadPartialSse();
    }

    public void ReadPartialSse()
    {
        if (_line.Length != 0)
        {
            ConsumeLine(_line.ToString().TrimEnd('\r'));
            _line.Clear();
        }
        if (_eventData.Length == 0) return;
        var bytes = Encoding.UTF8.GetBytes(_eventData.ToString());
        _eventData.Clear();
        ReadCompletion(bytes, true);
    }

    public void ReadJson(byte[] bytes) => ReadCompletion(bytes, false);

    private void ReadCompletion(byte[] bytes, bool sse)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { _invalid = true; return; }
            if (root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String)
                ModelId = model.GetString();
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                InputTokens = ReadInt(usage, "prompt_tokens") ?? InputTokens;
                OutputTokens = ReadInt(usage, "completion_tokens") ?? OutputTokens;
            }
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            { _invalid = true; return; }
            if (choices.GetArrayLength() == 0)
            {
                if (!sse || !root.TryGetProperty("usage", out _)) _invalid = true;
                return;
            }
            if (choices.GetArrayLength() != 1) _invalid = true;
            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object) { _invalid = true; return; }
            if (choice.TryGetProperty("index", out var index)
                && (index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out var n) || n != 0))
                _invalid = true;
            if (choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind != JsonValueKind.Null)
            {
                if (finish.ValueKind == JsonValueKind.String) FinishReason = finish.GetString();
                else _invalid = true;
            }
            if (!choice.TryGetProperty(sse ? "delta" : "message", out var message) || message.ValueKind != JsonValueKind.Object)
            { _invalid = true; return; }
            if (message.TryGetProperty("tool_calls", out _) || message.TryGetProperty("function_call", out _)) _invalid = true;
            if (message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind != JsonValueKind.Null) _invalid = true;
            if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                _seenContent = true;
                Text.Append(content.GetString());
            }
            else if (!sse || (message.TryGetProperty("content", out content) && content.ValueKind != JsonValueKind.Null))
                _invalid = true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _invalid = true;
            ReadPartialJson(bytes);
        }
    }

    /// <summary>
    /// Retains complete content tokens and the decodable prefix of an interrupted content
    /// string. An unfinished scalar/escape is not guessed, and the JSON is never repaired.
    /// </summary>
    public void ReadPartialJson(byte[] bytes)
    {
        _invalid = true;
        var reader = new Utf8JsonReader(bytes, isFinalBlock: false, state: default);
        string? property = null;
        bool choices = false, choice = false, message = false;
        bool awaitingContent = false;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    property = reader.GetString();
                    awaitingContent = reader.CurrentDepth == 4 && message && property == "content";
                }
                else if (reader.TokenType == JsonTokenType.StartArray && reader.CurrentDepth == 1 && property == "choices") choices = true;
                else if (reader.TokenType == JsonTokenType.StartObject && reader.CurrentDepth == 2 && choices) choice = true;
                else if (reader.TokenType == JsonTokenType.StartObject && reader.CurrentDepth == 3 && choice && property is "message" or "delta") message = true;
                else if (reader.TokenType == JsonTokenType.String && reader.CurrentDepth == 4 && message && property == "content")
                {
                    _seenContent = true;
                    Text.Append(DecodeStringPrefix(bytes.AsSpan((int)reader.TokenStartIndex + 1), out _));
                    awaitingContent = false;
                }
                else if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 3) message = false;
                else if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 2 && choice) break;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        { /* The bounded raw body retains the unparseable suffix. */ }
        if (awaitingContent)
        {
            var rest = bytes.AsSpan((int)reader.BytesConsumed);
            while (!rest.IsEmpty && rest[0] is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t') rest = rest[1..];
            if (!rest.IsEmpty && rest[0] == '"') Text.Append(DecodeStringPrefix(rest[1..], out _));
        }
    }

    /// <summary>Decode only complete JSON escapes and Unicode scalars, starting after a quote.</summary>
    private static string DecodeStringPrefix(ReadOnlySpan<byte> bytes, out int consumed)
    {
        var text = new StringBuilder();
        int offset = 0;
        while (offset < bytes.Length)
        {
            byte value = bytes[offset];
            if (value == '"') { offset++; break; }
            if (value < 0x20) break;
            if (value == '\\')
            {
                if (offset + 1 >= bytes.Length) break;
                char escaped = (char)bytes[offset + 1];
                char decoded = escaped switch { '"' => '"', '\\' => '\\', '/' => '/', 'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r', 't' => '\t', _ => '\0' };
                if (decoded != '\0') { text.Append(decoded); offset += 2; continue; }
                if (escaped != 'u' || !TryHex(bytes[(offset + 2)..], out char first)) break;
                int length = 6;
                if (char.IsHighSurrogate(first))
                {
                    if (offset + 12 > bytes.Length || bytes[offset + 6] != '\\' || bytes[offset + 7] != 'u'
                        || !TryHex(bytes[(offset + 8)..], out char second) || !char.IsLowSurrogate(second)) break;
                    text.Append(first).Append(second);
                    length = 12;
                }
                else if (char.IsLowSurrogate(first)) break;
                else text.Append(first);
                offset += length;
                continue;
            }
            if (Rune.DecodeFromUtf8(bytes[offset..], out var rune, out int count) != OperationStatus.Done) break;
            text.Append(rune.ToString());
            offset += count;
        }
        consumed = offset;
        return text.ToString();
    }

    private static bool TryHex(ReadOnlySpan<byte> bytes, out char value)
    {
        value = default;
        if (bytes.Length < 4 || !ushort.TryParse(Encoding.ASCII.GetString(bytes[..4]), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number)) return false;
        value = (char)number;
        return true;
    }

    internal static bool ContainsDecodedSecret(ReadOnlySpan<byte> bytes, string secret)
    {
        for (int offset = 0; offset < bytes.Length; offset++)
        {
            if (bytes[offset] != '"') continue;
            var decoded = DecodeStringPrefix(bytes[(offset + 1)..], out int consumed);
            if (decoded.Contains(secret, StringComparison.Ordinal)) return true;
            offset += consumed;
        }
        return false;
    }

    internal static ReadOnlySpan<byte> ValidUtf8Prefix(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        while (offset < bytes.Length && Rune.DecodeFromUtf8(bytes[offset..], out _, out int count) == OperationStatus.Done)
            offset += count;
        return bytes[..offset];
    }

    private static int? ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number) && number >= 0 ? number : null;
}
