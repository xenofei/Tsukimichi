using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui.Themes;

/// <summary>
/// The frame kit layer behind the renderer seam (feature plan v7 T3; theme-system §3.3, §6.1): everything metal that is
/// not a state's medal, which is to say the gauges (the halo and the filling moon) and a row medal's badge content drawn
/// beside it at text height. A medal's rim and hero badge come from the kit's frames atlas when the medal is composed
/// (<see cref="ThemeAtlasCache.TryCompose"/>), and are baked into a set's own art otherwise (ATLAS-CONTRACT §2, §7).
/// <see cref="GlyphSeam.Kit"/> is the kit in effect; Classic short-circuits to the 1.11 gauges.
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
/// A metal kit (Brass, Silver, Lead came, Astrolabe): the Moon Road's gauges (<see cref="MedalGauge"/>) and the row badge
/// content (<see cref="MedalArt.RowGlyph"/>) drawn in the kit's metal. The metal itself is in <see cref="Theme.Gauges"/>
/// and <see cref="Theme.Brass"/>, which the seam sets from the kit (<see cref="Theme.UseFrameKit"/>;
/// <see cref="FrameKitMetals"/>), so Brass draws exactly as shipped. The row badges at text height stay one drawing for
/// every kit (theme-system §3.3: at 12–16 px the material barely shows).
/// </summary>
internal sealed class MetalFrameKit(FrameKitId id) : IFrameKit
{
    public FrameKitId Id { get; } = id;

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
    private static readonly MetalFrameKit[] Kits =
        [new(FrameKitId.Brass), new(FrameKitId.Silver), new(FrameKitId.Came), new(FrameKitId.Astrolabe)];

    /// <summary>The Brass kit, the default.</summary>
    public static IFrameKit Brass => Kits[0];

    /// <summary>The kit that draws <paramref name="id"/>: its own metal, or Brass for a kit without one yet (Kirikane).</summary>
    public static IFrameKit For(FrameKitId id)
    {
        foreach (var kit in Kits)
        {
            if (kit.Id == id)
            {
                return kit;
            }
        }

        return Kits[0];
    }

    /// <summary>
    /// Whether <paramref name="id"/> draws a metal of its own (<see cref="FrameKitMetals.HasOwnMetal"/>): Settings ›
    /// Themes offers the Frames choice once two offered kits do (<see cref="ThemesPage.FramesChoosable"/>).
    /// </summary>
    public static bool HasOwnMetal(FrameKitId id) => FrameKitMetals.HasOwnMetal(id);
}
