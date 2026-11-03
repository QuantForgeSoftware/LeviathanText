using System.Globalization;
using System.Text.Json;
using LeviathanText.Recognition;
using SkiaSharp;
using Xunit;

namespace LeviathanText.ML.Tests;

public class LinePreprocessingTests
{
    private static SKBitmap GrayPalette()
    {
        byte[] gray = [0, 32, 64, 96, 128, 160, 192, 255];
        var image = new SKBitmap(8, 8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++) image.SetPixel(x, y, new SKColor(gray[x], gray[x], gray[x]));
        return image;
    }

    [Fact]
    public void P01_legacy_threshold_is_strict_and_uses_ink_positive_pixels()
    {
        using var image = GrayPalette();
        var result = LinePreprocessor.Apply(image, LinePreprocessingProfile.P01Otsu(8));

        Assert.Equal(8, result.Height);
        Assert.Equal(8, result.Width);
        Assert.Equal(0, result.BackgroundValue);
        // The historical threshold is 96/255: gray 96 is background, not ink.
        var row = new float[] { 1, 1, 1, 0, 0, 0, 0, 0 };
        for (int y = 0; y < 8; y++) Assert.Equal(row, result.Pixels.Skip(y * 8).Take(8));
    }

    [Fact]
    public void Legacy_grayscale_keeps_original_channel_weights_and_does_not_binarize()
    {
        using var image = new SKBitmap(8, 8);
        image.Erase(new SKColor(20, 80, 160));
        var result = LinePreprocessor.Apply(image, LinePreprocessingProfile.LegacyInkPositive(8));
        // Preserve the original BGRA byte-reader's behavior on the default RGBA
        // resized bitmap, including the historical red/blue swap.
        float expected = 1f - (0.299f * 160 + 0.587f * 80 + 0.114f * 20) / 255f;
        Assert.All(result.Pixels, value => Assert.Equal(expected, value));
        Assert.Equal(0, result.BackgroundValue);
        Assert.InRange(result.Pixels[0], 0.6f, 0.7f);
    }

    [Fact]
    public void P01_histogram_bins_use_floor_rather_than_rounding()
    {
        using var image = GrayPalette();
        // The legacy byte-weighted value is 96.598: floor assigns bin96. Rounding
        // would choose threshold97 and classify this pixel as ink under strict '<'.
        for (int y = 0; y < 8; y++) image.SetPixel(3, y, new SKColor(96, 96, 98));
        var result = LinePreprocessor.Apply(image, LinePreprocessingProfile.P01Otsu(8));
        for (int y = 0; y < 8; y++) Assert.Equal(0, result[y, 3]);
    }

    [Fact]
    public void Luminance_keeps_white_background_and_distinct_gray_values()
    {
        using var image = GrayPalette();
        var result = LinePreprocessor.Apply(image, LinePreprocessingProfile.Luminance(8));
        Assert.Equal(1, result.BackgroundValue);
        Assert.Equal(0, result[0, 0]);
        Assert.Equal(1, result[0, 7]);
        Assert.InRange(result[0, 3], .376f, .377f);
        Assert.Equal(8, result.Pixels.Take(8).Distinct().Count());
    }

    [Fact]
    public void Luminance_uses_color_channels_and_does_not_mutate_source_pixels()
    {
        using var image = new SKBitmap(8, 8);
        var color = new SKColor(20, 80, 160);
        image.Erase(color);
        var result = LinePreprocessor.Apply(image, LinePreprocessingProfile.Luminance(8));
        float expected = (0.299f * 20 + 0.587f * 80 + 0.114f * 160) / 255f;
        Assert.All(result.Pixels, value => Assert.Equal(expected, value));
        Assert.All(image.Pixels, pixel => Assert.Equal(color, pixel));
        Assert.Equal(1, result.BackgroundValue);
    }

    [Theory]
    [InlineData("p01-otsu", 0)]
    [InlineData("ink-positive-grayscale", 0)]
    [InlineData("luminance-grayscale", 1)]
    [InlineData("luminance-local-background-v1", 1)]
    public void Custom_height_aspect_ratio_and_background_are_stable(string recipe, int background)
    {
        using var image = new SKBitmap(40, 20);
        image.Erase(SKColors.White);
        var profile = new LinePreprocessingProfile { Recipe = recipe, Height = 32 };
        var first = LinePreprocessor.Apply(image, profile);
        var second = LinePreprocessor.Apply(image, profile);
        Assert.Equal(32, first.Height);
        Assert.Equal(64, first.Width);
        Assert.Equal(background, first.BackgroundValue);
        Assert.Equal(first.Pixels, second.Pixels);
        using var narrow = new SKBitmap(1, 20);
        narrow.Erase(SKColors.White);
        Assert.Equal(8, LinePreprocessor.Apply(narrow, profile).Width);
    }

    [Fact]
    public void Black_white_P01_inputs_retain_polarity_without_a_different_shortcut()
    {
        using var image = new SKBitmap(8, 8);
        image.Erase(SKColors.White);
        for (int y = 0; y < 8; y++) image.SetPixel(0, y, SKColors.Black);
        var result = LinePreprocessor.Apply(image, LinePreprocessingProfile.P01Otsu(8));
        for (int y = 0; y < 8; y++)
        {
            Assert.Equal(1, result[y, 0]);
            Assert.Equal(0, result[y, 1]);
        }
    }

    [Fact]
    public void Profiles_round_trip_without_losing_version_recipe_height_or_geometry()
    {
        var profile = LinePreprocessingProfile.Luminance(32) with { Geometry = "externally-prepared-line" };
        var restored = JsonSerializer.Deserialize<LinePreprocessingProfile>(JsonSerializer.Serialize(profile));
        Assert.Equal(profile, restored);
        Assert.Equal("line-preprocessing-v1:luminance-grayscale;height=32;geometry=externally-prepared-line", profile.Describe());
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Recipe")]
    [InlineData("Height")]
    [InlineData("Geometry")]
    public void Explicit_json_profiles_require_every_contract_field(string missingField)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(LinePreprocessingProfile.P01Otsu()))!.AsObject();
        node.Remove(missingField);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LinePreprocessingProfile>(node.ToJsonString()));
    }

    [Fact]
    public void Empty_explicit_json_profile_does_not_guess_legacy_defaults() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LinePreprocessingProfile>("{}"));

    [Fact]
    public void Unknown_profiles_invalid_heights_and_height_conflicts_are_rejected()
    {
        var valid = LinePreprocessingProfile.P01Otsu();
        Assert.Throws<InvalidDataException>(() => (valid with { Version = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Recipe = "guess" }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Geometry = "auto-rectify" }).Validate());
        foreach (int height in new[] { 0, 4, 9 })
            Assert.Throws<InvalidDataException>(() => (valid with { Height = height }).Validate());
        Assert.Throws<InvalidDataException>(() => valid.Validate(expectedHeight: 32));
        valid.Validate(expectedHeight: 16);
    }

    [Fact]
    public void Externally_prepared_geometry_is_explicit_and_not_implemented_by_pixel_transform()
    {
        var profile = LinePreprocessingProfile.Luminance(8) with { Geometry = "externally-prepared-line" };
        profile.Validate();
        Assert.Throws<InvalidOperationException>(profile.RequireStandardGeometry);
        using var alreadyPrepared = GrayPalette();
        Assert.Equal(8, LinePreprocessor.Apply(alreadyPrepared, profile).Height);
    }

    [Fact]
    public void Pixel_transform_rejects_empty_images_and_description_is_culture_invariant()
    {
        using var empty = new SKBitmap();
        Assert.Throws<ArgumentException>(() => LinePreprocessor.Apply(empty, LinePreprocessingProfile.P01Otsu()));
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
            Assert.Contains("height=32", LinePreprocessingProfile.Luminance(32).Describe());
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
