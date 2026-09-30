using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Developer window that renders every state glyph at the radii the glyph rules gate on, plus the filling moon at
/// the fractions that matter, on a Night panel, with a slider to try any radius. Opened with <c>/tsukimichi glyphs</c>.
/// Radii are device pixels, not logical sizes, because the rim, ring, notch and shading gates are pixel rules.
///
/// The "Simulate" combo re-colours everything the panel draws through a colour-vision-deficiency simulation
/// (<see cref="ColorVisionSimulation"/>: Machado, Oliveira and Fernandes 2009 at severity 1.0 in linear sRGB; greyscale
/// is WCAG relative luminance — docs/review/panel/accessibility.md Appendix C) so the owner can check that all eight
/// states differ without a legend (accessibility §2.3 rule 1). The simulation lives here only: after the panel is
/// drawn its draw list's vertex colours are rewritten, so <see cref="Theme"/> and <see cref="MoonGlyph"/> stay untouched.
///
/// The "Palettes" strip at the top draws the Standard and the high-contrast glyph palettes (and the high-contrast
/// light variant on a white host) side by side once per simulation, each row re-coloured through its own simulation,
/// so the two palettes can be compared under every condition at a glance. The "Palette" combo picks the palette the
/// tables below are drawn with.
/// </summary>
public sealed class GlyphDebugWindow : Window
{
    private static readonly string[] SimulationNames = Array.ConvertAll(ColorVisionSimulation.All, ColorVisionSimulation.Name);

    /// <summary>What the tables draw with: the palette in effect (Settings), or one forced.</summary>
    private static readonly string[] PaletteNames = ["Settings", "Standard", "High contrast", "High contrast (light host)"];

    /// <summary>The palettes compared side by side, and the host colour each is shown on (null: the panel's own).</summary>
    private static readonly (GlyphPalette Palette, string Label, Vector4? Host)[] Compared =
    [
        (GlyphPalette.Standard, "Standard", null),
        (GlyphPalette.HighContrastDark, "High contrast", null),
        (GlyphPalette.HighContrastLight, "High contrast · light host", new Vector4(1f, 1f, 1f, 1f)),
    ];

    /// <summary>The comparison's states, in the Help legend's order.</summary>
    private static readonly QuestState[] ComparedStates =
    [
        QuestState.Completed, QuestState.Accepted, QuestState.Ready, QuestState.ReadyOnOtherJob,
        QuestState.DoneThisCycle, QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown,
    ];

    private static readonly (QuestState State, string Phase)[] States =
    [
        (QuestState.Completed, "Full moon, no rim, highlight arc from r 16"),
        (QuestState.Accepted, "Early gibbous 60 %, Night seal, Silver rim"),
        (QuestState.Ready, "First quarter, Dusk rim, glow (ring below r 9)"),
        (QuestState.ReadyOnOtherJob, "First quarter, silver, Moon ring"),
        (QuestState.DoneThisCycle, "Waning gibbous, silver, Dusk rim"),
        (QuestState.Blocked, "New moon, Silver ring"),
        (QuestState.Foreclosed, "Eclipse rim, bar, notch from r 12"),
        (QuestState.Unknown, "Veiled, dashed Dusk rim"),
    ];

    /// <summary>Glyph radii in device pixels: each is a gate in <see cref="MoonGlyph"/> or a size the UI draws.</summary>
    private static readonly (float Radius, string Use)[] Radii =
    [
        (6f, "12 px box"),
        (7.5f, "row glyph"),
        (9f, "shading gate"),
        (12f, "notch + detail gate"),
        (16f, "header + arc"),
        (32f, "help"),
    ];

    private static readonly string[] RadiusHeaders = Array.ConvertAll(Radii, static s => $"r {s.Radius:0.#} · {s.Use}");

    /// <summary>Progress fractions: the ends, the first sliver (17/612), both sides of the floor and both sides of the half.</summary>
    private static readonly float[] Fractions = [0f, 0.03f, 0.10f, 0.25f, 0.5f, 0.75f, 0.9f, 0.97f, 1f];

    /// <summary>Halo boxes in device pixels (R = box / 2): number only, track + arc, the tree floor, the tooltip and the help size.</summary>
    private static readonly (float Box, string Use)[] HaloBoxes =
    [
        (12f, "number only"),
        (16f, "track + arc"),
        (24f, "tree floor, core"),
        (32f, "tooltip"),
        (64f, "help"),
    ];

    private static readonly string[] HaloHeaders = Array.ConvertAll(HaloBoxes, static s => $"{s.Box:0} px · {s.Use}");

    private float testRadius = 24f;
    private float compareRadius = 9f;
    private bool nightPanel = true;
    private bool haloOnCard;
    private int simulation;
    private int tablePalette;

    /// <summary>Packed colour → simulated packed colour, one cache per simulation; the panel draws a few dozen distinct colours.</summary>
    private readonly Dictionary<uint, uint>[] simulated = Array.ConvertAll(ColorVisionSimulation.All, static _ => new Dictionary<uint, uint>());

    public GlyphDebugWindow()
        : base("Tsukimichi Glyphs###TsukimichiGlyphs")
    {
        Size = new Vector2(820f, 860f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420f, 320f) };
    }

    /// <summary>Starts the interactive tutorial (the same action the help window and Settings use); null hides the button.</summary>
    public Action? StartTutorial { get; set; }

    public override void Draw()
    {
        ImGui.Checkbox("Night panel", ref nightPanel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("Simulate", ref simulation, SimulationNames);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Test radius", ref testRadius, 4f, 64f, "%.0f px");
        ImGui.SameLine();
        ImGui.TextDisabled($"scale {ImGuiHelpers.GlobalScale:0.00} · radii in device px");
        if (StartTutorial is { } startTutorial)
        {
            ImGui.SameLine();
            if (ImGui.Button("Tutorial preview"))
            {
                startTutorial();
            }
        }

        ImGui.SetNextItemWidth(190f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("Palette (tables)", ref tablePalette, PaletteNames);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Compare radius", ref compareRadius, 6f, 24f, "%.1f px");

        using var colors = Theme.PushNightPanel(nightPanel);
        DrawComparison();

        using var panel = ImRaii.Child("##glyphPanel", new Vector2(-1f, -1f), true);
        if (!panel) return;

        using var glyphs = Theme.PushGlyphs(TablePalette());
        DrawStates();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawHalos();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawFilling();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawInlineRow();

        // Everything the child drew this frame, background included, is in its own draw list.
        var dl = ImGui.GetWindowDrawList();
        ApplySimulation(dl, (ColorVision)simulation, 0, dl.VtxBuffer.Size);
    }

    private GlyphPalette TablePalette() => tablePalette switch
    {
        1 => GlyphPalette.Standard,
        2 => GlyphPalette.HighContrastDark,
        3 => GlyphPalette.HighContrastLight,
        _ => Theme.Glyphs,
    };

    /// <summary>
    /// Both palettes side by side under every simulation: one row per <see cref="ColorVision"/>, each holding the eight
    /// state moons, a halo at 35 % and the check and cross marks per palette, the light high-contrast variant on a
    /// white host swatch. Drawn without a table in a child of its own, so each row's vertices are one contiguous range
    /// of the child's draw list that its simulation re-colours; the panel-wide "Simulate" combo does not touch it.
    /// </summary>
    private void DrawComparison()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = compareRadius;
        var box = MathF.Round(radius * 2.6f);
        var halo = MathF.Max(box, 2f * GaugeGeometry.RingMinRadius);
        var line = ImGui.GetTextLineHeightWithSpacing();
        var labelWidth = 110f * scale;
        var gap = 3f * scale;
        var blockGap = 18f * scale;
        var rowHeight = MathF.Max(box, halo) + 6f * scale;
        var height = line * 2f + ColorVisionSimulation.All.Length * rowHeight + 12f * scale;

        ImGui.TextUnformatted("Palettes side by side (each row under its own simulation)");
        using var child = ImRaii.Child("##glyphCompare", new Vector2(-1f, height), true, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child) return;

        var dl = ImGui.GetWindowDrawList();

        // Header: the palette names over their blocks.
        var blockWidth = ComparedStates.Length * (box + gap) + halo + 2f * (box + gap) + gap;
        ImGui.Dummy(new Vector2(labelWidth, line));
        for (var b = 0; b < Compared.Length; b++)
        {
            ImGui.SameLine(labelWidth + b * (blockWidth + blockGap));
            ImGui.TextUnformatted(Compared[b].Label);
        }

        foreach (var mode in ColorVisionSimulation.All)
        {
            var rowStart = dl.VtxBuffer.Size;
            var rowPos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(labelWidth, rowHeight));
            dl.AddText(rowPos + new Vector2(0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), ColorVisionSimulation.Name(mode));

            for (var b = 0; b < Compared.Length; b++)
            {
                var (palette, _, host) = Compared[b];
                var x = rowPos.X + labelWidth + b * (blockWidth + blockGap);
                if (host is { } hostColor)
                {
                    dl.AddRectFilled(new Vector2(x - gap, rowPos.Y), new Vector2(x + blockWidth, rowPos.Y + rowHeight - 2f * scale), Theme.U32(hostColor), 4f * scale);
                }

                using var glyphs = Theme.PushGlyphs(palette);
                var midY = rowPos.Y + rowHeight * 0.5f;
                foreach (var state in ComparedStates)
                {
                    MoonGlyph.Draw(dl, new Vector2(x + box * 0.5f, midY), radius, state);
                    x += box + gap;
                }

                MoonGlyph.DrawHalo(dl, new Vector2(x + halo * 0.5f, midY), halo * 0.5f, 0.35f);
                x += halo + gap;
                Marks.Draw(dl, new Vector2(x + box * 0.5f, midY), box, Mark.Check);
                x += box + gap;
                Marks.Draw(dl, new Vector2(x + box * 0.5f, midY), box, Mark.Cross);
            }

            // Reserve the row's width so the child scrolls horizontally when the three blocks do not fit.
            ImGui.SameLine(labelWidth + Compared.Length * (blockWidth + blockGap));
            ImGui.Dummy(new Vector2(1f, rowHeight));
            ApplySimulation(dl, mode, rowStart, dl.VtxBuffer.Size);
        }
    }

    private void DrawStates()
    {
        ImGui.TextUnformatted("Quest states");
        using var table = ImRaii.Table("##states", 2 + Radii.Length + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH);
        if (!table) return;

        ImGui.TableSetupColumn("State");
        ImGui.TableSetupColumn("Phase");
        foreach (var header in RadiusHeaders) ImGui.TableSetupColumn(header);
        ImGui.TableSetupColumn("Slider");
        ImGui.TableHeadersRow();

        foreach (var (state, phase) in States)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            using (Theme.PushText(Theme.StateColor(state)))
                ImGui.TextUnformatted(Strings.StateName(state));
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            var palette = Theme.Glyphs;
            ImGui.TextDisabled(palette.HighContrast
                ? $"{StateNames.HighContrastSubtitle(state)} · {palette.Style(state).Mark}, rim {palette.Style(state).RimWidth(7.5f):0.#} px at r 7.5"
                : Strings.StateGlyphSubtitle(state) + " · " + phase);

            foreach (var (radius, _) in Radii)
            {
                ImGui.TableNextColumn();
                GlyphCell(radius, state);
            }

            ImGui.TableNextColumn();
            GlyphCell(testRadius, state);
        }
    }

    private void DrawHalos()
    {
        ImGui.TextUnformatted("Halo gauge (progress)");
        ImGui.SameLine();
        ImGui.Checkbox("On a card (Dusk 80 % track)", ref haloOnCard);
        using var table = ImRaii.Table("##halos", 1 + HaloBoxes.Length + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH);
        if (!table) return;

        ImGui.TableSetupColumn("Fraction");
        foreach (var header in HaloHeaders) ImGui.TableSetupColumn(header);
        ImGui.TableSetupColumn("Slider (box = 2 r)");
        ImGui.TableHeadersRow();

        foreach (var f in Fractions)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{f:0.00}");

            foreach (var (box, _) in HaloBoxes)
            {
                ImGui.TableNextColumn();
                HaloCell(box, f, haloOnCard);
            }

            ImGui.TableNextColumn();
            HaloCell(testRadius * 2f, f, haloOnCard);
        }

        // The Journal tree's complete node: MoonDim ring and flat core, beside the bright 1.00 row above.
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("1.00 tree");
        foreach (var (box, _) in HaloBoxes)
        {
            ImGui.TableNextColumn();
            HaloCell(box, 1f, haloOnCard, dimComplete: true);
        }

        ImGui.TableNextColumn();
        HaloCell(testRadius * 2f, 1f, haloOnCard, dimComplete: true);
    }

    /// <summary>A halo at exactly <paramref name="box"/> px with the number beside it where the rules ask for one.</summary>
    private static void HaloCell(float box, float fraction, bool onCard, bool dimComplete = false)
    {
        var mode = MoonGlyph.DrawHaloInline(fraction, box, onCard, dimComplete);
        if (mode == HaloMode.Core) return;
        ImGui.SameLine(0f, 4f);
        ImGui.TextDisabled($"{fraction:P0}");
    }

    private void DrawFilling()
    {
        ImGui.TextUnformatted("Filling moon (legacy progress glyph, for comparison)");
        using var table = ImRaii.Table("##filling", 1 + Radii.Length + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH);
        if (!table) return;

        ImGui.TableSetupColumn("Fraction");
        foreach (var header in RadiusHeaders) ImGui.TableSetupColumn(header);
        ImGui.TableSetupColumn("Slider");
        ImGui.TableHeadersRow();

        foreach (var f in Fractions)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{f:0.00}");

            foreach (var (radius, _) in Radii)
            {
                ImGui.TableNextColumn();
                FillingCell(radius, f);
            }

            ImGui.TableNextColumn();
            FillingCell(testRadius, f);
        }
    }

    /// <summary>The inline helpers as a table row would use them, at text line height, with a state badge after each.</summary>
    private static void DrawInlineRow()
    {
        ImGui.TextUnformatted("Inline at line height");
        var lineHeight = ImGui.GetTextLineHeight();
        foreach (var (state, _) in States)
        {
            MoonGlyph.DrawInline(state, lineHeight);
            ImGui.SameLine();
            using (Theme.PushText(Theme.StateColor(state)))
                ImGui.TextUnformatted(state.ToString());
            ImGui.SameLine(0f, 12f * ImGuiHelpers.GlobalScale);
        }

        ImGui.NewLine();
        foreach (var f in Fractions)
        {
            MoonGlyph.DrawHaloInline(f, lineHeight);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{f:0.00}");
            ImGui.SameLine(0f, 12f * ImGuiHelpers.GlobalScale);
        }

        ImGui.NewLine();
    }

    /// <summary>Reserves a square with room for the glow (1.7 r) and draws the glyph at exactly <paramref name="radius"/>.</summary>
    private static void GlyphCell(float radius, QuestState state)
    {
        var box = radius * 3.4f;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        MoonGlyph.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(box * 0.5f), radius, state);
    }

    private static void FillingCell(float radius, float fraction)
    {
        var box = radius * 2.4f;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        MoonGlyph.DrawFilling(ImGui.GetWindowDrawList(), pos + new Vector2(box * 0.5f), radius, fraction);
    }

    // ------------------------------------------------------------------ colour-vision simulation

    /// <summary>
    /// Rewrites the vertex colours <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive) of
    /// <paramref name="dl"/> through <paramref name="mode"/> (<see cref="ColorVisionSimulation"/>).
    /// </summary>
    private void ApplySimulation(ImDrawListPtr dl, ColorVision mode, int from, int to)
    {
        if (mode == ColorVision.None || (uint)mode >= (uint)simulated.Length) return;

        var cache = simulated[(int)mode];
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
        if (cache.TryGetValue(packed, out var cached)) return cached;

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

