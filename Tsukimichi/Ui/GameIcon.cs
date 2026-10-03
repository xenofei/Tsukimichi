using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Ui;

/// <summary>
/// Game icons drawn without throwing: <c>GetFromGameIcon</c> throws for an icon the game does not have (a patch can drop
/// or renumber one), and inside a tooltip that broke the window's draw on every hovered frame, so every square icon goes
/// through <c>TryGetFromGameIcon</c> here. The resolution follows the drawn size (<see cref="Orbit.LowResMaxPx"/>: the
/// native texture at small sizes, the hi-res one above, so a 56 px tooltip icon is sharp). Where no icon can be drawn a
/// sunken rounded square holds its place, and an icon the game does not have gets the faded veiled moon on it (the
/// stand-in Moonlit uses for a reward without art), so layout and hover behave as with the image.
/// </summary>
internal static class GameIcon
{
    /// <summary>Alpha of the veiled moon on the stand-in for an icon the game does not have.</summary>
    private const float MissingAlpha = 0.6f;

    /// <summary>Radius of that veiled moon over the stand-in's side (the Moonlit gallery's no-icon proportion).</summary>
    private const float MissingRadius = 0.28f;

    /// <summary>The lookup for <paramref name="iconId"/> drawn <paramref name="sizePx"/> physical pixels across: hi-res above <see cref="Orbit.LowResMaxPx"/>.</summary>
    public static GameIconLookup Lookup(uint iconId, float sizePx) => new(iconId, false, sizePx > Orbit.LowResMaxPx);

    /// <summary>
    /// The loaded texture of <paramref name="iconId"/> at the resolution for <paramref name="sizePx"/>; false while it
    /// loads, for icon 0 and when the game has no such icon. Never throws for a missing icon.
    /// </summary>
    public static bool TryGetWrap(ITextureProvider textures, uint iconId, float sizePx, [NotNullWhen(true)] out IDalamudTextureWrap? wrap) =>
        TryGetWrap(textures, iconId, sizePx, out wrap, out _);

    /// <summary>
    /// The icon as a square item <paramref name="size"/> across at the cursor (where <c>ImGui.Image</c> was), or the
    /// sunken stand-in while it loads or when the game has none. Returns whether the icon itself was drawn.
    /// </summary>
    public static bool Draw(ITextureProvider textures, uint iconId, float size)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        return DrawAt(ImGui.GetWindowDrawList(), textures, iconId, min, min + new Vector2(size, size));
    }

    /// <summary>
    /// As <see cref="Draw(ITextureProvider, uint, float)"/>; with <paramref name="hiRes"/> the high-resolution texture
    /// at any size (Moonlit's reward art, feature plan v6 G6), so a 24 px icon is scaled down from 80 px, never up.
    /// </summary>
    public static bool Draw(ITextureProvider textures, uint iconId, float size, bool hiRes)
    {
        if (!hiRes)
        {
            return Draw(textures, iconId, size);
        }

        ArgumentNullException.ThrowIfNull(textures);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(size, size);
        ImGui.Dummy(new Vector2(size, size));
        var dl = ImGui.GetWindowDrawList();
        if (iconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(iconId, false, true), out var texture))
        {
            if (texture.TryGetWrap(out var wrap, out _))
            {
                dl.AddImage(wrap.Handle, min, max);
                return true;
            }

            DrawStandIn(dl, min, max, size, 0f, missing: false);
            return false;
        }

        DrawStandIn(dl, min, max, size, 0f, missing: true);
        return false;
    }

    /// <summary>
    /// The icon on <paramref name="dl"/> in <paramref name="min"/>..<paramref name="max"/> (no item), rounded by
    /// <paramref name="rounding"/>, or the sunken stand-in while it loads or when the game has none. Returns whether the
    /// icon itself was drawn.
    /// </summary>
    public static bool DrawAt(ImDrawListPtr dl, ITextureProvider textures, uint iconId, Vector2 min, Vector2 max, float rounding = 0f)
    {
        ArgumentNullException.ThrowIfNull(textures);
        var side = MathF.Min(max.X - min.X, max.Y - min.Y);
        if (TryGetWrap(textures, iconId, side, out var wrap, out var missing))
        {
            if (rounding > 0f)
            {
                dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding);
            }
            else
            {
                dl.AddImage(wrap.Handle, min, max);
            }

            return true;
        }

        DrawStandIn(dl, min, max, side, rounding, missing);
        return false;
    }

    /// <summary>A sunken rounded square; with the faded veiled moon on it when the game has no such icon (not while it merely loads).</summary>
    private static void DrawStandIn(ImDrawListPtr dl, Vector2 min, Vector2 max, float side, float rounding, bool missing)
    {
        if (!(side > 0f))
        {
            return;
        }

        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Sunken), rounding > 0f ? rounding : MathF.Round(side * 0.125f));
        if (missing)
        {
            MoonGlyph.DrawVeiled(dl, (min + max) * 0.5f, side * MissingRadius, MissingAlpha);
        }
    }

    private static bool TryGetWrap(ITextureProvider textures, uint iconId, float sizePx, [NotNullWhen(true)] out IDalamudTextureWrap? wrap, out bool missing)
    {
        wrap = null;
        if (iconId == 0 || !textures.TryGetFromGameIcon(Lookup(iconId, sizePx), out var texture))
        {
            missing = true;
            return false;
        }

        missing = false;
        return texture.TryGetWrap(out wrap, out _);
    }
}
