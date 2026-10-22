namespace LeviathanText.Training;

/// <summary>Bounded package inputs. These are resource guards, not a claim that weight
/// deserialization is sandboxed; packages must still come from an authorized source.</summary>
public static class ModelPackageLimits
{
    public const long MaxModelBytes = 1L * 1024 * 1024 * 1024;
    public const long MaxMetadataBytes = 1L * 1024 * 1024;
    public const long MaxManifestBytes = 1L * 1024 * 1024;
    public const long MaxOptimizerBytes = 2L * 1024 * 1024 * 1024;

    public static void EnsureFileSize(string path, long limit, string label)
    {
        if (!File.Exists(path)) return;
        var length = new FileInfo(path).Length;
        if (length > limit)
            throw new InvalidDataException($"{label} is {length} bytes, exceeding the {limit}-byte package limit.");
    }
}
