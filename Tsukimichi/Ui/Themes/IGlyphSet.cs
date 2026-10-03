using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui.Themes;

/// <summary>
/// A glyph set behind the renderer seam (feature plan v7 T3; theme-system §6.1): draws one state's medal as the set
/// designed it, its frame and, at hero sizes, its badge included. <see cref="GlyphSeam"/> picks the set per state from the
/// appearance and falls back to Menphina's Medallion whenever a set cannot draw (an atlas still loading, a file the set
/// does not ship), so a medal is never blank.
/// </summary>
internal interface IGlyphSet
{
    GlyphSetId Id { get; }

    /// <summary>
    /// Draws the medal of <paramref name="state"/> centred at <paramref name="center"/> with keyline radius
    /// <paramref name="radius"/> px, faded by <paramref name="alpha"/>; <paramref name="job"/> is the ClassJob a Ready on
    /// another job quest is ready on (0: unknown). False, with nothing drawn, when the set cannot draw it this frame.
    /// </summary>
    bool TryDraw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, float alpha);

    /// <summary>The Not checked medal veiled to <paramref name="alpha"/> (a stand-in for a missing icon); false as above.</summary>
    bool TryDrawVeiled(ImDrawListPtr dl, Vector2 center, float radius, float alpha);
}

/// <summary>Menphina's Medallion (1.12): <see cref="MedalGlyph"/>, unchanged. Always draws.</summary>
internal sealed class MedallionGlyphSet : IGlyphSet
{
    public static readonly MedallionGlyphSet Instance = new();

    /// <summary>Alpha of the veiled medal standing in for an icon the game does not have, at full strength.</summary>
    private const float VeiledMedalAlpha = 0.7f;

    public GlyphSetId Id => GlyphSetId.Medallion;

    public bool TryDraw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, float alpha)
    {
        // In another kit Medallion's faces are composed with that kit's frames; until they load, and in Brass, the medal
        // is drawn exactly as shipped.
        if (!Compose(dl, center, radius, state, job, alpha))
        {
            MedalGlyph.Draw(dl, center, radius, state, job, alpha);
        }

        return true;
    }

    public bool TryDrawVeiled(ImDrawListPtr dl, Vector2 center, float radius, float alpha)
    {
        if (!(radius > 0.5f) || Compose(dl, center, radius, QuestState.Unknown, 0, alpha * VeiledMedalAlpha))
        {
            return true;
        }

        var (min, size) = MedalGlyph.Box(center, radius);
        MedalGlyph.DrawMesh(dl, MedalArt.Medal(QuestState.Unknown, MedalTokens.For(Theme.Glyphs), size), min, size, alpha * VeiledMedalAlpha);
        return true;
    }

    /// <summary>
    /// Draws Medallion's face of <paramref name="state"/> in the appearance's kit when the appearance composes Medallion
    /// (<see cref="ResolvedAppearance.Composes"/>: its kit is not Brass) at Full or Quiet; false when it does not, or while
    /// the parts load. The glyph window's forced renderers and high contrast always draw the medal as shipped.
    /// </summary>
    private static bool Compose(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, float alpha) =>
        MedalGlyph.Renderer == MedalRenderer.Auto && !Theme.Glyphs.HighContrast
        && AtlasGlyphSet.TryCompose(dl, GlyphSetId.Medallion, center, radius, state, job, alpha);
}

/// <summary>
/// The 1.11 moons (<see cref="LegacyMoonGlyph"/>), unchanged. Always draws; a fade scales the alpha of the vertices it drew
/// (<see cref="Chrome.FadeVertices"/>), since the moons pack their own colours.
/// </summary>
internal sealed class ClassicGlyphSet : IGlyphSet
{
    public static readonly ClassicGlyphSet Instance = new();

    public GlyphSetId Id => GlyphSetId.Classic;

    public bool TryDraw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, float alpha)
    {
        if (!(alpha > 0f))
        {
            return true;
        }

        var first = dl.VtxBuffer.Size;
        LegacyMoonGlyph.Draw(dl, center, radius, state);
        if (alpha < 1f)
        {
            Chrome.FadeVertices(dl, first, alpha);
        }

        return true;
    }

    public bool TryDrawVeiled(ImDrawListPtr dl, Vector2 center, float radius, float alpha)
    {
        LegacyMoonGlyph.DrawVeiled(dl, center, radius, alpha);
        return true;
    }
}

/// <summary>
/// A set drawn from its pre-rendered atlases (docs/design/v7/themes/ATLAS-CONTRACT.md) through
/// <see cref="ThemeAtlasCache"/>: the hero atlas from 32 px, the whole-pixel row strip below, the flat atlases at
/// Decoration Plain. The game's job icon goes into the seat of a hero Ready on another job medal, as on Medallion's.
/// </summary>
internal sealed class AtlasGlyphSet(GlyphSetId id) : IGlyphSet
{
    /// <summary>Alpha of the veiled medal, as Medallion's.</summary>
    private const float VeiledMedalAlpha = 0.7f;

    public GlyphSetId Id { get; } = id;

    public bool TryDraw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, float alpha)
    {
        if (!(radius > 0.5f) || !(alpha > 0f))
        {
            return true;
        }

        if (Theme.Glyphs.HighContrast)
        {
            // High contrast is one shared low-vision set (the resolver already says so; this covers a pushed palette).
            return false;
        }

        if (GlyphSeam.Appearance.Composes(Id) && Theme.MedalFinish != MedalFinish.Plain)
        {
            // In another kit than its own the set's faces take that kit's frames and badges; Plain has no frames, so it
            // draws the set's flat finish as below.
            return TryCompose(dl, Id, center, radius, state, job, alpha);
        }

        var (min, size) = MedalGlyph.Box(center, radius);
        var tint = Theme.WithAlpha(Vector4.One, alpha);
        var seat = state == QuestState.ReadyOnOtherJob ? JobBadges.Seat(job) : JobSeat.Hand;
        if (!ThemeAtlasCache.TryDraw(dl, Id, state, seat, min, size, Theme.MedalFinish, tint, out var hero))
        {
            return false;
        }

        if (hero && state == QuestState.ReadyOnOtherJob)
        {
            MedalGlyph.DrawJobInSeat(dl, min, size, job, tint);
        }

        return true;
    }

    public bool TryDrawVeiled(ImDrawListPtr dl, Vector2 center, float radius, float alpha) =>
        TryDraw(dl, center, radius, QuestState.Unknown, 0, alpha * VeiledMedalAlpha);

    /// <summary>
    /// <paramref name="set"/>'s face of <paramref name="state"/> composed in the appearance's kit
    /// (<see cref="ThemeAtlasCache.TryCompose"/>), the game's job icon in a hero badge's seat; false, with nothing drawn,
    /// when the appearance does not compose the set or a part is not ready.
    /// </summary>
    internal static bool TryCompose(ImDrawListPtr dl, GlyphSetId set, Vector2 center, float radius, QuestState state, byte job, float alpha)
    {
        var appearance = GlyphSeam.Appearance;
        if (!(radius > 0.5f) || !(alpha > 0f) || !appearance.Composes(set))
        {
            return false;
        }

        var (min, size) = MedalGlyph.Box(center, radius);
        var tint = Theme.WithAlpha(Vector4.One, alpha);
        var seat = state == QuestState.ReadyOnOtherJob ? JobBadges.Seat(job) : JobSeat.Hand;
        if (!ThemeAtlasCache.TryCompose(dl, set, appearance.Frames, state, seat, min, size, Theme.MedalFinish, tint, out var hero))
        {
            return false;
        }

        if (hero && state == QuestState.ReadyOnOtherJob)
        {
            MedalGlyph.DrawJobInSeat(dl, min, size, job, tint);
        }

        return true;
    }
}
