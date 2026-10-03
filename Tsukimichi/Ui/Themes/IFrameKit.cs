using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui.Themes;

/// <summary>
/// The frame kit layer behind the renderer seam (feature plan v7 T3; theme-system §3.3, §6.1): everything metal that is
/// not a state's medal, which is to say the gauges (the halo and the filling moon) and a row medal's badge content drawn
/// beside it at text height. A medal's own rim and hero badge are baked into its set's art (ATLAS-CONTRACT §2), so the kit
/// does not draw them. <see cref="GlyphSeam.Kit"/> is the kit in effect; Classic short-circuits to the 1.11 gauges.
/// </summary>
internal interface IFrameKit
{
    /// <summary>The halo gauge (see <see cref="MoonGlyph.DrawHalo"/>).</summary>
    void DrawHalo(ImDrawListPtr dl, Vector2 center, float radius, float fraction, bool onCard, bool dimComplete);

    /// <summary>The filling moon (see <see cref="MoonGlyph.DrawFilling"/>).</summary>
    void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction);

    /// <summary>A row medal's badge content beside it (see <see cref="MedalGlyph.DrawRowBadge"/>); whether anything was drawn.</summary>
    bool DrawRowBadge(ImDrawListPtr dl, Vector2 min, float side, QuestState state, byte job, float alpha);
}

/// <summary>
/// The Brass kit: the Moon Road's gilt brass, as shipped since 1.12 (<see cref="MedalGauge"/>, <see cref="MedalArt.RowGlyph"/>).
/// In 1.16 every kit draws its gauges and row badges in brass; their own metals arrive with the frames choice (1.17 T11),
/// which only has to add an implementation per kit here.
/// </summary>
internal sealed class BrassFrameKit : IFrameKit
{
    public static readonly BrassFrameKit Instance = new();

    public void DrawHalo(ImDrawListPtr dl, Vector2 center, float radius, float fraction, bool onCard, bool dimComplete) =>
        MoonGlyph.DrawMedalHalo(dl, center, radius, fraction, onCard, dimComplete);

    public void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction) =>
        MoonGlyph.DrawMedalFilling(dl, center, radius, fraction);

    public bool DrawRowBadge(ImDrawListPtr dl, Vector2 min, float side, QuestState state, byte job, float alpha) =>
        MedalGlyph.DrawRowBadge(dl, min, side, state, job, alpha);
}

/// <summary>The 1.11 gauges of the Classic theme (<see cref="LegacyMoonGlyph"/>); its moons have no badges.</summary>
internal sealed class ClassicFrameKit : IFrameKit
{
    public static readonly ClassicFrameKit Instance = new();

    public void DrawHalo(ImDrawListPtr dl, Vector2 center, float radius, float fraction, bool onCard, bool dimComplete) =>
        LegacyMoonGlyph.DrawHalo(dl, center, radius, fraction, onCard, dimComplete);

    public void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction) =>
        LegacyMoonGlyph.DrawFilling(dl, center, radius, fraction);

    public bool DrawRowBadge(ImDrawListPtr dl, Vector2 min, float side, QuestState state, byte job, float alpha) => false;
}

/// <summary>The kit implementations by id.</summary>
internal static class FrameKitRenderers
{
    /// <summary>The kit that draws <paramref name="id"/> (Brass for every kit until 1.17 T11 gives each its metal).</summary>
    public static IFrameKit For(FrameKitId id) => id switch
    {
        _ => BrassFrameKit.Instance,
    };
}
