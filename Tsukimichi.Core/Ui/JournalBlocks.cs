using System;

namespace Tsukimichi.Core.Ui;

/// <summary>The blocks of the Journal tree that a moon-road divider separates (Moon Road proposal §7.2.1, "Section spacing").</summary>
public enum JournalBlock : byte
{
    /// <summary>The All quests node at the top: no divider after it.</summary>
    All,

    /// <summary>The story: both Main Scenario sections and Chronicles of a New Era.</summary>
    Story,

    /// <summary>Sidequests, the allied societies, Class &amp; Job and Other Quests.</summary>
    Side,

    /// <summary>The plugin's own nodes: Unlock quests, Removed from the game, Other paths.</summary>
    Virtual,
}

/// <summary>
/// Which block a Journal tree section belongs to, by JournalSection id (so it holds in every client language), and
/// where the tree draws a divider: between the story, side and virtual blocks, never after All quests.
/// </summary>
public static class JournalBlocks
{
    /// <summary>The block of the section with this JournalSection id: the story up to Chronicles, the side block after it.</summary>
    public static JournalBlock ForSection(uint sectionId) =>
        sectionId <= BannerArts.ChroniclesSection ? JournalBlock.Story : JournalBlock.Side;

    /// <summary>Whether a divider goes between a node of <paramref name="previous"/> and the next one of <paramref name="next"/>.</summary>
    public static bool DividerBetween(JournalBlock previous, JournalBlock next) =>
        previous != JournalBlock.All && previous != next;
}

/// <summary>
/// The road under a Journal tree row (Moon Road proposal §5, §7.2.1): a thin track whose walked part is the node's
/// completion, drawn as length so rows compare at a glance.
/// </summary>
public static class TreeRoad
{
    /// <summary>
    /// The walked length of a road <paramref name="length"/> px long at <paramref name="fraction"/> (clamped; NaN is 0):
    /// 0 with nothing done, at least <paramref name="minimum"/> px once anything is (a 1 % road still shows), never past
    /// the road's end.
    /// </summary>
    public static float Walked(float length, float fraction, float minimum)
    {
        if (!(length > 0f))
        {
            return 0f;
        }

        var f = float.IsFinite(fraction) ? Math.Clamp(fraction, 0f, 1f) : 0f;
        if (f <= 0f)
        {
            return 0f;
        }

        var min = float.IsFinite(minimum) && minimum > 0f ? minimum : 0f;
        return Math.Min(length, Math.Max(min, length * f));
    }
}
