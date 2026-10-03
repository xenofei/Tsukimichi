using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui.Themes;

/// <summary>
/// The theme sets' atlases at run time (feature plan v7 T5; theme-system §6.3; docs/design/v7/themes/ATLAS-CONTRACT.md),
/// generalising <see cref="MedalAtlas"/> to sets loaded from disk:
/// <list type="bullet">
/// <item>Only the sets in use load. A set's layouts (<c>medals.json</c>, <c>plain.json</c>, <c>row.json</c>) are read and
/// checked off the UI thread the first time the set is wanted or drawn; a malformed or missing file just leaves that part
/// out (logged once).</item>
/// <item>The row strip is requested as soon as the set is in the appearance; the hero atlas on its first hero draw; the
/// 2x atlas only above the largest 1x tier (while it loads, the 1x atlas's largest tier stands in).</item>
/// <item>A part that has not drawn for <see cref="AtlasResidency.IdleSeconds"/> is released unless it is a 1x part of a set
/// the saved appearance uses (<see cref="AtlasResidency"/>).</item>
/// </list>
/// Until a part is ready <see cref="TryDraw"/> returns false and the seam draws Medallion's procedural medal instead.
/// </summary>
internal static class ThemeAtlasCache
{
    private const int Slots = 8;

    private static readonly AtlasResidency Residency = new();
    private static readonly SetEntry?[] Entries = new SetEntry?[Slots];
    private static ITextureProvider? provider;
    private static string? root;

    /// <summary>Sets the texture provider and the plugin directory; optional, since the plugin's own are used when none is set.</summary>
    public static void Initialize(ITextureProvider textures, string pluginDirectory)
    {
        provider = textures ?? throw new ArgumentNullException(nameof(textures));
        root = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        Array.Clear(Entries);
    }

    /// <summary>
    /// Once per frame, with the saved appearance: marks its atlas sets as wanted, requests their row strips, and releases
    /// parts left idle. Allocation-free once the sets' layouts have loaded.
    /// </summary>
    public static void BeginFrame(ResolvedAppearance appearance)
    {
        Residency.Retain(appearance);
        var now = Now;
        foreach (var set in GlyphSets.All)
        {
            if (set.Kind != GlyphRenderKind.Atlas)
            {
                continue;
            }

            if (Residency.IsWanted(set.Id))
            {
                var entry = Ensure(set.Id);
                if (entry?.Layouts is { } layouts && layouts.Row is not null && Residency.ShouldPreload(set.Id, AtlasPart.Row))
                {
                    Texture(entry, AtlasPart.Row, now);
                }
            }

            ReleaseIdle(set.Id, now);
        }
    }

    /// <summary>Whether <paramref name="set"/> has a hero atlas on disk that parsed (false while its layouts load).</summary>
    public static bool IsDrawable(GlyphSetId set) => Ensure(set)?.Layouts?.Medals is not null;

    /// <summary>
    /// Draws <paramref name="state"/>'s medal of <paramref name="set"/> into the whole-pixel box <paramref name="min"/> ..
    /// <paramref name="min"/> + <paramref name="size"/> at <paramref name="finish"/>, tinted; <paramref name="hero"/> says
    /// whether a hero cell drew (one with a badge seat). False, with nothing drawn, when the part it needs is not ready.
    /// </summary>
    public static bool TryDraw(ImDrawListPtr dl, GlyphSetId set, QuestState state, JobSeat seat, Vector2 min, float size, MedalFinish finish, uint tint, out bool hero)
    {
        hero = false;
        if (Ensure(set)?.Layouts is not { } layouts)
        {
            return false;
        }

        var entry = Entries[(int)set]!;
        var pick = ThemeAtlasRules.Pick(size, finish, layouts.Medals, layouts.Plain, layouts.Row);
        var now = Now;
        var max = min + new Vector2(size);
        switch (pick.Source)
        {
            case AtlasSource.Row:
            {
                if (Texture(entry, AtlasPart.Row, now) is not { } wrap || !layouts.Row!.TryRect(state, pick.Finish, pick.Cell, out var rect))
                {
                    return false;
                }

                var (u0, v0, u1, v1) = layouts.Row.Uv(rect);
                dl.AddImage(wrap.Handle, min, max, new Vector2(u0, v0), new Vector2(u1, v1), tint);
                return true;
            }

            case AtlasSource.Medals:
            case AtlasSource.Plain:
            {
                var flat = pick.Source == AtlasSource.Plain;
                var layout = flat ? layouts.Plain! : layouts.Medals!;
                var tier = pick.Cell;
                var wrap = Texture(entry, pick.TwoX ? (flat ? AtlasPart.Plain2x : AtlasPart.Medals2x) : (flat ? AtlasPart.Plain : AtlasPart.Medals), now);
                if (wrap is null && pick.TwoX)
                {
                    // While the 2x texture loads, the 1x one's largest tier stands in (never the other way round).
                    wrap = Texture(entry, flat ? AtlasPart.Plain : AtlasPart.Medals, now);
                    tier = layout.Tiers[^1];
                }

                if (wrap is null)
                {
                    return false;
                }

                var (u0, v0, u1, v1) = layout.Uv(layout.Rect(MedalLayout.For(state, seat), tier));
                dl.AddImage(wrap.Handle, min, max, new Vector2(u0, v0), new Vector2(u1, v1), tint);
                hero = true;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Seconds on a steady clock, for the residency rules.</summary>
    private static double Now => Environment.TickCount64 / 1000.0;

    private static SetEntry? Ensure(GlyphSetId set)
    {
        var slot = (int)set;
        if ((uint)slot >= Slots || GlyphSets.Get(set) is not { Kind: GlyphRenderKind.Atlas } info || info.Id != set)
        {
            return null;
        }

        if (Entries[slot] is { } entry)
        {
            return entry;
        }

        var directory = PluginDirectory();
        if (directory is null)
        {
            return null;
        }

        entry = new SetEntry(set, directory);
        Entries[slot] = entry;
        entry.StartLoad();
        return entry;
    }

    private static string? PluginDirectory()
    {
        if (root is not null)
        {
            return root;
        }

        var location = Plugin.PluginInterface?.AssemblyLocation;
        return location is null ? null : root = Path.GetDirectoryName(location.FullName);
    }

    /// <summary>The texture of <paramref name="part"/>, requested on first use; null while it loads or when the set has no such file.</summary>
    private static IDalamudTextureWrap? Texture(SetEntry entry, AtlasPart part, double now)
    {
        var layouts = entry.Layouts;
        var file = part switch
        {
            AtlasPart.Row when layouts?.Row is not null => "row.png",
            AtlasPart.Medals when layouts?.Medals is not null => "medals.png",
            AtlasPart.Medals2x when layouts?.Medals is not null => "medals@2x.png",
            AtlasPart.Plain when layouts?.Plain is not null => "plain.png",
            AtlasPart.Plain2x when layouts?.Plain is not null => "plain@2x.png",
            _ => null,
        };

        if (file is null || (provider ?? Plugin.TextureProvider) is not { } textures)
        {
            return null;
        }

        Residency.Touch(entry.Set, part, now);
        ref var texture = ref entry.Textures[(int)part];
        if (texture is null)
        {
            if (entry.Missing[(int)part])
            {
                return null;
            }

            var path = Path.Combine(entry.Directory, ThemeAtlasRules.RelativePath(entry.Set, file));
            if (!File.Exists(path))
            {
                entry.Missing[(int)part] = true;
                entry.WarnOnce($"{file} is missing");
                return null;
            }

            texture = textures.GetFromFile(path);
        }

        return texture.TryGetWrap(out var wrap, out _) ? wrap : null;
    }

    private static void ReleaseIdle(GlyphSetId set, double now)
    {
        if (Entries[(int)set] is not { } entry)
        {
            return;
        }

        for (var p = AtlasPart.Row; p <= AtlasPart.Plain2x; p++)
        {
            if (Residency.ShouldRelease(set, p, now))
            {
                entry.Textures[(int)p] = null;
                Residency.Released(set, p);
            }
        }
    }

    /// <summary>A set's parsed layouts: null for a file the set does not ship or that did not parse.</summary>
    private sealed record SetLayouts(HeroAtlasLayout? Medals, HeroAtlasLayout? Plain, RowStripLayout? Row);

    private sealed class SetEntry(GlyphSetId set, string directory)
    {
        private SetLayouts? layouts;
        private int warned;

        public GlyphSetId Set { get; } = set;

        public string Directory { get; } = directory;

        public ISharedImmediateTexture?[] Textures { get; } = new ISharedImmediateTexture?[(int)AtlasPart.Plain2x + 1];

        /// <summary>Parts whose PNG was not on disk when first asked for (checked once, not every frame).</summary>
        public bool[] Missing { get; } = new bool[(int)AtlasPart.Plain2x + 1];

        /// <summary>The layouts once read; null while they load.</summary>
        public SetLayouts? Layouts => Volatile.Read(ref layouts);

        public void StartLoad() => Task.Run(() => Volatile.Write(ref layouts, new SetLayouts(
            ReadHero("medals.json"),
            ReadHero("plain.json"),
            ReadRow("row.json"))));

        public void WarnOnce(string message)
        {
            if (Interlocked.Exchange(ref warned, 1) == 0)
            {
                Plugin.Log?.Warning("Theme set {Set}: {Message}; Menphina's Medallion stands in", GlyphSets.Get(Set).Key, message);
            }
        }

        private HeroAtlasLayout? ReadHero(string file)
        {
            var text = Read(file);
            if (text is null)
            {
                return null;
            }

            if (HeroAtlasLayout.TryParse(text, out var layout, out var error))
            {
                return layout;
            }

            WarnOnce($"{file} is not a valid layout ({error})");
            return null;
        }

        private RowStripLayout? ReadRow(string file)
        {
            var text = Read(file);
            if (text is null)
            {
                return null;
            }

            if (RowStripLayout.TryParse(text, out var layout, out var error))
            {
                return layout;
            }

            WarnOnce($"{file} is not a valid layout ({error})");
            return null;
        }

        private string? Read(string file)
        {
            var path = Path.Combine(Directory, ThemeAtlasRules.RelativePath(Set, file));
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                WarnOnce($"{file} could not be read ({ex.Message})");
                return null;
            }
        }
    }
}
