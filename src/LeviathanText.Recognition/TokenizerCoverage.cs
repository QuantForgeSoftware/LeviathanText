using System.Globalization;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Recognition;

public enum TextScript
{
    Common,
    Greek,
    Latin,
    Hebrew,
    Arabic,
    Coptic,
    Cyrillic,
    Unknown,
}

public sealed record TokenizerCoverageReport(
    IReadOnlyList<string> MissingGraphemes,
    IReadOnlyDictionary<TextScript, int> Counts,
    bool MixedScript,
    string SuggestedRoute);

/// <summary>
/// Script/tokenizer coverage checks. Script detection is a bounded Unicode-block heuristic,
/// not language identification; common punctuation, digits and marks do not by themselves
/// make a text mixed-script. Unknown graphemes are reported, never silently discarded.
/// </summary>
public static class TokenizerCoverage
{
    public static TokenizerCoverageReport Inspect(LabelSet labels, string text)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(text);

        var counts = new Dictionary<TextScript, int>();
        var missing = new List<string>();
        foreach (var grapheme in Graphemes.Enumerate(text))
        {
            var script = ScriptOf(grapheme.Text);
            counts[script] = counts.GetValueOrDefault(script) + 1;
            try { _ = labels.Encode([grapheme.Text]); }
            catch (Contracts.LeviathanException ex) when (ex.Error.Code == Contracts.ErrorCode.UnknownLabel)
            {
                missing.Add(grapheme.Text);
            }
        }

        var materialScripts = counts.Keys.Where(script => script is not (TextScript.Common or TextScript.Unknown)).ToHashSet();
        var mixed = materialScripts.Count > 1;
        var suggested = mixed
            ? "mixed-script: route by script or add a mixed profile"
            : materialScripts.Count == 0
                ? "common-or-unknown: inspect text"
                : materialScripts.Single() switch
                {
                    TextScript.Greek => "greek",
                    TextScript.Latin => "latin",
                    TextScript.Hebrew => "hebrew",
                    TextScript.Arabic => "arabic",
                    TextScript.Coptic => "coptic",
                    TextScript.Cyrillic => "cyrillic",
                    _ => "unknown",
                };
        return new TokenizerCoverageReport(
            missing.Distinct(StringComparer.Ordinal).ToArray(),
            counts,
            mixed,
            suggested);
    }

    public static TextScript ScriptOf(string grapheme)
    {
        if (string.IsNullOrEmpty(grapheme)) return TextScript.Unknown;
        int scalar = char.ConvertToUtf32(grapheme, 0);
        return scalar switch
        {
            >= 0x0370 and <= 0x03FF => TextScript.Greek,
            >= 0x1F00 and <= 0x1FFF => TextScript.Greek,
            >= 0x0041 and <= 0x024F => TextScript.Latin,
            >= 0x1E00 and <= 0x1EFF => TextScript.Latin,
            >= 0x0590 and <= 0x05FF => TextScript.Hebrew,
            >= 0x0600 and <= 0x06FF => TextScript.Arabic,
            >= 0x0750 and <= 0x077F => TextScript.Arabic,
            >= 0x2C80 and <= 0x2CFF => TextScript.Coptic,
            >= 0x0400 and <= 0x04FF => TextScript.Cyrillic,
            >= 0x0030 and <= 0x0039 => TextScript.Common,
            >= 0x2000 and <= 0x206F => TextScript.Common,
            >= 0x0020 and <= 0x002F => TextScript.Common,
            >= 0x003A and <= 0x0040 => TextScript.Common,
            _ => TextScript.Unknown,
        };
    }
}
