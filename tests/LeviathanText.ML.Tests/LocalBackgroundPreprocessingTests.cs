using System.Text.Json;
using LeviathanText.Recognition;
using SkiaSharp;
using Xunit;

namespace LeviathanText.ML.Tests;

public sealed class LocalBackgroundPreprocessingTests
{
    [Theory]
    [InlineData(8, 8, 1)]
    [InlineData(17, 12, 1729)]
    [InlineData(64, 32, 2026)]
    public void Exact_selection_matches_sorted_percentile_on_seeded_RGB_tiles(int width, int height, int seed)
    {
        using var crop = new SKBitmap(width, height);
        var random = new Random(seed);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            crop.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        AssertSingleTileMatchesSortedReference(crop, height);
    }

    [Theory]
    [InlineData(0)] // ascending
    [InlineData(1)] // descending
    [InlineData(2)] // repeated values at the percentile boundary
    [InlineData(3)] // mostly zero
    public void Exact_selection_handles_ordered_and_duplicate_luminances(int pattern)
    {
        using var crop = new SKBitmap(8, 8);
        for (int i = 0; i < 64; i++)
        {
            byte value = (byte)(pattern switch { 0 => i * 4, 1 => (63 - i) * 4, 2 => i < 58 ? 64 : 192, _ => i < 63 ? 0 : 255 });
            crop.SetPixel(i % 8, i / 8, new SKColor(value, value, value));
        }
        AssertSingleTileMatchesSortedReference(crop, 8);
    }

    private static void AssertSingleTileMatchesSortedReference(SKBitmap crop, int height)
    {
        var luminance = crop.Pixels.Select(c => (.299f * c.Red + .587f * c.Green + .114f * c.Blue) / 255f).ToArray();
        var sorted = luminance.OrderBy(v => v).ToArray();
        float background = Math.Max(sorted[(int)Math.Ceiling(.9 * sorted.Length) - 1], .05f);
        var actual = LinePreprocessor.Apply(crop, LinePreprocessingProfile.LocalBackground(height));
        Assert.Equal(crop.Width, actual.Width); // identity-sized float resize
        Assert.Equal(crop.Height, actual.Height);
        for (int i = 0; i < luminance.Length; i++)
            Assert.Equal(BitConverter.SingleToInt32Bits(Math.Clamp(luminance[i] / background, 0f, 1f)), BitConverter.SingleToInt32Bits(actual.Pixels[i]));
    }

    [Fact]
    public void Flat_background_becomes_white_and_dark_strokes_keep_their_ratio()
    {
        using var crop = new SKBitmap(128, 64);
        crop.Erase(new SKColor(128, 128, 128));
        for (int y = 10; y < 54; y++) for (int x = 30; x < 34; x++)
            crop.SetPixel(x, y, new SKColor(32, 32, 32));
        var original = crop.Pixels.ToArray();
        var result = LinePreprocessor.Apply(crop, LinePreprocessingProfile.LocalBackground(64));
        Assert.Equal(1f, result.BackgroundValue);
        Assert.Equal(1f, result[20, 70]);
        Assert.InRange(result[20, 31], .25f - 1e-6f, .25f + 1e-6f);
        Assert.Equal(original, crop.Pixels);
        Assert.All(result.Pixels, value => Assert.InRange(value, 0f, 1f));
    }

    [Fact]
    public void Partial_edge_tiles_use_their_actual_centers_for_bilinear_background()
    {
        using var crop = new SKBitmap(70, 36);
        for (int y = 0; y < crop.Height; y++)
        for (int x = 0; x < crop.Width; x++)
        {
            byte value = (byte)(y < 32 ? (x < 64 ? 128 : 64) : (x < 64 ? 192 : 96));
            crop.SetPixel(x, y, new SKColor(value, value, value));
        }
        crop.SetPixel(49, 24, new SKColor(32, 32, 32));
        var result = LinePreprocessor.Apply(crop, LinePreprocessingProfile.LocalBackground(36));
        // Centers (31.5,66.5) and (15.5,33.5): tx=.5, ty=8.5/18.
        double background = 96 * (1 - 8.5 / 18) + 144 * (8.5 / 18);
        Assert.InRange(result[24, 49], (float)(32 / background - 1e-6), (float)(32 / background + 1e-6));
        Assert.Equal(1f, result[0, 0]);
        Assert.Equal(1f, result[0, 69]);
        Assert.Equal(1f, result[35, 69]);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(255, 1f)]
    [InlineData(4, 4f / 255 / .05f)]
    public void Endpoints_and_denominator_floor_remain_finite(int level, float expected)
    {
        using var crop = new SKBitmap(8, 8);
        byte value = (byte)level;
        crop.Erase(new SKColor(value, value, value));
        var result = LinePreprocessor.Apply(crop, LinePreprocessingProfile.LocalBackground(8));
        Assert.All(result.Pixels, p => Assert.InRange(p, expected - 1e-6f, expected + 1e-6f));
    }

    [Fact]
    public void Float_resize_uses_half_pixel_centers_without_binarizing_faint_ink()
    {
        using var crop = new SKBitmap(16, 16);
        crop.Erase(new SKColor(128, 128, 128));
        crop.SetPixel(4, 4, new SKColor(32, 32, 32));
        var result = LinePreprocessor.Apply(crop, LinePreprocessingProfile.LocalBackground(8));
        Assert.Equal(8, result.Width);
        Assert.Equal(8, result.Height);
        // Source center(4.5,4.5): one quarter-ink pixel plus three white pixels.
        Assert.InRange(result[2, 2], .8125f - 1e-6f, .8125f + 1e-6f);
        Assert.Equal(1f, result[3, 3]);
    }

    [Fact]
    public void Smooth_illumination_gradient_is_reduced_without_erasing_black_strokes()
    {
        using var crop = new SKBitmap(192, 64);
        for (int y = 0; y < crop.Height; y++)
        for (int x = 0; x < crop.Width; x++)
        {
            byte value = (byte)(64 + x / 2);
            crop.SetPixel(x, y, new SKColor(value, value, value));
        }
        for (int y = 12; y < 52; y++)
        foreach (int x in new[] { 30, 90, 150 }) crop.SetPixel(x, y, SKColors.Black);
        var result = LinePreprocessor.Apply(crop, LinePreprocessingProfile.LocalBackground(64));
        float normalizedRange = result[0, 191] - result[0, 0];
        Assert.InRange(normalizedRange, 0, (159f - 64f) / 255);
        Assert.Equal(0f, result[25, 30]);
        Assert.Equal(0f, result[25, 90]);
        Assert.Equal(0f, result[25, 150]);
    }

    [Fact]
    public void New_profile_round_trips_and_existing_profiles_remain_distinct()
    {
        var profile = LinePreprocessingProfile.LocalBackground(32) with { Geometry = "externally-prepared-line" };
        profile.Validate(expectedHeight: 32);
        Assert.Equal("luminance-local-background-v1", profile.Recipe);
        Assert.Equal(profile, JsonSerializer.Deserialize<LinePreprocessingProfile>(JsonSerializer.Serialize(profile)));
        using var crop = new SKBitmap(40, 20);
        crop.Erase(new SKColor(128, 128, 128));
        var normalized = LinePreprocessor.Apply(crop, profile);
        var unchanged = LinePreprocessor.Apply(crop, LinePreprocessingProfile.Luminance(32));
        Assert.Equal(32, normalized.Height);
        Assert.Equal(64, normalized.Width);
        Assert.All(normalized.Pixels, p => Assert.Equal(1f, p));
        Assert.All(unchanged.Pixels, p => Assert.InRange(p, .501f, .503f));
        using var narrow = new SKBitmap(1, 20);
        narrow.Erase(SKColors.White);
        Assert.Equal(8, LinePreprocessor.Apply(narrow, profile).Width);
    }
}
