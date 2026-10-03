using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Developer window for the glyphs, opened with <c>/tsukimichi glyphs</c> (feature plan v6 G3: the owner checks it in
/// game before the new medals merge). Sizes are device pixels, not logical sizes, because the row tier, the atlas tiers
/// and the gauges' gates are pixel rules. Four tabs:
/// <list type="bullet">
/// <item><b>A/B sheet</b>: every state from 5 to 32 px, the 1.11 moons (A, <see cref="LegacyMoonGlyph"/>) beside the 1.12
/// medals (B, <see cref="MedalGlyph"/>), then a quest-list mock at 16 and 20 px rows with each medal's badge content at
/// text height, and the three palettes side by side.</item>
/// <item><b>Hero</b>: the medals from 32 to 128 px from the atlas and as vector meshes, and the job badge for a job of
/// each role.</item>
/// <item><b>Gauges</b>: the halo gauge, the filling moon and the orbit ring, A beside B (G5).</item>
/// <item><b>Icon</b>: the plugin icon at the installer's sizes on Night, Dalamud grey and white.</item>
/// </list>
/// The "Simulate" combo re-colours the whole window through a colour-vision simulation (greyscale, deuteranopia,
/// protanopia, tritanopia: <see cref="ColorVisionSimulation"/>, Machado 2009 in linear sRGB; greyscale is WCAG relative
/// luminance). Vector glyphs are simulated by rewriting the draw list's vertex colours after the panel is drawn; the
/// atlas medals are drawn from a simulated copy of the atlas made once per mode (read back from the GPU, so it is ready a
/// moment after the mode is picked). The game's job icons stay as they are. <see cref="Theme"/> and the renderers stay
/// untouched.
/// </summary>
public sealed class GlyphDebugWindow : Window, IDisposable
{
    private static readonly string[] SimulationNames = Array.ConvertAll(ColorVisionSimulation.All, ColorVisionSimulation.Name);

    /// <summary>What the panels draw with: the palette in effect (Settings), or one forced.</summary>
    private static readonly string[] PaletteNames = ["Settings", "Standard", "High contrast", "High contrast (light host)"];

    /// <summary>The palettes compared side by side, and the host colour each is shown on (null: the panel's own).</summary>
    private static readonly (GlyphPalette Palette, string Label, Vector4? Host)[] Compared =
    [
        (GlyphPalette.Standard, "Standard", null),
        (GlyphPalette.HighContrastDark, "High contrast", null),
        (GlyphPalette.HighContrastLight, "High contrast · light host", new Vector4(1f, 1f, 1f, 1f)),
    ];

    /// <summary>The states in the Help legend's order.</summary>
    private static readonly QuestState[] States =
    [
        QuestState.Ready, QuestState.ReadyOnOtherJob, QuestState.Accepted, QuestState.Blocked,
        QuestState.DoneThisCycle, QuestState.Completed, QuestState.Foreclosed, QuestState.Unknown,
    ];

    /// <summary>The A/B sheet's sizes: the medal's box in device px, 5 to 32.</summary>
    private static readonly float[] SheetSizes = [5f, 6f, 8f, 10f, 12f, 14f, 16f, 18f, 20f, 24f, 28f, 32f];

    /// <summary>The hero sizes: the row tier's end, each atlas tier and the 2x atlas's first.</summary>
    private static readonly float[] HeroSizes = [32f, 40f, 48f, 64f, 96f, 128f];

    /// <summary>A job of each role for the badge (ClassJob rows): Paladin, White Mage, Bard, Black Mage, Carpenter, Miner.</summary>
    private static readonly (byte Job, string Name)[] BadgeJobs = [(19, "Paladin"), (24, "White Mage"), (23, "Bard"), (25, "Black Mage"), (8, "Carpenter"), (16, "Miner")];

    /// <summary>Progress fractions: the ends, the first sliver, both sides of the floor and both sides of the half.</summary>
    private static readonly float[] Fractions = [0f, 0.03f, 0.10f, 0.25f, 0.5f, 0.75f, 0.9f, 0.97f, 1f];

    /// <summary>Halo boxes in device pixels (R = box / 2): number only, track + arc, the tree floor, the tooltip and the help size.</summary>
    private static readonly float[] HaloBoxes = [12f, 16f, 24f, 32f, 64f];

    /// <summary>The installer's icon sizes, and the share of the 512 master Dalamud's "Installed" check covers (x 248, y 312 on).</summary>
    private static readonly float[] IconSizes = [16f, 32f, 64f, 128f, 256f];

    private static readonly Vector2 InstalledCorner = new(248f / 512f, 312f / 512f);

    private readonly Dictionary<uint, uint>[] simulated = Array.ConvertAll(ColorVisionSimulation.All, static _ => new Dictionary<uint, uint>());
    private readonly IDalamudTextureWrap?[] simulatedAtlas = new IDalamudTextureWrap?[ColorVisionSimulation.All.Length];
    private readonly Task?[] simulating = new Task?[ColorVisionSimulation.All.Length];

    private bool nightPanel = true;
    private bool haloOnCard;
    private bool showInstalled = true;
    private int simulation;
    private int palette;
    private float heroScale = 1f;
    private bool disposed;

    public GlyphDebugWindow()
        : base("Tsukimichi Glyphs###TsukimichiGlyphs")
    {
        Size = new Vector2(980f, 860f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420f, 320f) };
    }

    /// <summary>Starts the interactive tutorial (the same action the help window and Settings use); null hides the button.</summary>
    public Action? StartTutorial { get; set; }

    private ColorVision Mode => (uint)simulation < (uint)ColorVisionSimulation.All.Length ? ColorVisionSimulation.All[simulation] : ColorVision.None;

    public override void Draw()
    {
        ImGui.Checkbox("Night panel", ref nightPanel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("Simulate", ref simulation, SimulationNames);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(190f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("Palette", ref palette, PaletteNames);
        ImGui.SameLine();
        ImGui.TextDisabled($"scale {ImGuiHelpers.GlobalScale:0.00} · sizes in device px · atlas {(MedalAtlas.IsReady ? "loaded" : "loading")}");
        if (StartTutorial is { } startTutorial)
        {
            ImGui.SameLine();
            if (ImGui.Button("Tutorial preview"))
            {
                startTutorial();
            }
        }

        using var colors = Theme.PushNightPanel(nightPanel);
        using var tabs = ImRaii.TabBar("##glyphTabs");
        if (!tabs)
        {
            return;
        }

        Tab("A/B sheet", DrawSheet);
        Tab("Hero", DrawHero);
        Tab("Gauges", DrawGauges);
        Tab("Icon", DrawIcon);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        for (var i = 0; i < simulatedAtlas.Length; i++)
        {
            simulatedAtlas[i]?.Dispose();
            simulatedAtlas[i] = null;
        }
    }

    /// <summary>One tab: a child panel drawn with the chosen palette, its vertex colours simulated afterwards.</summary>
    private void Tab(string label, Action draw)
    {
        using var tab = ImRaii.TabItem(label);
        if (!tab)
        {
            return;
        }

        using var panel = ImRaii.Child("##glyphPanel" + label, new Vector2(-1f, -1f), true, ImGuiWindowFlags.HorizontalScrollbar);
        if (!panel)
        {
            return;
        }

        using (Theme.PushGlyphs(Palette()))
        {
            draw();
        }

        // Everything the child drew this frame, background included, is in its own draw list.
        var dl = ImGui.GetWindowDrawList();
        ApplySimulation(dl, Mode, 0, dl.VtxBuffer.Size);
    }

    private GlyphPalette Palette() => palette switch
    {
        1 => GlyphPalette.Standard,
        2 => GlyphPalette.HighContrastDark,
        3 => GlyphPalette.HighContrastLight,
        _ => Theme.Glyphs,
    };

    // ------------------------------------------------------------------ A/B sheet

    private void DrawSheet()
    {
        ImGui.TextUnformatted("A: 1.11 moons · B: 1.12 medals (Menphina's Medallion). Each size is the glyph's box; the row tier ends at 32 px.");
        var scale = ImGuiHelpers.GlobalScale;
        var line = ImGui.GetTextLineHeight();
        var labelWidth = 150f * scale;
        var gap = 4f * scale;
        var rowHeight = SheetSizes[^1] + 6f * scale;
        var dl = ImGui.GetWindowDrawList();

        var start = ImGui.GetCursorScreenPos();
        var groupWidth = 0f;
        foreach (var s in SheetSizes)
        {
            groupWidth += s + gap;
        }

        // Header: the sizes over each group.
        ImGui.Dummy(new Vector2(labelWidth + 2f * groupWidth + 40f * scale, line * 2f));
        for (var g = 0; g < 2; g++)
        {
            var x = start.X + labelWidth + g * (groupWidth + 30f * scale);
            dl.AddText(new Vector2(x, start.Y), ImGui.GetColorU32(ImGuiCol.Text), g == 0 ? "A · 1.11" : "B · 1.12");
            foreach (var s in SheetSizes)
            {
                dl.AddText(new Vector2(x, start.Y + line), ImGui.GetColorU32(ImGuiCol.TextDisabled), $"{s:0}");
                x += s + gap;
            }
        }

        foreach (var state in States)
        {
            var row = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(labelWidth + 2f * groupWidth + 40f * scale, rowHeight));
            var midY = row.Y + rowHeight * 0.5f;
            dl.AddText(new Vector2(row.X, midY - line * 0.5f), Theme.StateColorU32(state), Strings.StateName(state));
            for (var g = 0; g < 2; g++)
            {
                var x = row.X + labelWidth + g * (groupWidth + 30f * scale);
                foreach (var s in SheetSizes)
                {
                    var center = new Vector2(x + s * 0.5f, midY);
                    if (g == 0)
                    {
                        LegacyMoonGlyph.Draw(dl, center, s * 0.5f, state);
                    }
                    else
                    {
                        DrawMedal(dl, center, s * 0.5f, state, BadgeJobs[0].Job);
                    }

                    x += s + gap;
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Quest list mock: the row-size medal with its badge content at text height (the row fallback)");
        foreach (var rowSize in new[] { 16f, 20f })
        {
            ImGui.TextDisabled($"{rowSize:0} px rows");
            foreach (var state in States)
            {
                var pos = ImGui.GetCursorScreenPos();
                var height = MathF.Max(rowSize, line);
                ImGui.Dummy(new Vector2(rowSize * 2f + gap * 3f, height));
                var midY = pos.Y + height * 0.5f;
                DrawMedal(dl, new Vector2(pos.X + rowSize * 0.5f, midY), rowSize * 0.5f, state, BadgeJobs[0].Job);
                MedalGlyph.DrawRowBadge(dl, new Vector2(pos.X + rowSize + gap, midY - line * 0.5f), line, state, BadgeJobs[0].Job);
                ImGui.SameLine();
                ImGui.TextUnformatted(Strings.StateName(state));
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        DrawComparison();
    }

    /// <summary>
    /// The three palettes side by side under every simulation: one row per <see cref="ColorVision"/> holding the eight
    /// medals at 20 px, each row re-coloured through its own simulation (the panel-wide combo re-colours on top).
    /// </summary>
    private void DrawComparison()
    {
        ImGui.TextUnformatted("Palettes side by side at 20 px (each row under its own simulation)");
        var scale = ImGuiHelpers.GlobalScale;
        const float size = 20f;
        var gap = 4f * scale;
        var labelWidth = 110f * scale;
        var rowHeight = size + 8f * scale;
        var blockWidth = States.Length * (size + gap) + 16f * scale;
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();

        var header = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(labelWidth + Compared.Length * blockWidth, line));
        for (var b = 0; b < Compared.Length; b++)
        {
            dl.AddText(new Vector2(header.X + labelWidth + b * blockWidth, header.Y), ImGui.GetColorU32(ImGuiCol.Text), Compared[b].Label);
        }

        foreach (var mode in ColorVisionSimulation.All)
        {
            var rowStart = dl.VtxBuffer.Size;
            var row = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(labelWidth + Compared.Length * blockWidth, rowHeight));
            dl.AddText(new Vector2(row.X, row.Y + (rowHeight - line) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), ColorVisionSimulation.Name(mode));
            for (var b = 0; b < Compared.Length; b++)
            {
                var (glyphs, _, host) = Compared[b];
                var x = row.X + labelWidth + b * blockWidth;
                if (host is { } hostColor)
                {
                    dl.AddRectFilled(new Vector2(x - gap, row.Y), new Vector2(x + blockWidth - 12f * scale, row.Y + rowHeight), Theme.U32(hostColor), 4f * scale);
                }

                using var pushed = Theme.PushGlyphs(glyphs);
                foreach (var state in States)
                {
                    MedalGlyph.Draw(dl, new Vector2(x + size * 0.5f, row.Y + rowHeight * 0.5f), size * 0.5f, state);
                    x += size + gap;
                }
            }

            ApplySimulation(dl, mode, rowStart, dl.VtxBuffer.Size);
        }
    }

    // ------------------------------------------------------------------ hero sizes

    private void DrawHero()
    {
        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Scale", ref heroScale, 0.5f, 2f, "%.2f×");
        ImGui.SameLine();
        ImGui.TextDisabled("each size: the atlas (left) and the vector medal (right), which high contrast, Plain and loading draw");
        var dl = ImGui.GetWindowDrawList();
        var gap = 6f * ImGuiHelpers.GlobalScale;
        var line = ImGui.GetTextLineHeight();
        foreach (var state in States)
        {
            ImGui.TextUnformatted(Strings.StateName(state));
            var row = ImGui.GetCursorScreenPos();
            var height = HeroSizes[^1] * heroScale + gap;
            var x = row.X;
            foreach (var s0 in HeroSizes)
            {
                var s = MathF.Round(s0 * heroScale);
                foreach (var renderer in new[] { MedalRenderer.Atlas, MedalRenderer.Vector })
                {
                    DrawMedal(dl, new Vector2(x + s * 0.5f, row.Y + height * 0.5f), s * 0.5f, state, BadgeJobs[0].Job, renderer);
                    x += s + gap;
                }

                x += gap * 2f;
            }

            ImGui.Dummy(new Vector2(x - row.X, height));
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Job badges: a job of each role (the icon is the game's, centred optically on the seat)");
        var size = MathF.Round(96f * heroScale);
        var jobs = ImGui.GetCursorScreenPos();
        for (var i = 0; i < BadgeJobs.Length; i++)
        {
            var (job, name) = BadgeJobs[i];
            var x = jobs.X + i * (size + gap * 3f);
            DrawMedal(dl, new Vector2(x + size * 0.5f, jobs.Y + size * 0.5f), size * 0.5f, QuestState.ReadyOnOtherJob, job);
            dl.AddText(new Vector2(x, jobs.Y + size + gap), ImGui.GetColorU32(ImGuiCol.TextDisabled), name);
            MedalGlyph.DrawRowBadge(dl, new Vector2(x, jobs.Y + size + gap + line * 1.2f), line, QuestState.ReadyOnOtherJob, job);
        }

        ImGui.Dummy(new Vector2(BadgeJobs.Length * (size + gap * 3f), size + gap + line * 2.6f));
    }

    // ------------------------------------------------------------------ gauges

    private void DrawGauges()
    {
        ImGui.Checkbox("On a card", ref haloOnCard);
        ImGui.SameLine();
        ImGui.TextDisabled("A: 1.11 · B: 1.12, gilt in a lapis groove round a moonstone moon (high contrast keeps A)");
        var dl = ImGui.GetWindowDrawList();
        var gap = 6f * ImGuiHelpers.GlobalScale;
        foreach (var (label, legacy) in new[] { ("Halo A", true), ("Halo B", false) })
        {
            ImGui.TextUnformatted(label);
            foreach (var box in HaloBoxes)
            {
                var row = ImGui.GetCursorScreenPos();
                var x = row.X;
                foreach (var f in Fractions)
                {
                    var center = new Vector2(x + box * 0.5f, row.Y + box * 0.5f);
                    if (legacy)
                    {
                        LegacyMoonGlyph.DrawHalo(dl, center, box * 0.5f, f, haloOnCard);
                    }
                    else
                    {
                        MoonGlyph.DrawHalo(dl, center, box * 0.5f, f, haloOnCard);
                    }

                    x += box + gap;
                }

                var center1 = new Vector2(x + box * 0.5f, row.Y + box * 0.5f);
                if (legacy)
                {
                    LegacyMoonGlyph.DrawHalo(dl, center1, box * 0.5f, 1f, haloOnCard, dimComplete: true);
                }
                else
                {
                    MoonGlyph.DrawHalo(dl, center1, box * 0.5f, 1f, haloOnCard, dimComplete: true);
                }

                ImGui.Dummy(new Vector2(x + box - row.X, box + gap));
            }
        }

        foreach (var (label, legacy) in new[] { ("Filling moon A", true), ("Filling moon B", false) })
        {
            ImGui.TextUnformatted(label);
            var row = ImGui.GetCursorScreenPos();
            var x = row.X;
            foreach (var radius in new[] { 6f, 9f, 16f })
            {
                foreach (var f in Fractions)
                {
                    var center = new Vector2(x + radius * 1.2f, row.Y + 16f * 1.2f);
                    if (legacy)
                    {
                        LegacyMoonGlyph.DrawFilling(dl, center, radius, f);
                    }
                    else
                    {
                        MoonGlyph.DrawFilling(dl, center, radius, f);
                    }

                    x += radius * 2.4f;
                }

                x += gap * 2f;
            }

            ImGui.Dummy(new Vector2(x - row.X, 16f * 2.4f));
        }

        ImGui.TextUnformatted("Orbit ring (the rail's Journal station and foot gauge)");
        var orbit = ImGui.GetCursorScreenPos();
        var ox = orbit.X;
        foreach (var box in new[] { 28f, 40f, 56f })
        {
            foreach (var f in Fractions)
            {
                Orbit.DrawMoon(dl, new Vector2(ox, orbit.Y), box, f, Theme.Glyphs.HighContrast);
                ox += box + gap;
            }

            ox += gap * 2f;
        }

        ImGui.Dummy(new Vector2(ox - orbit.X, 56f + gap));
    }

    // ------------------------------------------------------------------ the plugin icon

    private void DrawIcon()
    {
        ImGui.Checkbox("Mark Dalamud's \"Installed\" corner", ref showInstalled);
        var textures = Plugin.TextureProvider;
        var directory = Plugin.PluginInterface?.AssemblyLocation.DirectoryName;
        if (textures is null || directory is null)
        {
            ImGui.TextDisabled("No texture provider.");
            return;
        }

        var path = Path.Combine(directory, "images", "icon.png");
        if (!File.Exists(path) || !textures.GetFromFile(path).TryGetWrap(out var wrap, out _))
        {
            ImGui.TextDisabled($"Loading {path}");
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var gap = 10f * ImGuiHelpers.GlobalScale;
        foreach (var (label, ground) in new[] { ("Night", Theme.Night), ("Dalamud grey", new Vector4(0.13f, 0.13f, 0.13f, 1f)), ("White", Vector4.One) })
        {
            ImGui.TextUnformatted(label);
            var row = ImGui.GetCursorScreenPos();
            var height = IconSizes[^1] + 2f * gap;
            var x = row.X;
            var width = gap;
            foreach (var s in IconSizes)
            {
                width += s + gap;
            }

            dl.AddRectFilled(row, row + new Vector2(width, height), Theme.U32(ground), 6f);
            x += gap;
            foreach (var s in IconSizes)
            {
                var min = new Vector2(x, row.Y + gap + (IconSizes[^1] - s));
                dl.AddImage(wrap.Handle, min, min + new Vector2(s));
                if (showInstalled && s >= 64f)
                {
                    dl.AddRect(min + InstalledCorner * s, min + new Vector2(s), Theme.EclipseU32, 0f, ImDrawFlags.None, 1f);
                }

                x += s + gap;
            }

            ImGui.Dummy(new Vector2(width, height + gap));
        }
    }

    // ------------------------------------------------------------------ drawing with the simulated atlas

    /// <summary>
    /// A medal as <see cref="MedalGlyph.Draw"/> draws it, except that under a simulation an atlas medal is drawn from
    /// the simulated atlas (vertex colours cannot re-colour a texture). <paramref name="renderer"/> forces the atlas or
    /// the vector medal.
    /// </summary>
    private void DrawMedal(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job, MedalRenderer renderer = MedalRenderer.Auto)
    {
        var (min, size) = MedalGlyph.Box(center, radius);
        var tokens = MedalTokens.For(Theme.Glyphs);
        var atlas = renderer switch
        {
            MedalRenderer.Vector => false,
            MedalRenderer.Atlas => !tokens.Flat,
            _ => size >= MedalLayout.RowTierMaxPx && !tokens.Flat && Theme.Flair != Flair.Plain,
        };

        if (atlas && Mode != ColorVision.None)
        {
            if (SimulatedAtlas(Mode) is { } wrap)
            {
                var seat = state == QuestState.ReadyOnOtherJob ? JobBadges.Seat(job) : JobSeat.Hand;
                var (u0, v0, u1, v1) = MedalLayout.Uv(MedalLayout.Rect(MedalLayout.For(state, seat), MedalLayout.Pick(MathF.Min(size, MedalLayout.Tiers[^1])).Tier));
                dl.AddImage(wrap.Handle, min, min + new Vector2(size), new Vector2(u0, v0), new Vector2(u1, v1));
                if (state == QuestState.ReadyOnOtherJob)
                {
                    var k = size / 128f;
                    var half = MedalArt.JobIconSlot * 0.5f * k;
                    var seatCenter = min + MedalArt.BadgeCenter * k;
                    JobBadges.TryDraw(dl, job, seatCenter - new Vector2(half), seatCenter + new Vector2(half));
                }

                return;
            }

            // Still simulating: the vector medal, which the vertex pass does simulate.
            renderer = MedalRenderer.Vector;
        }

        var previous = MedalGlyph.Renderer;
        MedalGlyph.Renderer = renderer;
        try
        {
            MedalGlyph.Draw(dl, center, radius, state, job);
        }
        finally
        {
            MedalGlyph.Renderer = previous;
        }
    }

    /// <summary>The 1x atlas re-coloured through <paramref name="mode"/>, made once in the background; null until ready.</summary>
    private IDalamudTextureWrap? SimulatedAtlas(ColorVision mode)
    {
        var i = Array.IndexOf(ColorVisionSimulation.All, mode);
        if (i < 0 || disposed)
        {
            return null;
        }

        if (simulatedAtlas[i] is { } done)
        {
            return done;
        }

        if (simulating[i] is null && MedalAtlas.Wrap(twoX: false) is { } source && Plugin.TextureReadback is { } readback && Plugin.TextureProvider is { } textures)
        {
            simulating[i] = Task.Run(async () =>
            {
                try
                {
                    var (spec, raw) = await readback.GetRawImageAsync(source, default, leaveWrapOpen: true);

                    // DXGI_FORMAT_B8G8R8A8_UNORM is 87; anything else here is RGBA.
                    var bgra = spec.DxgiFormat == 87;
                    for (var y = 0; y < spec.Height; y++)
                    {
                        for (var x = 0; x < spec.Width; x++)
                        {
                            var o = y * spec.Pitch + x * 4;
                            var (ri, bi) = bgra ? (o + 2, o) : (o, o + 2);
                            var c = ColorVisionSimulation.Simulate(new Vector4(raw[ri] / 255f, raw[o + 1] / 255f, raw[bi] / 255f, 1f), mode);
                            raw[ri] = (byte)Math.Clamp((int)MathF.Round(c.X * 255f), 0, 255);
                            raw[o + 1] = (byte)Math.Clamp((int)MathF.Round(c.Y * 255f), 0, 255);
                            raw[bi] = (byte)Math.Clamp((int)MathF.Round(c.Z * 255f), 0, 255);
                        }
                    }

                    var wrap = await textures.CreateFromRawAsync(spec, raw, $"Tsukimichi medals ({mode})");
                    if (disposed)
                    {
                        wrap.Dispose();
                        return;
                    }

                    simulatedAtlas[i] = wrap;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.Debug(ex, $"Glyph window: could not simulate the medal atlas ({mode})");
                }
            });
        }

        return null;
    }

    // ------------------------------------------------------------------ colour-vision simulation of vertex colours

    /// <summary>
    /// Rewrites the vertex colours <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive) of
    /// <paramref name="dl"/> through <paramref name="mode"/> (<see cref="ColorVisionSimulation"/>).
    /// </summary>
    private void ApplySimulation(ImDrawListPtr dl, ColorVision mode, int from, int to)
    {
        var index = Array.IndexOf(ColorVisionSimulation.All, mode);
        if (mode == ColorVision.None || index < 0)
        {
            return;
        }

        var cache = simulated[index];
        var vertices = dl.VtxBuffer;
        to = Math.Min(to, vertices.Size);
        for (var i = Math.Max(0, from); i < to; i++)
        {
            var vertex = vertices[i];
            vertex.Col = SimulatePacked(cache, vertex.Col, mode);
            vertices[i] = vertex;
        }
    }

    private static uint SimulatePacked(Dictionary<uint, uint> cache, uint packed, ColorVision mode)
    {
        if (cache.TryGetValue(packed, out var cached))
        {
            return cached;
        }

        var result = Pack(ColorVisionSimulation.Simulate(Unpack(packed), mode));
        cache[packed] = result;
        return result;
    }

    private static int Channel(float value) => Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    /// <summary>IM_COL32 layout (0xAABBGGRR), the same packing <see cref="Theme"/> uses.</summary>
    private static Vector4 Unpack(uint packed) => new(
        (packed & 0xFF) / 255f,
        ((packed >> 8) & 0xFF) / 255f,
        ((packed >> 16) & 0xFF) / 255f,
        ((packed >> 24) & 0xFF) / 255f);

    private static uint Pack(Vector4 c) =>
        (uint)Channel(c.X) | ((uint)Channel(c.Y) << 8) | ((uint)Channel(c.Z) << 16) | ((uint)Channel(c.W) << 24);
}
