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
/// The theme sets' and frame kits' atlases at run time (feature plan v7 T5 and T11; theme-system §6.3;
/// docs/design/v7/themes/ATLAS-CONTRACT.md), generalising <see cref="MedalAtlas"/> to atlases loaded from disk:
/// <list type="bullet">
/// <item>Only what the appearance draws loads. A set's or kit's layouts (<c>medals.json</c>, <c>plain.json</c>,
/// <c>row.json</c>, <c>faces*.json</c>, <c>frames*.json</c>) are read and checked off the UI thread the first time it is
/// wanted or drawn; a malformed or missing file just leaves that part out (logged once).</item>
/// <item>A row strip (a set's <c>row</c>, a composed set's <c>faces-row</c>, a kit's <c>frames-row</c>) is requested as
/// soon as the appearance draws it; a hero atlas on its first hero draw; a 2x atlas only above the largest 1x tier (while
/// it loads, the 1x atlas's largest tier stands in).</item>
/// <item>A requested part is rented (<see cref="ISharedImmediateTexture.RentAsync"/>), so Dalamud's shared cache cannot let
/// it go while the plugin holds it: a set's medals never drop back to a stand-in after a spell off-screen.</item>
/// <item>A part that has not drawn for <see cref="AtlasResidency.IdleSeconds"/> is released unless it is a 1x part the
/// saved appearance draws (<see cref="AtlasResidency"/>). Its texture is disposed a frame later, never in a frame that
/// could still draw it; everything is disposed on unload (<see cref="Dispose()"/>).</item>
/// </list>
/// Until a part is ready <see cref="TryDraw"/> and <see cref="TryCompose"/> return false and the seam draws Medallion's
/// procedural medal instead.
/// </summary>
internal static class ThemeAtlasCache
{
    private const int Slots = 8;

    /// <summary>Frames a released texture is kept before it is disposed, so no draw list still holds it.</summary>
    private const int RetireFrames = 1;

    private const int PartCount = (int)AtlasPart.Frames2x + 1;

    private static readonly AtlasResidency Residency = new();
    private static readonly Entry?[] Sets = new Entry?[Slots];
    private static readonly Entry?[] Kits = new Entry?[Slots];
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
    /// Once per frame, with the saved appearance: marks what it draws as wanted, requests its row strips, and releases
    /// parts left idle; disposes the textures released a frame ago. Allocation-free once the layouts have loaded.
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
            var set = sets[i].Id;
            if (Residency.IsWanted(set) && EnsureSet(set) is { Layouts: { } layouts } entry)
            {
                if (layouts.Row is not null && Residency.ShouldPreload(set, AtlasPart.Row))
                {
                    Texture(entry, AtlasPart.Row, now);
                }

                if (layouts.FacesRow is not null && Residency.ShouldPreload(set, AtlasPart.FacesRow))
                {
                    Texture(entry, AtlasPart.FacesRow, now);
                }
            }

            ReleaseIdle(Sets[(int)set & (Slots - 1)], now);
        }

        var kits = FrameKits.All;
        for (var i = 0; i < kits.Count; i++)
        {
            var kit = kits[i].Id;
            if (Residency.IsWanted(kit) && EnsureKit(kit) is { Layouts.FramesRow: not null } entry && Residency.ShouldPreload(kit, AtlasPart.FramesRow))
            {
                Texture(entry, AtlasPart.FramesRow, now);
            }

            ReleaseIdle(Kits[(int)kit & (Slots - 1)], now);
        }
    }

    /// <summary>Whether <paramref name="set"/> has a hero atlas on disk that parsed (false while its layouts load).</summary>
    public static bool IsDrawable(GlyphSetId set) => EnsureSet(set)?.Layouts?.Medals is not null;

    /// <summary>
    /// Draws <paramref name="state"/>'s medal of <paramref name="set"/> as the set ships it into the whole-pixel box
    /// <paramref name="min"/> .. <paramref name="min"/> + <paramref name="size"/> at <paramref name="finish"/>, tinted;
    /// <paramref name="hero"/> says whether a hero cell drew (one with a badge seat). False, with nothing drawn, when the
    /// part it needs is not ready.
    /// </summary>
    public static bool TryDraw(ImDrawListPtr dl, GlyphSetId set, QuestState state, JobSeat seat, Vector2 min, float size, MedalFinish finish, uint tint, out bool hero)
    {
        hero = false;
        if (EnsureSet(set) is not { Layouts: { } layouts } entry)
        {
            return false;
        }

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

    /// <summary>
    /// Composes <paramref name="state"/>'s medal from <paramref name="set"/>'s unframed faces in <paramref name="kit"/>
    /// (feature plan v7 T11; theme-system §6.1; ATLAS-CONTRACT §7) into the whole-pixel box <paramref name="min"/> ..
    /// <paramref name="min"/> + <paramref name="size"/>: the face's under layer, the kit's frame for the state's urgency
    /// tier (its Quiet hairline at <see cref="MedalFinish.LightRim"/>), the face's over layer, then from 32 px the kit's
    /// badge (<paramref name="seat"/>'s empty seat on Ready on another job). <paramref name="hero"/> says whether a hero
    /// cell drew, so the caller puts the job icon in the seat. False, with nothing drawn, until every part it needs is
    /// ready; Medallion stands in meanwhile. Plain draws no frames, so it is never composed.
    /// </summary>
    public static bool TryCompose(ImDrawListPtr dl, GlyphSetId set, FrameKitId kit, QuestState state, JobSeat seat, Vector2 min, float size, MedalFinish finish, uint tint, out bool hero)
    {
        hero = false;
        if (finish is not (MedalFinish.Gilt or MedalFinish.LightRim) || !(size > 0f)
            || EnsureSet(set) is not { Layouts: { } faceLayouts } faceEntry
            || EnsureKit(kit) is not { Layouts: { } kitLayouts } kitEntry)
        {
            return false;
        }

        var heroSize = size >= MedalLayout.RowTierMaxPx;
        var faces = heroSize ? faceLayouts.Faces : faceLayouts.FacesRow;
        var frames = heroSize ? kitLayouts.Frames : kitLayouts.FramesRow;
        if (faces is null || frames is null)
        {
            return false;
        }

        var now = Now;
        var (cell, twoX) = faces.Pick(size);
        IDalamudTextureWrap? faceWrap, frameWrap;
        if (!heroSize)
        {
            faceWrap = Texture(faceEntry, AtlasPart.FacesRow, now);
            frameWrap = Texture(kitEntry, AtlasPart.FramesRow, now);
        }
        else
        {
            faceWrap = Texture(faceEntry, twoX ? AtlasPart.Faces2x : AtlasPart.Faces, now);
            frameWrap = Texture(kitEntry, twoX ? AtlasPart.Frames2x : AtlasPart.Frames, now);
            if (twoX && (faceWrap is null || frameWrap is null))
            {
                // Both from the 1x atlases' largest tier while either 2x texture loads, so face and frame always match.
                faceWrap = Texture(faceEntry, AtlasPart.Faces, now);
                frameWrap = Texture(kitEntry, AtlasPart.Frames, now);
                cell = faces.Cells[^1];
            }
        }

        var under = FrameParts.Face(state, over: false);
        var rim = FrameParts.Frame(state, quiet: finish == MedalFinish.LightRim);
        if (faceWrap is null || frameWrap is null || !faces.TryRect(under, cell, out var underRect) || !frames.TryRect(rim, cell, out var rimRect))
        {
            return false;
        }

        Part(dl, faceWrap, faces, under, underRect, min, size, tint);
        Part(dl, frameWrap, frames, rim, rimRect, min, size, tint);
        var over = FrameParts.Face(state, over: true);
        if (faces.TryRect(over, cell, out var overRect))
        {
            Part(dl, faceWrap, faces, over, overRect, min, size, tint);
        }

        if (heroSize)
        {
            var badge = FrameParts.Badge(state, seat);
            if (badge >= 0 && frames.TryRect(badge, cell, out var badgeRect))
            {
                Part(dl, frameWrap, frames, badge, badgeRect, min, size, tint);
            }

            hero = true;
        }

        return true;
    }

    /// <summary>One part at its box of the medal's 128-unit box.</summary>
    private static void Part(ImDrawListPtr dl, IDalamudTextureWrap wrap, PartAtlasLayout layout, int sprite, AtlasRect rect, Vector2 min, float size, uint tint)
    {
        var (x, y, w, h) = FrameParts.Place(layout.Box(sprite), min.X, min.Y, size);
        var (u0, v0, u1, v1) = layout.Uv(rect);
        dl.AddImage(wrap.Handle, new Vector2(x, y), new Vector2(x + w, y + h), new Vector2(u0, v0), new Vector2(u1, v1), tint);
    }

    /// <summary>Seconds on a steady clock, for the residency rules.</summary>
    private static double Now => Environment.TickCount64 / 1000.0;

    /// <summary>
    /// The entry of <paramref name="set"/>: an atlas set, or Menphina's Medallion, whose faces are on disk for the frames
    /// axis (its own medals stay embedded and procedural).
    /// </summary>
    private static Entry? EnsureSet(GlyphSetId set)
    {
        var slot = (int)set;
        if ((uint)slot >= Slots || GlyphSets.Get(set) is not { } info || info.Id != set || (info.Kind != GlyphRenderKind.Atlas && set != GlyphSetId.Medallion))
        {
            return null;
        }

        return Sets[slot] ?? Start(ref Sets[slot], static (directory, id) => new Entry(directory, (GlyphSetId)id, null), slot);
    }

    private static Entry? EnsureKit(FrameKitId kit)
    {
        var slot = (int)kit;
        if ((uint)slot >= Slots || FrameKits.Get(kit) is not { } info || info.Id != kit)
        {
            return null;
        }

        return Kits[slot] ?? Start(ref Kits[slot], static (directory, id) => new Entry(directory, null, (FrameKitId)id), slot);
    }

    private static Entry? Start(ref Entry? slot, Func<string, int, Entry> create, int id)
    {
        if (PluginDirectory() is not { } directory)
        {
            return null;
        }

        var entry = create(directory, id);
        slot = entry;
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
    /// The texture of <paramref name="part"/>, requested and rented on first use; null while it loads, when the entry has
    /// no such file, or when it failed to load (logged once).
    /// </summary>
    private static IDalamudTextureWrap? Texture(Entry entry, AtlasPart part, double now)
    {
        var file = entry.FileOf(part);
        if (file is null || (provider ?? Plugin.TextureProvider) is not { } textures)
        {
            return null;
        }

        entry.Touch(Residency, part, now);
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
            var path = Path.Combine(entry.Directory, entry.RelativePath(file));
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
    private static void Fail(Entry entry, AtlasPart part, string file, Exception? error)
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
        foreach (var entries in new[] { Sets, Kits })
        {
            foreach (var entry in entries)
            {
                if (entry is null)
                {
                    continue;
                }

                for (var p = 0; p < PartCount; p++)
                {
                    Let(ref entry.Parts[p]);
                    entry.Released(Residency, (AtlasPart)p);
                }
            }

            Array.Clear(entries);
        }

        foreach (var (wrap, _) in Retired)
        {
            DisposeWrap(wrap);
        }

        Retired.Clear();
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

    private static void ReleaseIdle(Entry? entry, double now)
    {
        if (entry is null)
        {
            return;
        }

        for (var p = 0; p < PartCount; p++)
        {
            if (entry.ShouldRelease(Residency, (AtlasPart)p, now))
            {
                Let(ref entry.Parts[p]);
                entry.Released(Residency, (AtlasPart)p);
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

    /// <summary>An entry's parsed layouts: null for a file it does not ship or that did not parse.</summary>
    private sealed record Layouts(
        HeroAtlasLayout? Medals,
        HeroAtlasLayout? Plain,
        RowStripLayout? Row,
        PartAtlasLayout? Faces,
        PartAtlasLayout? FacesRow,
        PartAtlasLayout? Frames,
        PartAtlasLayout? FramesRow);

    /// <summary>A glyph set's atlases (<paramref name="set"/>) or a frame kit's (<paramref name="kit"/>).</summary>
    private sealed class Entry(string directory, GlyphSetId? set, FrameKitId? kit)
    {
        private Layouts? layouts;
        private int warned;

        public string Directory { get; } = directory;

        public HeldTexture[] Parts { get; } = new HeldTexture[PartCount];

        /// <summary>Parts whose PNG was not on disk when first asked for, or that failed to load (checked once, not every frame).</summary>
        public bool[] Missing { get; } = new bool[PartCount];

        /// <summary>The layouts once read; null while they load.</summary>
        public Layouts? Layouts => Volatile.Read(ref layouts);

        public string RelativePath(string file) => kit is { } k ? ThemeAtlasRules.RelativePath(k, file) : ThemeAtlasRules.RelativePath(set!.Value, file);

        /// <summary>The PNG of <paramref name="part"/>, when its layout parsed; null otherwise (or for a part of the other kind).</summary>
        public string? FileOf(AtlasPart part)
        {
            var l = Layouts;
            return part switch
            {
                AtlasPart.Row when l?.Row is not null => "row.png",
                AtlasPart.Medals when l?.Medals is not null => "medals.png",
                AtlasPart.Medals2x when l?.Medals is not null => "medals@2x.png",
                AtlasPart.Plain when l?.Plain is not null => "plain.png",
                AtlasPart.Plain2x when l?.Plain is not null => "plain@2x.png",
                AtlasPart.FacesRow when l?.FacesRow is not null => "faces-row.png",
                AtlasPart.Faces when l?.Faces is not null => "faces.png",
                AtlasPart.Faces2x when l?.Faces is not null => "faces@2x.png",
                AtlasPart.FramesRow when l?.FramesRow is not null => "frames-row.png",
                AtlasPart.Frames when l?.Frames is not null => "frames.png",
                AtlasPart.Frames2x when l?.Frames is not null => "frames@2x.png",
                _ => null,
            };
        }

        public void Touch(AtlasResidency residency, AtlasPart part, double now)
        {
            if (kit is { } k)
            {
                residency.Touch(k, part, now);
            }
            else
            {
                residency.Touch(set!.Value, part, now);
            }
        }

        public bool ShouldRelease(AtlasResidency residency, AtlasPart part, double now) =>
            kit is { } k ? residency.ShouldRelease(k, part, now) : residency.ShouldRelease(set!.Value, part, now);

        public void Released(AtlasResidency residency, AtlasPart part)
        {
            if (kit is { } k)
            {
                residency.Released(k, part);
            }
            else
            {
                residency.Released(set!.Value, part);
            }
        }

        public void StartLoad() => Task.Run(() =>
        {
            Layouts read;
            try
            {
                read = kit is not null
                    ? new Layouts(null, null, null, null, null, ReadParts("frames.json", PartAtlasKind.Frames), ReadParts("frames-row.json", PartAtlasKind.Frames))
                    : new Layouts(
                        ReadHero("medals.json"),
                        ReadHero("plain.json"),
                        ReadRow("row.json"),
                        ReadParts("faces.json", PartAtlasKind.Faces),
                        ReadParts("faces-row.json", PartAtlasKind.Faces),
                        null,
                        null);
            }
            catch (Exception ex)
            {
                // Anything unexpected is logged once; the entry then draws nothing rather than loading forever.
                WarnOnce($"its layouts could not be loaded ({ex.Message})");
                read = new Layouts(null, null, null, null, null, null, null);
            }

            Volatile.Write(ref layouts, read);
        });

        public void WarnOnce(string message)
        {
            if (Interlocked.Exchange(ref warned, 1) == 0)
            {
                var name = kit is { } k ? $"Frame kit {FrameKits.Get(k).Key}" : $"Theme set {GlyphSets.Get(set!.Value).Key}";
                Plugin.Log?.Warning("{Name}: {Message}; Menphina's Medallion stands in", name, message);
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

        private PartAtlasLayout? ReadParts(string file, PartAtlasKind kind)
        {
            var text = Read(file);
            if (text is null)
            {
                return null;
            }

            if (PartAtlasLayout.TryParse(text, kind, out var layout, out var error))
            {
                return layout;
            }

            WarnOnce($"{file} is not a valid layout ({error})");
            return null;
        }

        private string? Read(string file)
        {
            var path = Path.Combine(Directory, RelativePath(file));
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
