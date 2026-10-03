namespace Tsukimichi.Core.Ui.Themes;

/// <summary>The textures of a theme set (ATLAS-CONTRACT §1), each loaded on its own.</summary>
public enum AtlasPart : byte
{
    /// <summary><c>row.png</c>: loaded as soon as the set is in the appearance (rows draw first and most).</summary>
    Row,

    /// <summary><c>medals.png</c>: on the first hero draw.</summary>
    Medals,

    /// <summary><c>medals@2x.png</c>: on the first draw above the largest 1x tier.</summary>
    Medals2x,

    /// <summary><c>plain.png</c>: on the first hero draw at Decoration Plain.</summary>
    Plain,

    /// <summary><c>plain@2x.png</c>.</summary>
    Plain2x,
}

/// <summary>
/// Which theme set textures to keep (theme-system §6.3, "Loading rules"), pure so it is tested without Dalamud. The atlas
/// cache asks it every frame:
/// <list type="bullet">
/// <item><see cref="Retain"/> names the sets the saved appearance uses. Their row strips are wanted at once
/// (<see cref="ShouldPreload"/>); nothing else is loaded until something draws it.</item>
/// <item><see cref="Touch"/> marks a part as drawn this frame; a draw is what loads a hero tier, a 2x tier, or a set
/// outside the appearance (the Themes page's previews).</item>
/// <item><see cref="ShouldRelease"/> lets a loaded part go once it has not been drawn for <see cref="IdleSeconds"/>, unless
/// it is a 1x part of a set the appearance uses. So a set dropped from the appearance, a preview that was only hovered,
/// and a 2x tier no longer drawn all free their memory, while the appearance's own sets never flicker back to a stand-in.</item>
/// </list>
/// Releasing drops the plugin's reference; Dalamud's shared texture cache frees the texture after its own idle period.
/// </summary>
public sealed class AtlasResidency
{
    /// <summary>How long a part may go undrawn before it is released.</summary>
    public const double IdleSeconds = 30.0;

    private const int SetSlots = 8;
    private const int PartSlots = 5;

    private readonly bool[] wanted = new bool[SetSlots];
    private readonly bool[] loaded = new bool[SetSlots * PartSlots];
    private readonly double[] lastDrawn = new double[SetSlots * PartSlots];

    /// <summary>
    /// Marks the atlas sets of <paramref name="appearance"/> as wanted and every other set as not. Procedural sets
    /// (Medallion, Classic) have nothing to load and are never wanted here.
    /// </summary>
    public void Retain(ResolvedAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        for (var s = 0; s < SetSlots; s++)
        {
            wanted[s] = false;
        }

        // Indexed: Retain runs every frame, and a foreach over the IReadOnlyList would box its enumerator.
        var sets = GlyphSets.All;
        for (var i = 0; i < sets.Count; i++)
        {
            var set = sets[i];
            if (set.Kind == GlyphRenderKind.Atlas && appearance.Uses(set.Id) && Slot(set.Id) is var slot and >= 0)
            {
                wanted[slot] = true;
            }
        }
    }

    /// <summary>Whether <paramref name="set"/> is in the appearance last passed to <see cref="Retain"/>.</summary>
    public bool IsWanted(GlyphSetId set) => Slot(set) is var slot and >= 0 && wanted[slot];

    /// <summary>Whether <paramref name="part"/> of <paramref name="set"/> should be requested before anything draws it (a wanted set's row strip).</summary>
    public bool ShouldPreload(GlyphSetId set, AtlasPart part) => part == AtlasPart.Row && IsWanted(set) && !IsLoaded(set, part);

    /// <summary>Records that <paramref name="part"/> of <paramref name="set"/> was drawn (or requested) at <paramref name="now"/> seconds.</summary>
    public void Touch(GlyphSetId set, AtlasPart part, double now)
    {
        if (Index(set, part) is var i and >= 0)
        {
            lastDrawn[i] = now;
            loaded[i] = true;
        }
    }

    /// <summary>Whether <paramref name="part"/> of <paramref name="set"/> is held (touched and not released since).</summary>
    public bool IsLoaded(GlyphSetId set, AtlasPart part) => Index(set, part) is var i and >= 0 && loaded[i];

    /// <summary>Whether a held part should be let go at <paramref name="now"/> (see the class remarks).</summary>
    public bool ShouldRelease(GlyphSetId set, AtlasPart part, double now)
    {
        if (Index(set, part) is not (var i and >= 0) || !loaded[i])
        {
            return false;
        }

        var kept = IsWanted(set) && part is AtlasPart.Row or AtlasPart.Medals or AtlasPart.Plain;
        return !kept && now - lastDrawn[i] > IdleSeconds;
    }

    /// <summary>Records that a part was let go.</summary>
    public void Released(GlyphSetId set, AtlasPart part)
    {
        if (Index(set, part) is var i and >= 0)
        {
            loaded[i] = false;
        }
    }

    private static int Slot(GlyphSetId set) => (int)set is var s && s > 0 && s < SetSlots ? s : -1;

    private static int Index(GlyphSetId set, AtlasPart part) =>
        Slot(set) is var s and >= 0 && (uint)part < PartSlots ? (s * PartSlots) + (int)part : -1;
}
