namespace Tsukimichi.Core.Ui;

/// <summary>What a heading line does with a caption that would crowd its title.</summary>
public enum CaptionOverflow : byte
{
    /// <summary>The caption is left out and names itself when the heading is hovered (pane headings, the Journal count).</summary>
    Tooltip = 0,

    /// <summary>The caption drops to its own lines under the heading, wrapped between words (the detail pane's sections, L5).</summary>
    Below = 1,
}

/// <summary>
/// The Moon Road heading line (proposal §5 "section header treatment"): <c>✦ TITLE ────── caption</c>, one set of
/// metrics for every pane's section headings, the detail pane's open sections and the Journal header. Logical pixels:
/// 4 above the line, a line box at least 20 tall, a sigil 10 wide (never more than 0.8 of the title's line), 7 after the
/// sigil, 8 either side of the rule, and a rule shorter than 16 left out rather than drawn as a stub. The caption drops
/// before the rule, and the rule before the title is cut (the title then ends in an ellipsis).
/// </summary>
public static class HeadingLayout
{
    public const float TopPadLogical = 4f;
    public const float MinHeightLogical = 20f;
    public const float SigilLogical = 10f;
    public const float SigilGapLogical = 7f;
    public const float RuleGapLogical = 8f;
    public const float RuleMinLogical = 16f;

    /// <summary>The sigil never grows past this share of the title's line height.</summary>
    public const float SigilLineShare = 0.8f;

    /// <summary>
    /// Lays out one heading line from <paramref name="left"/>, <paramref name="room"/> pixels wide, at
    /// <paramref name="scale"/> (pixels per logical pixel). <paramref name="titleLine"/> and <paramref name="titleWidth"/>
    /// are the title's line height and width in its role; <paramref name="captionWidth"/> 0 is no caption. Every x is
    /// absolute (from <paramref name="left"/>); every y is from the heading's top.
    /// </summary>
    public static HeadingGeometry Compute(float left, float room, float scale, float titleLine, float titleWidth, float captionWidth, float captionLine, bool sigil, CaptionOverflow overflow)
    {
        scale = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        room = float.IsFinite(room) ? MathF.Max(0f, room) : 0f;
        titleLine = Finite(titleLine);
        titleWidth = Finite(titleWidth);
        captionWidth = Finite(captionWidth);
        captionLine = Finite(captionLine);

        var top = MathF.Round(TopPadLogical * scale);
        var height = MathF.Round(MathF.Max(MinHeightLogical * scale, MathF.Max(titleLine, captionWidth > 0f ? captionLine : 0f)));
        var midY = top + MathF.Round(height * 0.5f);
        var ruleGap = RuleGapLogical * scale;
        var ruleMin = RuleMinLogical * scale;
        var right = left + room;

        var x = left;
        var sigilSize = 0f;
        var sigilCenter = left;
        if (sigil)
        {
            sigilSize = MathF.Round(MathF.Min(SigilLogical * scale, MathF.Max(1f, titleLine * SigilLineShare)));
            sigilCenter = MathF.Round(left + (sigilSize * 0.5f));
            x = left + sigilSize + (SigilGapLogical * scale);
        }

        // The caption shows on the line only with the whole title and a rule of at least the minimum before it.
        var captionX = right - captionWidth;
        var captionShown = captionWidth > 0f && captionX - ruleGap - ruleMin >= x + titleWidth + ruleGap;
        var captionBelow = captionWidth > 0f && !captionShown && overflow == CaptionOverflow.Below;
        var titleRoom = MathF.Max(0f, (captionShown ? captionX - ruleGap : right) - x);
        var ruleStart = x + MathF.Min(titleWidth, titleRoom) + ruleGap;
        var ruleEnd = captionShown ? captionX - ruleGap : right;
        if (ruleEnd - ruleStart < ruleMin)
        {
            ruleEnd = ruleStart;
        }

        return new HeadingGeometry(top, height, midY, sigilCenter, sigilSize, x, titleRoom, ruleStart, ruleEnd, captionShown ? captionX : right, captionShown, captionBelow);
    }

    private static float Finite(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;
}

/// <summary>A heading line laid out by <see cref="HeadingLayout.Compute"/>.</summary>
/// <param name="Top">The pad above the line box.</param>
/// <param name="Height">The line box's height; the heading is <see cref="Top"/> + <see cref="Height"/> tall.</param>
/// <param name="MidY">The line box's centre from the heading's top: the title, the sigil, the rule and the caption centre on it.</param>
/// <param name="SigilCenterX">The sigil's centre; meaningless when <see cref="SigilSize"/> is 0.</param>
/// <param name="SigilSize">The sigil's size; 0 for none.</param>
/// <param name="TitleX">Where the title starts.</param>
/// <param name="TitleRoom">The room the title has; a longer one ends in an ellipsis.</param>
/// <param name="RuleStart">Where the rule starts.</param>
/// <param name="RuleEnd">Where it ends; equal to <see cref="RuleStart"/> when it is left out.</param>
/// <param name="CaptionX">Where the caption starts on the line.</param>
/// <param name="CaptionShown">The caption is drawn on the line.</param>
/// <param name="CaptionBelow">The caption did not fit and drops under the heading (<see cref="CaptionOverflow.Below"/>).</param>
public readonly record struct HeadingGeometry(
    float Top,
    float Height,
    float MidY,
    float SigilCenterX,
    float SigilSize,
    float TitleX,
    float TitleRoom,
    float RuleStart,
    float RuleEnd,
    float CaptionX,
    bool CaptionShown,
    bool CaptionBelow)
{
    /// <summary>The heading's full height: the top pad and the line box.</summary>
    public float TotalHeight => Top + Height;

    /// <summary>Whether a rule is drawn.</summary>
    public bool HasRule => RuleEnd > RuleStart;
}

/// <summary>
/// The case policy of every Moon Road heading (the Eyebrow role's face is capitals): English (and the pseudo-language)
/// headings upper-cased with the invariant culture, so a Turkish or Azeri system culture never turns "i" into "İ";
/// every other language keeps its own case and draws in the Caption role in sentence case. The upper-cased forms are
/// cached per string, so a heading drawn every frame allocates nothing after its first.
/// </summary>
public sealed class HeadingCase
{
    /// <summary>Distinct headings kept before the cache starts over.</summary>
    public const int MaxCached = 256;

    private readonly Dictionary<string, string> upper = new(StringComparer.Ordinal);

    /// <summary>How many upper-cased forms are held.</summary>
    public int Count => upper.Count;

    /// <summary><paramref name="text"/> as a heading prints it: upper-cased (invariant) when <paramref name="capitals"/>, else as given.</summary>
    public string For(string text, bool capitals)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!capitals || text.Length == 0)
        {
            return text;
        }

        if (upper.TryGetValue(text, out var cached))
        {
            return cached;
        }

        if (upper.Count >= MaxCached)
        {
            upper.Clear();
        }

        cached = text.ToUpperInvariant();
        upper[text] = cached;
        return cached;
    }
}
