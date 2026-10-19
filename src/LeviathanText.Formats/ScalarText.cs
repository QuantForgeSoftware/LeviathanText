using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Formats;

/// <summary>Scalar count helper shared by the adapters.</summary>
public static class ScalarText
{
    public static int Count(string text) => Core.Unicode.ScalarText.ScalarLength(text);

    public static string Slice(string text, int start, int end) => Core.Unicode.ScalarText.Slice(text, start, end);
}
