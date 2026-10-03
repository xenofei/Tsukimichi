using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The bundled giver-portrait atlas (1.15 design spec A4, A6; docs/design/v7/ui/1.15/silhouettes/gen_portrait_atlas.py):
/// the sixteen race silhouettes, the moon disc and Full's plate shade, each the plate's whole 72-unit box, rendered from
/// their SVG masters at four tiers and embedded as <c>Tsukimichi.assets.ui.portraits.png</c> (1024 × 616) and
/// <c>portraits@2x.png</c> (2048 × 1232). Loaded once each through <see cref="ITextureProvider.GetFromManifestResource"/>
/// as <see cref="MedalAtlas"/> loads the medals; the plugin never renders SVG. <see cref="PortraitAtlasLayout.Pick"/>
/// chooses the tier, so a sprite is never shrunk by more than 1.5×; the 2x texture is only asked for above 128 px. Until
/// a texture has loaded <see cref="TryDraw"/> returns false and the plate shows its well alone.
/// </summary>
public static class PortraitAtlas
{
    private static ITextureProvider? provider;
    private static ISharedImmediateTexture? atlas1x;
    private static ISharedImmediateTexture? atlas2x;

    /// <summary>Sets the texture provider; optional, since the plugin's own service is used when none is set.</summary>
    public static void Initialize(ITextureProvider textures)
    {
        provider = textures ?? throw new ArgumentNullException(nameof(textures));
        atlas1x = atlas2x = null;
    }

    /// <summary>
    /// Draws <paramref name="sprite"/> over the plate <paramref name="min"/>..<paramref name="max"/> (its 72-unit box),
    /// tinted by <paramref name="tint"/>; false, with nothing drawn, while the texture it needs is still loading.
    /// </summary>
    public static bool TryDraw(ImDrawListPtr dl, PortraitSprite sprite, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu)
    {
        var (tier, twoX) = PortraitAtlasLayout.Pick(max.X - min.X);
        if (!TryGetWrap(twoX, out var wrap))
        {
            // While the 2x texture loads, the 1x one's largest tier stands in (never the other way: that would load 2x early).
            if (!twoX || !TryGetWrap(twoX: false, out wrap))
            {
                return false;
            }

            tier = PortraitAtlasLayout.Tiers[^1];
        }

        var (u0, v0, u1, v1) = PortraitAtlasLayout.Uv(PortraitAtlasLayout.Rect(sprite, tier));
        dl.AddImage(wrap.Handle, min, max, new Vector2(u0, v0), new Vector2(u1, v1), tint);
        return true;
    }

    private static bool TryGetWrap(bool twoX, out IDalamudTextureWrap wrap)
    {
        var textures = provider ?? Plugin.TextureProvider;
        if (textures is null)
        {
            wrap = null!;
            return false;
        }

        if (twoX)
        {
            atlas2x ??= textures.GetFromManifestResource(typeof(PortraitAtlas).Assembly, PortraitAtlasLayout.ResourceName2x);
            return atlas2x.TryGetWrap(out wrap!, out _);
        }

        atlas1x ??= textures.GetFromManifestResource(typeof(PortraitAtlas).Assembly, PortraitAtlasLayout.ResourceName1x);
        return atlas1x.TryGetWrap(out wrap!, out _);
    }
}
