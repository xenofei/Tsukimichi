using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui.Themes;

/// <summary>
/// The renderer seam (feature plan v7 T3; theme-system §6.1): the appearance in effect this frame, and the compositor
/// <see cref="MoonGlyph"/> draws every state medal and gauge through. For a state it looks up the glyph set the
/// appearance gives it (<see cref="ResolvedAppearance.SetFor"/>) and asks that set to draw; a set that cannot draw yet (an
/// atlas still loading, a file it does not ship) gives way to Menphina's Medallion, which always can. A set drawn in a kit
/// other than its own is composed from its faces and that kit's frames (<see cref="ResolvedAppearance.Composes"/>,
/// <see cref="ThemeAtlasCache.TryCompose"/>). Gauges and row badge content come from the frame kit (<see cref="Kit"/>),
/// in its metal (<see cref="Theme.UseFrameKit"/>), or the 1.11 gauges under the Classic theme. With the default
/// appearance every draw is exactly the 1.15 one: Medallion is <see cref="MedalGlyph"/>, Classic is
/// <see cref="LegacyMoonGlyph"/>, and the Brass kit is <see cref="MedalGauge"/>.
/// </summary>
public static class GlyphSeam
{
    private static readonly AppearanceCache Cache = new();
    private static readonly IGlyphSet[] ByState = new IGlyphSet[AppearanceStates.Count];
    private static readonly IGlyphSet?[] AtlasSets = new IGlyphSet?[8];
    private static ResolvedAppearance appearance = null!;
    private static IFrameKit kit = FrameKitRenderers.Brass;

    // Whether the medal being drawn was composed from a set's faces and the kit's frames (AtlasGlyphSet.TryCompose).
    private static bool composed;

    static GlyphSeam() => Apply(ResolvedAppearance.Default);

    /// <summary>The appearance in effect: the saved one, or one pushed by <see cref="PushAppearance"/>.</summary>
    public static ResolvedAppearance Appearance => appearance;

    /// <summary>
    /// Once per frame, before any window draws: resolves the saved appearance (cached; nothing is allocated unless it
    /// changed) and puts it in effect. Returns it, so the caller can feed the palette from it, then call
    /// <see cref="BeginAtlasFrame"/>.
    /// </summary>
    public static ResolvedAppearance Refresh(AppearanceConfig? saved)
    {
        var resolved = Cache.Get(saved);
        Apply(resolved);
        return resolved;
    }

    /// <summary>
    /// Once per frame, after <see cref="Refresh"/> and the palette's refresh (<see cref="Theme.Refresh"/>): lets the atlas
    /// cache load what the appearance draws at this frame's finish on this frame's palette (a composed set's own flat
    /// finish at Plain, the kit's ornament sprites only where the palette draws them) and release what it dropped.
    /// </summary>
    public static void BeginAtlasFrame() => ThemeAtlasCache.BeginFrame(appearance, Theme.MedalFinish, Theme.Palette);

    /// <summary>
    /// Draws as <paramref name="preview"/> until the returned scope is disposed, then restores the frame's appearance: the
    /// Themes page's live preview and the glyph window's A/B. Sets it uses that the saved appearance does not load on
    /// demand and are released once the preview stops drawing them. A struct; <c>using</c> allocates nothing.
    /// </summary>
    public static AppearanceScope PushAppearance(ResolvedAppearance preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var previous = appearance;
        Apply(preview);
        return new AppearanceScope(previous);
    }

    /// <summary>Restores the appearance in effect before <see cref="PushAppearance"/>. Dispose exactly once.</summary>
    public readonly struct AppearanceScope(ResolvedAppearance previous) : IDisposable
    {
        public void Dispose()
        {
            if (previous is not null)
            {
                Apply(previous);
            }
        }
    }

    /// <summary>The frame kit in effect: the 1.11 gauges under the Classic theme, else the appearance's kit.</summary>
    internal static IFrameKit Kit => kit;

    /// <summary>The set that draws <paramref name="state"/> this frame.</summary>
    internal static IGlyphSet SetFor(QuestState state) => ByState[AppearanceStates.Index(state)];

    /// <summary>Draws <paramref name="state"/>'s medal from its set, or Medallion's while the set cannot.</summary>
    internal static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, float alpha = 1f)
    {
        composed = false;
        if (!SetFor(state).TryDraw(dl, center, radius, state, job, alpha))
        {
            MedallionGlyphSet.Instance.TryDraw(dl, center, radius, state, job, alpha);
        }

        // On a light palette every kit keeps a 1 px Abyss outer keyline (spec-1.17 §B2), so a medal's outline never
        // depends on its metal's darkest stop (Silver on Ishgard Snow); at Quiet too when the medal was composed, since the
        // kit's hairline has no light variant (FrameParts.LightKeyline).
        if (radius > 0.5f && alpha > 0f && FrameParts.LightKeyline(Theme.IsLight, Theme.Glyphs.HighContrast, Theme.MedalFinish, composed))
        {
            var (min, size) = MedalGlyph.Box(center, radius);
            var mid = min + new Vector2(size * 0.5f);
            dl.AddCircle(mid, FrameParts.LightKeylineRadius(size), Theme.WithAlpha(GlyphTokens.Abyss, FrameParts.LightKeylineAlpha * alpha), 0, 1f);
        }
    }

    /// <summary>Records that the medal being drawn was composed (its kit's hairline is its Quiet rim).</summary>
    internal static void NoteComposed() => composed = true;

    /// <summary>The veiled Not checked medal from its set, or Medallion's while the set cannot.</summary>
    internal static void DrawVeiled(ImDrawListPtr dl, Vector2 center, float radius, float alpha)
    {
        if (!SetFor(QuestState.Unknown).TryDrawVeiled(dl, center, radius, alpha))
        {
            MedallionGlyphSet.Instance.TryDrawVeiled(dl, center, radius, alpha);
        }
    }

    private static void Apply(ResolvedAppearance resolved)
    {
        if (ReferenceEquals(resolved, appearance))
        {
            return;
        }

        appearance = resolved;

        // Indexed: every pushed preview applies twice a draw, and a foreach over the IReadOnlyList would box its enumerator.
        var states = AppearanceStates.All;
        for (var i = 0; i < states.Count; i++)
        {
            ByState[AppearanceStates.Index(states[i])] = Renderer(resolved.SetFor(states[i]));
        }

        kit = resolved.Classic ? ClassicFrameKit.Instance : FrameKitRenderers.For(resolved.Frames);
        Theme.UseFrameKit(resolved.Classic || resolved.HighContrast ? FrameKitId.Brass : resolved.Frames);
    }

    private static IGlyphSet Renderer(GlyphSetId id)
    {
        switch (id)
        {
            case GlyphSetId.Medallion:
                return MedallionGlyphSet.Instance;
            case GlyphSetId.Classic:
                return ClassicGlyphSet.Instance;
        }

        var slot = (int)id;
        if ((uint)slot >= AtlasSets.Length || GlyphSets.Get(id) is not { Kind: GlyphRenderKind.Atlas } info || info.Id != id)
        {
            return MedallionGlyphSet.Instance;
        }

        return AtlasSets[slot] ??= new AtlasGlyphSet(id);
    }
}
