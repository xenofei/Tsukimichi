using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The bead ring (Moon Road proposal §7.7): a ring split into one segment per quest current, lit gold when attuned and
/// dim otherwise, so "4 of 5" is four gold beads. Segments start at 12 o'clock and run clockwise with a small gap
/// between them; a ring of more than <see cref="MaxSegments"/> is drawn as one continuous arc instead (the beads would
/// be too short to count). When the lit count rises the new segments light one after another
/// (<see cref="Shown"/>), a Full-flair moment that never plays under Reduce motion. Pure geometry, allocation-free.
/// </summary>
public static class BeadRingMath
{
    /// <summary>The gap between two segments: 8°.</summary>
    public const float GapRadians = 8f * MathF.PI / 180f;

    /// <summary>More segments than this and the ring is one continuous arc.</summary>
    public const int MaxSegments = 12;

    /// <summary>How long each newly lit segment takes before the next one lights.</summary>
    public const float StepSeconds = 0.09f;

    private const float TwoPi = 2f * MathF.PI;

    /// <summary>The angle a segment spans: its share of the circle less the gap; the whole circle for one segment.</summary>
    public static float SegmentSweep(int count, float gap = GapRadians)
    {
        if (count <= 1)
        {
            return TwoPi;
        }

        return MathF.Max(TwoPi / count * 0.25f, (TwoPi / count) - gap);
    }

    /// <summary>
    /// Where segment <paramref name="index"/> of <paramref name="count"/> starts, in ImGui's angle convention (0 is
    /// 3 o'clock, clockwise positive): the first segment is centred half a gap after 12 o'clock.
    /// </summary>
    public static float SegmentStart(int index, int count, float gap = GapRadians)
    {
        if (count <= 1)
        {
            return GaugeGeometry.StartAngle;
        }

        var share = TwoPi / count;
        return GaugeGeometry.StartAngle + (index * share) + ((share - SegmentSweep(count, gap)) * 0.5f);
    }

    /// <summary>Whether a ring of <paramref name="count"/> segments is drawn as beads rather than one arc.</summary>
    public static bool Segmented(int count) => count is > 1 and <= MaxSegments;

    /// <summary>How long the lighting sequence from <paramref name="from"/> to <paramref name="to"/> lit segments runs.</summary>
    public static float SequenceSeconds(int from, int to) => to > from ? (to - from + 1) * StepSeconds : 0f;

    /// <summary>
    /// The lit segments to show at <paramref name="progress"/> (0..1) of the lighting sequence from
    /// <paramref name="from"/> to <paramref name="to"/>, and how far the newest one has faded in (0..1). A negative
    /// progress (no sequence playing) or a count that fell shows <paramref name="to"/> at once.
    /// </summary>
    public static (int Lit, float HeadAlpha) Shown(int from, int to, float progress)
    {
        if (progress < 0f || float.IsNaN(progress) || to <= from || from < 0)
        {
            return (Math.Max(0, to), 1f);
        }

        var steps = to - from;
        var t = Math.Clamp(progress, 0f, 1f) * (steps + 1);
        var whole = (int)MathF.Floor(t);
        if (whole >= steps)
        {
            return (to, 1f);
        }

        // Segment from + whole + 1 is fading in; the ones before it are lit.
        return (from + whole + 1, t - whole);
    }
}

/// <summary>
/// The grids of the Moon Road panes (proposal §8.2): the Moonlit gallery has one 96 px column per tile (at least two),
/// the Characters dashboard one 120 px column per section ring (at least one), and Flight's zone banner is
/// <c>min(160, 0.35 × width)</c> tall. Widths are logical pixels (the pane's width over the UI scale).
/// </summary>
public static class PaneGrid
{
    /// <summary>A gallery tile's column width.</summary>
    public const float GalleryColumnLogical = 96f;

    /// <summary>A gallery tile's icon side (the game's 64 px hi-res item icon).</summary>
    public const float GalleryIconLogical = 64f;

    /// <summary>A section ring's column width on the Characters dashboard.</summary>
    public const float SectionColumnLogical = 120f;

    /// <summary>A section ring's orbit (the tree's component at 36 px).</summary>
    public const float SectionOrbitLogical = 36f;

    /// <summary>The zone banner's tallest height.</summary>
    public const float BannerMaxLogical = 160f;

    /// <summary>The zone banner's height as a share of the pane's width.</summary>
    public const float BannerWidthShare = 0.35f;

    /// <summary>Gallery columns for a pane <paramref name="widthLogical"/> wide: ⌊w / 96⌋, never under two.</summary>
    public static int GalleryColumns(float widthLogical) => Columns(widthLogical, GalleryColumnLogical, 2);

    /// <summary>Section ring columns for a pane <paramref name="widthLogical"/> wide: ⌊w / 120⌋, never under one.</summary>
    public static int SectionColumns(float widthLogical) => Columns(widthLogical, SectionColumnLogical, 1);

    /// <summary>How many tile rows <paramref name="items"/> tiles take in <paramref name="columns"/> columns.</summary>
    public static int Rows(int items, int columns) => items <= 0 || columns <= 0 ? 0 : ((items - 1) / columns) + 1;

    /// <summary>The zone banner's height for a pane <paramref name="widthLogical"/> wide: min(160, 0.35 w).</summary>
    public static float BannerHeight(float widthLogical) =>
        float.IsFinite(widthLogical) && widthLogical > 0f ? MathF.Min(BannerMaxLogical, BannerWidthShare * widthLogical) : 0f;

    private static int Columns(float widthLogical, float column, int least)
    {
        if (!float.IsFinite(widthLogical) || widthLogical <= 0f)
        {
            return least;
        }

        return Math.Max(least, (int)MathF.Floor(widthLogical / column));
    }
}

/// <summary>
/// The Moonlit kinds list's identity icons (proposal §7.5): the game's own menu icons (<c>MainCommand.Icon</c>) for the
/// kinds the game has a menu for (Mount Guide, Minion Guide, Emotes, Orchestrion List, Fashion Accessories and so on),
/// else an original glyph from the ornament atlas. The table is the MainCommand row, not the icon: the icon is read from
/// the sheet at runtime, so a patch that redraws a menu icon needs no change here. Verified against the sheet in tests.
/// </summary>
public static class MoonlitKindIcons
{
    /// <summary>MainCommand rows (English names in the 2026.09 client).</summary>
    public const uint MainCommandActionsAndTraits = 3;
    public const uint MainCommandAchievements = 6;
    public const uint MainCommandInventory = 10;
    public const uint MainCommandEmotes = 17;
    public const uint MainCommandArmouryChest = 25;
    public const uint MainCommandDutyFinder = 33;
    public const uint MainCommandCompanion = 42;
    public const uint MainCommandMountGuide = 61;
    public const uint MainCommandMinionGuide = 62;
    public const uint MainCommandGoldSaucer = 65;
    public const uint MainCommandAetherCurrents = 67;
    public const uint MainCommandOrchestrionList = 69;
    public const uint MainCommandBlueMagicSpellbook = 81;
    public const uint MainCommandFashionAccessories = 89;

    /// <summary>The MainCommand row whose icon stands for <paramref name="kind"/>; 0 when the game has no menu for it.</summary>
    public static uint MainCommandRow(RewardKind kind) => kind switch
    {
        RewardKind.Item or RewardKind.OptionalItem => MainCommandInventory,
        RewardKind.Emote => MainCommandEmotes,
        RewardKind.Action or RewardKind.GeneralAction or RewardKind.Trait or RewardKind.ClassJob => MainCommandActionsAndTraits,
        RewardKind.Instance or RewardKind.DutyUnlock => MainCommandDutyFinder,
        RewardKind.ArtifactGear => MainCommandArmouryChest,
        RewardKind.Mount => MainCommandMountGuide,
        RewardKind.Minion => MainCommandMinionGuide,
        RewardKind.Orchestrion => MainCommandOrchestrionList,
        RewardKind.TripleTriadCard => MainCommandGoldSaucer,
        RewardKind.Ornament => MainCommandFashionAccessories,
        RewardKind.Barding => MainCommandCompanion,
        RewardKind.AetherCurrent => MainCommandAetherCurrents,
        RewardKind.BlueMageSpell => MainCommandBlueMagicSpellbook,
        RewardKind.Achievement or RewardKind.Title => MainCommandAchievements,
        _ => 0,
    };

    /// <summary>The atlas glyph for a kind without a menu icon (or while the sheet cannot be read): the feature-unlock glyph for system unlocks, else Other.</summary>
    public static OrnamentGlyph Glyph(RewardKind? kind) => kind switch
    {
        null => OrnamentGlyph.Moonlit,
        RewardKind.SystemUnlock => OrnamentGlyph.PlanFallback,
        _ => OrnamentGlyph.Other,
    };
}
