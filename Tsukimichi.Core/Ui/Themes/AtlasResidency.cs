namespace Tsukimichi.Core.Ui.Themes;

/// <summary>The textures of a theme set or frame kit (ATLAS-CONTRACT §1, §7), each loaded on its own.</summary>
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

    /// <summary>A set's <c>faces-row.png</c>: loaded as soon as the set is composed in another kit (1.17 T11).</summary>
    FacesRow,

    /// <summary>A set's <c>faces.png</c>: on the first composed hero draw.</summary>
    Faces,

    /// <summary>A set's <c>faces@2x.png</c>.</summary>
    Faces2x,

    /// <summary>A kit's <c>frames-row.png</c>: loaded as soon as anything is composed in the kit.</summary>
    FramesRow,

    /// <summary>A kit's <c>frames.png</c>: on the first composed hero draw.</summary>
    Frames,

    /// <summary>A kit's <c>frames@2x.png</c>.</summary>
    Frames2x,

    /// <summary>A kit's <c>ornaments.png</c> (Kirikane): loaded as soon as the kit's metal is the ornament's.</summary>
    Ornaments,
}

/// <summary>
/// Which theme set and frame kit textures to keep (theme-system §6.3, "Loading rules"), pure so it is tested without
/// Dalamud. The atlas cache asks it every frame:
/// <list type="bullet">
/// <item><see cref="Retain(ResolvedAppearance, MedalFinish, UiPalette)"/> names what the saved appearance draws at the
/// frame's finish: each atlas set it uses as designed (its <c>medals</c>, <c>plain</c> and <c>row</c>), each set it
/// composes in another kit (its <c>faces</c>; Menphina's Medallion too), and that kit (its <c>frames</c>;
/// <see cref="ResolvedAppearance.Composes"/>). Plain draws no frames, so there no kit is wanted and an atlas set is wanted
/// only for a flat finish of its own (<see cref="GlyphSetInfo.HasPlainFinish"/>; the others draw Medallion's Plain). Their row strips are wanted at once (<see cref="ShouldPreload(GlyphSetId, AtlasPart)"/>); nothing
/// else is loaded until something draws it.</item>
/// <item><see cref="Touch(GlyphSetId, AtlasPart, double)"/> marks a part as drawn this frame; a draw is what loads a hero
/// tier, a 2x tier, or a set outside the appearance (the Themes page's previews).</item>
/// <item><see cref="ShouldRelease(GlyphSetId, AtlasPart, double)"/> lets a loaded part go once it has not been drawn for
/// <see cref="IdleSeconds"/>, unless it is a 1x part the appearance draws. So a set dropped from the appearance, a preview
/// that was only hovered, a kit no longer chosen and a 2x tier no longer drawn all free their memory, while the
/// appearance's own parts never flicker back to a stand-in.</item>
/// </list>
/// Releasing drops the plugin's reference; Dalamud's shared texture cache frees the texture after its own idle period.
/// </summary>
public sealed class AtlasResidency
{
    /// <summary>How long a part may go undrawn before it is released.</summary>
    public const double IdleSeconds = 30.0;

    /// <summary>Slots per kind: ids 1–7 (0 is never an id).</summary>
    private const int Slots = 8;

    private const int PartSlots = (int)AtlasPart.Ornaments + 1;

    // Sets take slots 0–7 and kits 8–15, so one table serves both.
    private readonly bool[] composite = new bool[Slots];
    private readonly bool[] faces = new bool[Slots];
    private readonly bool[] kits = new bool[Slots];
    private FrameKitId ornamentKit;
    private readonly bool[] loaded = new bool[2 * Slots * PartSlots];
    private readonly double[] lastDrawn = new double[2 * Slots * PartSlots];

    /// <summary>
    /// <see cref="Retain(ResolvedAppearance, MedalFinish, UiPalette)"/> at Decoration Full on the appearance's own palette
    /// (its high-contrast form under high contrast; Night for Follow Dalamud, which has no fixed colours).
    /// </summary>
    public void Retain(ResolvedAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var palette = UiPalettes.Get(appearance.Palette);
        Retain(appearance, appearance.Classic ? MedalFinish.Classic : MedalFinish.Gilt, appearance.HighContrast ? palette.HighContrast : palette);
    }

    /// <summary>
    /// Marks what <paramref name="appearance"/> draws at <paramref name="finish"/> on <paramref name="palette"/> (the
    /// palette in effect, its high-contrast form included) as wanted, and everything else as not (see the class remarks).
    /// </summary>
    public void Retain(ResolvedAppearance appearance, MedalFinish finish, UiPalette palette)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(palette);
        Array.Clear(composite);
        Array.Clear(faces);
        Array.Clear(kits);

        // The kit whose metal the ornament takes (the seam's Theme.UseFrameKit): Brass under Classic and high contrast. Its
        // sprites are wanted only where the palette draws them (Theme.KitOrnaments: a dark, standard-contrast palette).
        var metal = appearance.Classic || appearance.HighContrast ? FrameKitId.Brass : appearance.Frames;
        ornamentKit = FrameKitMetals.DrawsOrnamentSprites(palette, metal) ? metal : default;

        // Plain has no frames: a set composed at Full and Quiet draws its own flat finish there (AtlasGlyphSet), so it
        // wants its own atlases and nothing is composed. A set without a flat finish of its own draws Medallion's Plain
        // there (ThemeAtlasRules.Pick), so it wants nothing.
        var composing = finish != MedalFinish.Plain;

        // Indexed: Retain runs every frame, and a foreach over the IReadOnlyList would box its enumerator.
        var sets = GlyphSets.All;
        var composes = false;
        for (var i = 0; i < sets.Count; i++)
        {
            var set = sets[i];
            if (!appearance.Uses(set.Id) || Slot(set.Id) is not (var slot and >= 0))
            {
                continue;
            }

            if (composing && appearance.Composes(set.Id))
            {
                faces[slot] = true;
                composes = true;
            }
            else if (set.Kind == GlyphRenderKind.Atlas && (composing || set.HasPlainFinish))
            {
                composite[slot] = true;
            }
        }

        if (composes && Slot(appearance.Frames) is var kit and >= 0)
        {
            kits[kit] = true;
        }
    }

    /// <summary>Whether the appearance last passed to <see cref="Retain"/> draws anything from <paramref name="set"/>'s atlases.</summary>
    public bool IsWanted(GlyphSetId set) => Slot(set) is var slot and >= 0 && (composite[slot] || faces[slot]);

    /// <summary>Whether that appearance draws <paramref name="set"/> as designed (its <c>medals</c>, <c>plain</c> and <c>row</c>).</summary>
    public bool WantsComposites(GlyphSetId set) => Slot(set) is var slot and >= 0 && composite[slot];

    /// <summary>Whether that appearance composes <paramref name="set"/> from its faces.</summary>
    public bool WantsFaces(GlyphSetId set) => Slot(set) is var slot and >= 0 && faces[slot];

    /// <summary>Whether that appearance composes anything in <paramref name="kit"/>.</summary>
    public bool IsWanted(FrameKitId kit) => Slot(kit) is var slot and >= 0 && kits[slot];

    /// <summary>
    /// Whether that appearance's ornament is <paramref name="kit"/>'s (its frames, unless Classic or high contrast) and the
    /// palette draws the kit's sprites (<see cref="FrameKitMetals.DrawsOrnamentSprites"/>: a dark, standard-contrast
    /// palette): its <c>ornaments</c> strip is wanted.
    /// </summary>
    public bool WantsOrnaments(FrameKitId kit) => Slot(kit) >= 0 && kit == ornamentKit;

    /// <summary>
    /// Whether <paramref name="part"/> of <paramref name="set"/> should be requested before anything draws it: the row
    /// strip of a set drawn as designed, the faces' row strip of a set composed.
    /// </summary>
    public bool ShouldPreload(GlyphSetId set, AtlasPart part) =>
        ((part == AtlasPart.Row && WantsComposites(set)) || (part == AtlasPart.FacesRow && WantsFaces(set))) && !IsLoaded(set, part);

    /// <summary>
    /// Whether <paramref name="part"/> of <paramref name="kit"/> should be requested before anything draws it: a wanted
    /// kit's row strip, and the ornament kit's ornaments.
    /// </summary>
    public bool ShouldPreload(FrameKitId kit, AtlasPart part) =>
        ((part == AtlasPart.FramesRow && IsWanted(kit)) || (part == AtlasPart.Ornaments && WantsOrnaments(kit))) && !IsLoaded(kit, part);

    /// <summary>Records that <paramref name="part"/> of <paramref name="set"/> was drawn (or requested) at <paramref name="now"/> seconds.</summary>
    public void Touch(GlyphSetId set, AtlasPart part, double now) => Touch(Index(Slot(set), part), now);

    /// <summary>Records that <paramref name="part"/> of <paramref name="kit"/> was drawn (or requested) at <paramref name="now"/> seconds.</summary>
    public void Touch(FrameKitId kit, AtlasPart part, double now) => Touch(KitIndex(kit, part), now);

    /// <summary>Whether <paramref name="part"/> of <paramref name="set"/> is held (touched and not released since).</summary>
    public bool IsLoaded(GlyphSetId set, AtlasPart part) => Index(Slot(set), part) is var i and >= 0 && loaded[i];

    /// <summary>Whether <paramref name="part"/> of <paramref name="kit"/> is held.</summary>
    public bool IsLoaded(FrameKitId kit, AtlasPart part) => KitIndex(kit, part) is var i and >= 0 && loaded[i];

    /// <summary>Whether a held part of <paramref name="set"/> should be let go at <paramref name="now"/> (see the class remarks).</summary>
    public bool ShouldRelease(GlyphSetId set, AtlasPart part, double now)
    {
        var kept = part switch
        {
            AtlasPart.Row or AtlasPart.Medals or AtlasPart.Plain => WantsComposites(set),
            AtlasPart.FacesRow or AtlasPart.Faces => WantsFaces(set),
            _ => false,
        };

        return Expired(Index(Slot(set), part), kept, now);
    }

    /// <summary>Whether a held part of <paramref name="kit"/> should be let go at <paramref name="now"/>.</summary>
    public bool ShouldRelease(FrameKitId kit, AtlasPart part, double now) =>
        Expired(KitIndex(kit, part), (part is AtlasPart.FramesRow or AtlasPart.Frames && IsWanted(kit)) || (part == AtlasPart.Ornaments && WantsOrnaments(kit)), now);

    /// <summary>Records that a part of <paramref name="set"/> was let go.</summary>
    public void Released(GlyphSetId set, AtlasPart part) => Release(Index(Slot(set), part));

    /// <summary>Records that a part of <paramref name="kit"/> was let go.</summary>
    public void Released(FrameKitId kit, AtlasPart part) => Release(KitIndex(kit, part));

    private void Touch(int i, double now)
    {
        if (i >= 0)
        {
            lastDrawn[i] = now;
            loaded[i] = true;
        }
    }

    private bool Expired(int i, bool kept, double now) => i >= 0 && loaded[i] && !kept && now - lastDrawn[i] > IdleSeconds;

    private void Release(int i)
    {
        if (i >= 0)
        {
            loaded[i] = false;
        }
    }

    private static int Slot(GlyphSetId set) => (int)set is var s && s > 0 && s < Slots ? s : -1;

    private static int Slot(FrameKitId kit) => (int)kit is var s && s > 0 && s < Slots ? s : -1;

    private static int Index(int slot, AtlasPart part) => slot >= 0 && (uint)part < PartSlots ? (slot * PartSlots) + (int)part : -1;

    private static int KitIndex(FrameKitId kit, AtlasPart part) => Slot(kit) is var s and >= 0 ? Index(Slots + s, part) : -1;
}
