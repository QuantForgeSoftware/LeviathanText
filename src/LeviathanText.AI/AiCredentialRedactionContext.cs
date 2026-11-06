using System.Text;

namespace LeviathanText.AI;

/// <summary>Ephemeral credentials from at most five attempts of one logical HTTP call.</summary>
internal sealed class AiCredentialRedactionContext : IDisposable
{
    private readonly List<string> _credentials = [];
    private bool _disposed;

    public bool HasCredentials
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _credentials.Count > 0; }
    }

    public void Remember(string? credential)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(credential) || _credentials.Contains(credential, StringComparer.Ordinal)) return;
        if (_credentials.Count >= 5) throw new InvalidOperationException("Credential context exceeded the attempt limit.");
        _credentials.Add(credential);
    }

    public string? SanitizeForRecord(string? text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(text)) return null;
        var cleaned = new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray());
        // Longer keys first also handles rotations where one key prefixes another.
        foreach (var credential in _credentials.OrderByDescending(value => value.Length))
            cleaned = cleaned.Replace(credential, "[redacted]", StringComparison.Ordinal);
        if (_credentials.Count > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(cleaned);
            if (_credentials.Any(credential => VisionResponseParser.ContainsDecodedSecret(bytes, credential)))
                return "[redacted]";
        }
        return cleaned.Length <= 240 ? cleaned : cleaned[..240];
    }

    public bool ContainsCredential(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_credentials.Any(credential => text.Contains(credential, StringComparison.Ordinal))) return true;
        if (_credentials.Count == 0) return false;
        var bytes = Encoding.UTF8.GetBytes(text);
        return _credentials.Any(credential => VisionResponseParser.ContainsDecodedSecret(bytes, credential));
    }

    public void Dispose()
    {
        _credentials.Clear();
        _disposed = true;
    }
}
