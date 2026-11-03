namespace LeviathanText.Recognition;

/// <summary>
/// Executable, versioned preparation of an already extracted line crop. A null profile
/// in older metadata is distinct from an explicit profile and retains legacy routing.
/// </summary>
public sealed record LinePreprocessingProfile
{
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; init; } = 1;
    [System.Text.Json.Serialization.JsonRequired]
    public string Recipe { get; init; } = "p01-otsu";
    [System.Text.Json.Serialization.JsonRequired]
    public int Height { get; init; } = CrnnModel.InputHeight;

    /// <summary>
    /// line-crop accepts ordinary extracted line images. externally-prepared-line
    /// declares geometry (for example rectification) that generic page routing cannot reproduce.
    /// </summary>
    [System.Text.Json.Serialization.JsonRequired]
    public string Geometry { get; init; } = "line-crop";

    public static LinePreprocessingProfile P01Otsu(int height = CrnnModel.InputHeight) =>
        new() { Recipe = "p01-otsu", Height = height };

    public static LinePreprocessingProfile LegacyInkPositive(int height = CrnnModel.InputHeight) =>
        new() { Recipe = "ink-positive-grayscale", Height = height };

    public static LinePreprocessingProfile Luminance(int height = CrnnModel.InputHeight) =>
        new() { Recipe = "luminance-grayscale", Height = height };

    /// <summary>Fixed 64×32 tile/p90 background normalization followed by floating-point linear resize.</summary>
    public static LinePreprocessingProfile LocalBackground(int height = CrnnModel.InputHeight) =>
        new() { Recipe = "luminance-local-background-v1", Height = height };

    public void Validate(int? expectedHeight = null)
    {
        if (Version != 1)
            throw new InvalidDataException($"Unsupported line preprocessing version {Version}.");
        if (Recipe is not ("p01-otsu" or "ink-positive-grayscale" or "luminance-grayscale" or "luminance-local-background-v1"))
            throw new InvalidDataException($"Unsupported line preprocessing recipe '{Recipe}'.");
        if (Geometry is not ("line-crop" or "externally-prepared-line"))
            throw new InvalidDataException($"Unsupported line preprocessing geometry '{Geometry}'.");
        if (Height < 8 || Height % 4 != 0)
            throw new InvalidDataException("Line preprocessing height must be at least 8 and divisible by 4.");
        if (expectedHeight is { } expected && Height != expected)
            throw new InvalidDataException($"Line preprocessing height {Height} does not match model input height {expected}.");
    }

    public void RequireStandardGeometry()
    {
        Validate();
        if (Geometry != "line-crop")
            throw new InvalidOperationException("This profile requires externally prepared line geometry; generic line cropping cannot reproduce it.");
    }

    public string Describe() => FormattableString.Invariant($"line-preprocessing-v{Version}:{Recipe};height={Height};geometry={Geometry}");
}
