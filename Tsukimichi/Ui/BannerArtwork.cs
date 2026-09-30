using System;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The texture behind a resolved hero banner (<see cref="BannerChoice"/>, from <see cref="BannerIndex"/>): the game
/// icon for the journal, sibling and duty banners (<c>GetFromGameIcon</c>, hi-res), the zone's loading image
/// (<c>GetFromGame</c>), or the bundled category art (<c>GetFromManifestResource</c>, embedded 752 × 240 PNGs, drawn at
/// 376 × 120 logical so the GPU halves them at UI scale 1). When a game texture fails to load the bundled art stands
/// in, and <c>shown</c> says so; while one is still loading the call returns false and the caller draws its placeholder.
/// Per frame it allocates nothing: the resource names are built once per art.
/// </summary>
public static class BannerArtwork
{
    private static readonly string[] ResourceNames = BuildNames();
    private static readonly ISharedImmediateTexture?[] Bundled = new ISharedImmediateTexture?[ResourceNames.Length];

    /// <summary>
    /// The banner's texture. <paramref name="shown"/> is the source actually drawn (<see cref="BannerSource.Category"/>
    /// when a game texture failed and the bundled art replaced it), for the hero's tooltip caption.
    /// </summary>
    public static bool TryGetWrap(ITextureProvider textures, in BannerChoice choice, [NotNullWhen(true)] out IDalamudTextureWrap? wrap, out BannerSource shown)
    {
        ArgumentNullException.ThrowIfNull(textures);
        shown = choice.Source;
        switch (choice.Source)
        {
            case BannerSource.Own or BannerSource.Sibling or BannerSource.Duty when choice.IconId != 0:
                if (textures.GetFromGameIcon(new GameIconLookup(choice.IconId)).TryGetWrap(out wrap, out var iconError))
                {
                    return true;
                }

                if (iconError is null)
                {
                    return false;
                }

                break;
            case BannerSource.Zone when choice.GamePath is { Length: > 0 } path:
                if (textures.GetFromGame(path).TryGetWrap(out wrap, out var zoneError))
                {
                    return true;
                }

                if (zoneError is null)
                {
                    return false;
                }

                break;
        }

        shown = BannerSource.Category;
        return TryGetBundled(textures, choice.Art, out wrap);
    }

    /// <summary>The bundled category art alone.</summary>
    public static bool TryGetBundled(ITextureProvider textures, BannerArt art, [NotNullWhen(true)] out IDalamudTextureWrap? wrap)
    {
        ArgumentNullException.ThrowIfNull(textures);
        var i = (int)art;
        if (i < 0 || i >= Bundled.Length)
        {
            i = (int)BannerArt.Other;
        }

        Bundled[i] ??= textures.GetFromManifestResource(typeof(BannerArtwork).Assembly, ResourceNames[i]);
        return Bundled[i]!.TryGetWrap(out wrap, out _);
    }

    private static string[] BuildNames()
    {
        var all = BannerArts.All;
        var max = 0;
        foreach (var art in all)
        {
            max = Math.Max(max, (int)art);
        }

        var names = new string[max + 1];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = BannerArts.ResourceName((BannerArt)i);
        }

        return names;
    }
}
