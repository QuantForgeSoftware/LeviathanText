namespace LeviathanText.Core.Unicode;

/// <summary>
/// Decisions adopted for <c>greek-diplomatic-1.0-draft</c>. These are profile policy,
/// not a claim of specialist adjudication; changing any rule requires a new profile
/// version and an explicit conversion of affected revisions.
/// </summary>
public static class GreekDiplomaticProfile
{
    public const string Id = "greek-diplomatic-1.0-draft";
    public const string Version = "1.0-draft";
    public const string Status = "ratified-for-pilot";
    public const string RatifiedBy = "project-owner/Greek-specialist";
    public static readonly DateOnly RatifiedOn = new(2026, 10, 23);

    public enum UnderdotPolicy { PerCharacterPrimary, SpanOnly }

    public sealed record Rule(string Id, string Decision, string Consequence);

    public sealed record Sign(string Id, string Label, int? CodePoint, string Rendering, string Notes);

    public static readonly IReadOnlyList<Rule> Rules =
    [
        new("case", "Preserve visible case; never case-fold L1.",
            "Majuscule hands remain majuscule."),
        new("sigma", "Use ϲ U+03F2 only when the visible form is lunate; otherwise preserve visible σ/ς.",
            "Never substitute one sigma form for another; unexplained forms get a GlyphForm."),
        new("historical-letters", "Preserve the visible modern equivalent only when the character is known; otherwise record a GlyphForm and do not invent a mapping.",
            "No automatic epichoric mapping in 1.0-draft."),
        new("ligatures", "Transcribe the letters represented and add a GlyphForm for the ligature shape.",
            "Ligature appearance is not collapsed into text."),
        new("punctuation", "Transcribe visible marks only; never insert modern punctuation.",
            "Marks without an inventory ID become UnidentifiedMark."),
        new("numerals", "Preserve Greek numeral signs and digits as visible; do not convert to modern values.",
            "NumeralSign annotations carry the value when known."),
        new("abbreviation", "Transcribe visible letters and overbar in L1; expansion is an L2 revision with a reversible range link.",
            "No expansion is written into L1."),
        new("combining-order", "Breathing → diaeresis → accent → iota subscript/other; preserve absent marks.",
            "Applied as a recorded profile operation, not silently during scoring."),
        new("underdot", "Per-character Unclear is primary; span-level Unclear is allowed only when character boundaries cannot be established.",
            "Illegible remains distinct from Unclear."),
        new("scriptio-continua", "Preserve scriptio continua exactly; no spaces are inserted in L1.",
            "Word division belongs to L2 boundary annotations with source-span links."),
        new("correction-order", "Record correction order only with evidence; order basis is explicit, inferred or unknown.",
            "Annotation creation time is never used as evidence of witness order."),
        new("unknown-marks", "Known inventory marks use Sign; visible forms Unicode cannot express use GlyphForm; unknown traces use UnidentifiedMark.",
            "Never guess an unknown mark into a nearby character."),
    ];

    /// <summary>Minimal 1.0-draft sign inventory; extension requires a profile revision.</summary>
    public static readonly IReadOnlyList<Sign> Signs =
    [
        new("overline", "Combining overline", 0x0305, "combining mark", "Visible overbar; abbreviation evidence."),
        new("double-overline", "Combining double overline", 0x033F, "combining mark", "Visible double overbar."),
        new("ano-teleia", "Greek ano teleia", 0x0387, "·", "Visible high point."),
        new("middle-dot", "Middle dot", 0x00B7, "·", "Visible middle point; do not normalize to ano teleia."),
        new("greek-numeral-sign", "Greek numeral sign", 0x0374, "ʹ", "Numeral marker."),
        new("greek-lower-numeral-sign", "Greek lower numeral sign", 0x0375, "͵", "Thousands/lower numeral marker."),
        new("numeral-prime", "Numeral prime", 0x02B9, "ʹ", "Numeral separator where applicable."),
        new("prime", "Prime", 0x2032, "′", "Visible prime."),
        new("double-prime", "Double prime", 0x2033, "″", "Visible double prime."),
        new("fraction-slash", "Fraction slash", 0x2044, "⁄", "Visible fraction separator."),
    ];

    public static bool IsKnownSign(int codePoint) =>
        Signs.Any(sign => sign.CodePoint == codePoint);

    public static bool PreservesL1Spaces => false;

    public static UnderdotPolicy Underdots => UnderdotPolicy.PerCharacterPrimary;

    public static string Summary =>
        $"{Id}@{Version}: visible L1, per-character underdots, no L1 word spaces, explicit correction order.";
}
