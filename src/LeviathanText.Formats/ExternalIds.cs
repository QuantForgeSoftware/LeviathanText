namespace LeviathanText.Formats;

/// <summary>
/// Converts document-supplied IDs into the bounded EntityId suffix alphabet. PAGE/ALTO/hOCR
/// producers sometimes use spaces, parentheses or slashes; those are replaced rather than
/// dropping the record. Uniqueness is preserved by per-import scoping later.
/// </summary>
public static class ExternalIds
{
    private const int MaxLength = 63;

    public static string Sanitize(string? value)
    {
        var chars = (value ?? "").Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-' ? c : '_').ToArray();
        var result = new string(chars);
        if (result.Length == 0) result = "id";
        return result.Length <= MaxLength ? result : result[..MaxLength];
    }
}
