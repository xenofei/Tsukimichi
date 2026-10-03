using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>What a Frames warning is about.</summary>
public enum KitFlagKind : byte
{
    /// <summary>Two states (<see cref="KitFlag.A"/>, <see cref="KitFlag.B"/>) look alike in a list.</summary>
    Pair,

    /// <summary>Ready no longer leads the next state (<see cref="KitFlag.B"/>) by the mix bar.</summary>
    ReadyLead,

    /// <summary>Completed draws the eye more than the bar allows next to Ready.</summary>
    CompletedRecedes,
}

/// <summary>
/// One thing a set's faces miss in a kit that is not their own (from the kit's <c>metrics.json</c> <c>faces[set].flags</c>,
/// compiled by <c>tools/themes/build_themes.py</c> into <c>FrameKitChecks.g.cs</c>).
/// </summary>
/// <param name="Value">The measured value: a pair's distinctness, Ready's lead, or Completed's share of Ready.</param>
/// <param name="Hard">A pair under the "hard to tell apart" bar (10), rather than only "close" (12, or 11 under colour-vision simulation).</param>
/// <param name="ColourVision">The value is under a colour-vision simulation (Machado protanopia, deuteranopia or tritanopia), not greyscale.</param>
public readonly record struct KitFlag(FrameKitId Kit, GlyphSetId Set, KitFlagKind Kind, QuestState A, QuestState B, float Value, bool Hard, bool ColourVision);

/// <summary>
/// The Frames row's warnings (feature plan v7 T11; spec-1.17 §A3's wording and bars): what the chosen kit plus the
/// faces the appearance draws in it miss. Every pairing is allowed and nothing is blocked (theme-system §5.2); the row
/// names the states in plain words. A flag counts only when the appearance composes its set in that kit and draws the
/// flagged states from that set (a mix that takes one of the two states from elsewhere is the mix table's to judge).
/// Pure, so it is tested without ImGui.
/// </summary>
public static partial class FrameKitChecks
{
    /// <summary>Every compiled flag (for tests).</summary>
    public static IReadOnlyList<KitFlag> All => Flags;

    /// <summary>
    /// The flags <paramref name="appearance"/> shows: hard pairs first, then close pairs by value, then Ready's lead and
    /// Completed's recession. Empty when nothing is composed, under high contrast and in the Classic theme.
    /// </summary>
    public static IReadOnlyList<KitFlag> For(ResolvedAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        if (!appearance.ComposesAny)
        {
            return [];
        }

        var found = new List<KitFlag>();
        foreach (var flag in Flags)
        {
            if (flag.Kit != appearance.Frames || !appearance.Composes(flag.Set))
            {
                continue;
            }

            var shown = flag.Kind switch
            {
                KitFlagKind.Pair or KitFlagKind.ReadyLead => appearance.SetFor(flag.A) == flag.Set && appearance.SetFor(flag.B) == flag.Set,
                _ => appearance.SetFor(QuestState.Completed) == flag.Set && appearance.SetFor(QuestState.Ready) == flag.Set,
            };

            if (shown)
            {
                found.Add(flag);
            }
        }

        found.Sort(static (x, y) =>
        {
            var byKind = x.Kind.CompareTo(y.Kind);
            if (byKind != 0)
            {
                return byKind;
            }

            var byHard = y.Hard.CompareTo(x.Hard);
            return byHard != 0 ? byHard : x.Kind == KitFlagKind.CompletedRecedes ? y.Value.CompareTo(x.Value) : x.Value.CompareTo(y.Value);
        });
        return found;
    }
}
