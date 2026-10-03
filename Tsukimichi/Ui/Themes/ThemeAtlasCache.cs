using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
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
/// <item>A requested part is rented (<see cref="ISharedImmediateTexture.RentAsync"/>), so Dalamud's shared cache cannot let
/// it go while the plugin holds it: a set's medals never drop back to a stand-in after a spell off-screen.</item>
/// <item>A part that has not drawn for <see cref="AtlasResidency.IdleSeconds"/> is released unless it is a 1x part of a set
/// the saved appearance uses (<see cref="AtlasResidency"/>). Its texture is disposed a frame later, never in a frame that
/// could still draw it; everything is disposed on unload (<see cref="Dispose()"/>).</item>
/// </list>
/// Until a part is ready <see cref="TryDraw"/> returns false and the seam draws Medallion's procedural medal instead.
/// </summary>
internal static class ThemeAtlasCache
{
    private const int Slots = 8;

    /// <summary>Frames a released texture is kept before it is disposed, so no draw list still holds it.</summary>
    private const int RetireFrames = 1;

    private static readonly AtlasResidency Residency = new();
    private static readonly SetEntry?[] Entries = new SetEntry?[Slots];
    private static readonly List<(IDalamudTextureWrap Wrap, long Frame)> Retired = [];
    private static ITextureProvider? provider;
    private static string? root;
    private static long frame;

    /// <summary>Sets the texture provider and the plugin directory; optional, since the plugin's own are used when none is set.</summary>
    public static void Initialize(ITextureProvider textures, string pluginDirectory)
    {
        provider = textures ?? throw new ArgumentNullException(nameof(textures));
        root = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        ReleaseAll();
    }

    /// <summary>On unload: disposes every texture held, released or still renting.</summary>
    public static void Dispose() => ReleaseAll();

    /// <summary>
    /// Once per frame, with the saved appearance: marks its atlas sets as wanted, requests their row strips, and releases
    /// parts left idle; disposes the textures released a frame ago. Allocation-free once the sets' layouts have loaded.
    /// </summary>
    public static void BeginFrame(ResolvedAppearance appearance)
    {
        frame++;
        FlushRetired();
        Residency.Retain(appearance);
        var now = Now;
        var sets = GlyphSets.All;
        for (var i = 0; i < sets.Count; i++)
        {
            var set = sets[i];
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

    /// <summary>
    /// The texture of <paramref name="part"/>, requested and rented on first use; null while it loads, when the set has no
    /// such file, or when it failed to load (logged once).
    /// </summary>
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
        ref var held = ref entry.Parts[(int)part];
        if (held.Wrap is { } owned)
        {
            return owned;
        }

        if (entry.Missing[(int)part])
        {
            return null;
        }

        if (held.Shared is null)
        {
            var path = Path.Combine(entry.Directory, ThemeAtlasRules.RelativePath(entry.Set, file));
            if (!File.Exists(path))
            {
                entry.Missing[(int)part] = true;
                entry.WarnOnce($"{file} is missing");
                return null;
            }

            try
            {
                held.Shared = textures.GetFromFile(path);
                held.Rent = held.Shared.RentAsync();
            }
            catch (Exception ex)
            {
                Fail(entry, part, file, ex);
                return null;
            }
        }

        if (held.Rent is { IsCompleted: true } rent)
        {
            if (rent.IsCompletedSuccessfully)
            {
                held.Rent = null;
                return held.Wrap = rent.Result;
            }

            Fail(entry, part, file, rent.Exception?.GetBaseException());
            return null;
        }

        // Still renting: this frame's wrap, if the shared texture has landed already.
        if (held.Shared is not { } shared)
        {
            return null;
        }

        try
        {
            if (shared.TryGetWrap(out var wrap, out var error))
            {
                return wrap;
            }

            if (error is not null)
            {
                Fail(entry, part, file, error);
            }
        }
        catch (Exception ex)
        {
            Fail(entry, part, file, ex);
        }

        return null;
    }

    /// <summary>A part that could not load: logged once, not asked for again, and Medallion stands in.</summary>
    private static void Fail(SetEntry entry, AtlasPart part, string file, Exception? error)
    {
        entry.Missing[(int)part] = true;
        Let(ref entry.Parts[(int)part]);
        entry.WarnOnce($"{file} could not be loaded ({error?.Message ?? "cancelled"})");
    }

    /// <summary>Drops a part: its wrap is disposed after <see cref="RetireFrames"/>, a rent still running disposes its result when it lands.</summary>
    private static void Let(ref HeldTexture held)
    {
        if (held.Wrap is { } wrap)
        {
            Retired.Add((wrap, frame));
        }

        if (held.Rent is { } rent)
        {
            _ = rent.ToContentDisposedTask(true);
        }

        held = default;
    }

    /// <summary>Disposes the textures released more than <see cref="RetireFrames"/> frames ago.</summary>
    private static void FlushRetired()
    {
        for (var i = Retired.Count - 1; i >= 0; i--)
        {
            if (frame - Retired[i].Frame > RetireFrames)
            {
                DisposeWrap(Retired[i].Wrap);
                Retired.RemoveAt(i);
            }
        }
    }

    private static void ReleaseAll()
    {
        for (var s = 0; s < Slots; s++)
        {
            if (Entries[s] is not { } entry)
            {
                continue;
            }

            for (var p = AtlasPart.Row; p <= AtlasPart.Plain2x; p++)
            {
                Let(ref entry.Parts[(int)p]);
                Residency.Released(entry.Set, p);
            }
        }

        foreach (var (wrap, _) in Retired)
        {
            DisposeWrap(wrap);
        }

        Retired.Clear();
        Array.Clear(Entries);
    }

    private static void DisposeWrap(IDalamudTextureWrap wrap)
    {
        try
        {
            wrap.Dispose();
        }
        catch (Exception ex)
        {
            Plugin.Log?.Warning(ex, "Theme atlas: a texture could not be disposed");
        }
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
                Let(ref entry.Parts[(int)p]);
                Residency.Released(set, p);
            }
        }
    }

    /// <summary>A part's texture: the shared handle, the rent in flight, then the rented wrap the plugin owns until released.</summary>
    private struct HeldTexture
    {
        public ISharedImmediateTexture? Shared;
        public Task<IDalamudTextureWrap>? Rent;
        public IDalamudTextureWrap? Wrap;
    }

    /// <summary>A set's parsed layouts: null for a file the set does not ship or that did not parse.</summary>
    private sealed record SetLayouts(HeroAtlasLayout? Medals, HeroAtlasLayout? Plain, RowStripLayout? Row);

    private sealed class SetEntry(GlyphSetId set, string directory)
    {
        private SetLayouts? layouts;
        private int warned;

        public GlyphSetId Set { get; } = set;

        public string Directory { get; } = directory;

        public HeldTexture[] Parts { get; } = new HeldTexture[(int)AtlasPart.Plain2x + 1];

        /// <summary>Parts whose PNG was not on disk when first asked for, or that failed to load (checked once, not every frame).</summary>
        public bool[] Missing { get; } = new bool[(int)AtlasPart.Plain2x + 1];

        /// <summary>The layouts once read; null while they load.</summary>
        public SetLayouts? Layouts => Volatile.Read(ref layouts);

        public void StartLoad() => Task.Run(() =>
        {
            SetLayouts read;
            try
            {
                read = new SetLayouts(ReadHero("medals.json"), ReadHero("plain.json"), ReadRow("row.json"));
            }
            catch (Exception ex)
            {
                // Anything unexpected is logged once; the set then draws as Medallion rather than loading forever.
                WarnOnce($"its layouts could not be loaded ({ex.Message})");
                read = new SetLayouts(null, null, null);
            }

            Volatile.Write(ref layouts, read);
        });

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
