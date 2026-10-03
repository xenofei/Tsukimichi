using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// The glyph window's Themes tab (1.17, plan v7 T13; docs/design/v7/ui/spec-1.17.md §D, <c>glyph-window.png</c>), for
/// reviewers, the realism supervisor and curious players:
/// <list type="bullet">
/// <item><b>Look A and Look B</b>: each a theme, a palette, one frame kit and a mix (one state at a time), started from
/// the current look; a share code can be pasted into either. Nothing here is saved.</item>
/// <item><b>Two panels</b>: each look's eight medals at 40 px in its one kit, then its eight row medals at 16 px with
/// their names, on the look's palette.</item>
/// <item><b>Two heat tables</b>: how alike every pair of moons is (lower triangle; the build's units, row tier, 16 px,
/// Night), under the chosen vision mode or the worst of them. Cross-set pairs are outlined.</item>
/// <item><b>Ready's lead</b> for both looks: in rows, at 48 px and up, and Completed's share of Ready at 48 px.</item>
/// </list>
/// Every value is a lookup in <see cref="MixTable"/> through <see cref="MixRules"/>; nothing is rendered to measure.
/// </summary>
public sealed partial class GlyphDebugWindow
{
    private static readonly string[] HeatShort = ["Rdy", "RoJ", "Jrn", "Blk", "Done", "Comp", "Lock", "NotC"];
    private static readonly string[] VisionNames = ["Worst", "Grey", "Deut", "Deut (M)", "Prot", "Trit"];
    private static readonly VisionMode?[] VisionModes = [null, VisionMode.Grey, VisionMode.Deut, VisionMode.MachadoDeut, VisionMode.MachadoProt, VisionMode.MachadoTrit];
    private static readonly string[] VisionTips =
    [
        "Each cell in the mode closest to its bar (and the lowest when under 10)",
        "Greyscale (bar 12)",
        "Viénot deuteranopia (bar 12)",
        "Machado deuteranopia (bar 11)",
        "Machado protanopia (bar 11)",
        "Machado tritanopia (bar 11)",
    ];

    // Spec-1.17 §D's heat colours: slate blue reads apart, amber is close, plum is hard to tell apart.
    private static readonly Vector4 HeatClose = new(0xC9 / 255f, 0xA8 / 255f, 0x66 / 255f, 1f);
    private static readonly Vector4 HeatHard = new(0x8E / 255f, 0x3A / 255f, 0x5E / 255f, 1f);
    private static readonly Vector4 HeatEmpty = new(0x2A / 255f, 0x31 / 255f, 0x49 / 255f, 1f);

    private readonly ThemeLook lookA = new("A");
    private readonly ThemeLook lookB = new("B");
    private int vision;

    /// <summary>The saved appearance ("Current look"); null leaves the looks at the default.</summary>
    public Func<AppearanceConfig>? CurrentLook { get; set; }

    private void DrawThemes()
    {
        var current = CurrentLook?.Invoke() ?? new AppearanceConfig();
        lookA.Start(current);
        lookB.Start(current);

        ImGui.TextDisabled("Two looks side by side. Numbers are the build's units (row tier, 16 px, Night); bars: 12 greyscale and deuteranopia, 11 Machado, 10 hard. Nothing here is saved.");
        lookA.DrawControls(current);
        lookB.DrawControls(current);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Vision");
        for (var i = 0; i < VisionNames.Length; i++)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton(VisionNames[i], vision == i))
            {
                vision = i;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(VisionTips[i]);
            }
        }

        ImGui.Spacing();
        var scale = ImGuiHelpers.GlobalScale;
        var avail = ImGui.GetContentRegionAvail().X;
        var gap = 16f * scale;
        var panelWidth = MathF.Max(260f * scale, (avail - gap) * 0.5f);
        var top = ImGui.GetCursorScreenPos();
        var heightA = DrawLookPanel(lookA, top, panelWidth);
        var heightB = DrawLookPanel(lookB, top + new Vector2(panelWidth + gap, 0f), panelWidth);
        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2((panelWidth * 2f) + gap, MathF.Max(heightA, heightB)));

        ImGui.Spacing();
        var mode = VisionModes[vision];
        var tables = ImGui.GetCursorScreenPos();
        var tableA = DrawHeatTable(lookA, mode, tables);
        var tableB = DrawHeatTable(lookB, mode, tables + new Vector2(tableA.X + gap, 0f));
        var leadHeight = DrawLeadBox(tables + new Vector2(tableA.X + gap + tableB.X + gap, 0f));
        ImGui.SetCursorScreenPos(tables);
        ImGui.Dummy(new Vector2(tableA.X + gap + tableB.X + gap + (300f * scale), MathF.Max(MathF.Max(tableA.Y, tableB.Y), leadHeight)));
    }

    /// <summary>A look's panel: its kit and palette over its eight medals at 40 px, then the eight row medals at 16 px with their names.</summary>
    private static float DrawLookPanel(ThemeLook look, Vector2 min, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var resolved = look.Resolved;
        var line = ImGui.GetTextLineHeight();
        var header = line + (10f * scale);
        var hero = 40f;
        var row = 16f;
        var pad = 10f * scale;
        var rowLine = MathF.Max(row, line) + (4f * scale);
        var height = header + pad + hero + pad + (2 * rowLine) + pad;
        var palette = PanelPalette(resolved.Palette);
        var max = min + new Vector2(width, height);
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), 6f * scale);
        dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), 6f * scale);
        var kit = resolved.Classic ? "Classic gauges" : resolved.HighContrast ? "High contrast: one shared set" : $"{FrameKits.Get(resolved.Frames).Name} frames (one kit for the look)";
        dl.AddText(min + new Vector2(pad, 5f * scale), Theme.U32(Theme.Surface.Text), $"{look.Label} · {kit} · {PaletteChoices.Get(resolved.Palette).Name}");

        var paneMin = min + new Vector2(1f, header);
        var paneMax = max - new Vector2(1f, 1f);
        dl.AddRectFilled(paneMin, paneMax, Theme.U32(palette.Surface.Window with { W = 1f }), 0f);
        var states = AppearanceStates.All;
        using (GlyphSeam.PushAppearance(resolved))
        using (Theme.PushPalette(palette, resolved.GlyphPalette))
        {
            var step = MathF.Min(hero + (8f * scale), (width - (pad * 2f)) / states.Count);
            for (var i = 0; i < states.Count; i++)
            {
                var center = new Vector2(MathF.Round(paneMin.X + pad + (step * (i + 0.5f))), MathF.Round(paneMin.Y + pad + (hero * 0.5f)));
                GlyphSeam.Draw(dl, center, RadiusForBox(hero), states[i], 0);
            }

            var column = (width - (pad * 2f)) / 4f;
            var rowsTop = paneMin.Y + pad + hero + pad;
            for (var i = 0; i < states.Count; i++)
            {
                var x = paneMin.X + pad + ((i % 4) * column);
                var mid = rowsTop + ((i / 4) * rowLine) + (rowLine * 0.5f);
                GlyphSeam.Draw(dl, new Vector2(MathF.Round(x + (row * 0.5f)), MathF.Round(mid)), RadiusForBox(row), states[i], 0);
                var name = Strings.StateName(states[i]);
                Chrome.EllipsisTextAt(dl, new Vector2(x + row + (6f * scale), mid - (line * 0.5f)), MathF.Max(1f, column - row - (10f * scale)), name, Theme.U32(palette.Surface.Text));
            }
        }

        return height;
    }

    /// <summary>A look's heat table: the lower triangle of every pair, coloured by the bars, cross-set pairs outlined. Returns its size.</summary>
    private static Vector2 DrawHeatTable(ThemeLook look, VisionMode? mode, Vector2 min)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var cell = new Vector2(46f * scale, MathF.Max(24f * scale, line + (6f * scale)));
        var labelWidth = 42f * scale;
        var title = line + (6f * scale);
        var count = AppearanceStates.Count;
        var size = new Vector2(labelWidth + (count * cell.X), title + ((count + 1) * cell.Y));
        var column = look.Column;
        var modeName = mode is null ? "worst of every vision mode" : VisionNames[Array.IndexOf(VisionModes, mode)];
        dl.AddText(min, Theme.U32(Theme.Surface.Text), $"{look.Label} · how alike, {modeName}");
        if (!MixRules.Applies(look.Resolved))
        {
            dl.AddText(min + new Vector2(0f, title), Theme.U32(Theme.Surface.TextSecondary), look.Resolved.HighContrast ? "High contrast draws one shared set." : "Classic is not measured.");
            return new Vector2(size.X, title + line);
        }

        var origin = min + new Vector2(0f, title);
        var dim = Theme.U32(Theme.Surface.TextSecondary);
        for (var j = 0; j < count; j++)
        {
            var w = ImGui.CalcTextSize(HeatShort[j]).X;
            dl.AddText(origin + new Vector2(labelWidth + (j * cell.X) + ((cell.X - w) * 0.5f), (cell.Y - line) * 0.5f), dim, HeatShort[j]);
        }

        var states = AppearanceStates.All;
        for (var i = 0; i < count; i++)
        {
            var rowY = origin.Y + ((i + 1) * cell.Y);
            dl.AddText(new Vector2(origin.X, rowY + ((cell.Y - line) * 0.5f)), dim, HeatShort[i]);
            for (var j = 0; j < i; j++)
            {
                var cellMin = new Vector2(origin.X + labelWidth + (j * cell.X) + (1f * scale), rowY + (1f * scale));
                var cellMax = cellMin + cell - new Vector2(2f * scale);
                if (!MixRules.TryHeat(column, states[i], states[j], mode, out var heat))
                {
                    dl.AddRectFilled(cellMin, cellMax, Theme.U32(HeatEmpty), 2f * scale);
                    dl.AddText(cellMin + new Vector2(4f * scale, (cell.Y - line) * 0.5f), dim, "–");
                    continue;
                }

                dl.AddRectFilled(cellMin, cellMax, Theme.U32(HeatColor(heat)), 2f * scale);
                if (heat.CrossSet)
                {
                    dl.AddRect(cellMin, cellMax, Theme.WithAlpha(Theme.GoldLine, 0.85f), 2f * scale, ImDrawFlags.None, MathF.Max(1f, scale));
                }

                var text = heat.Value.ToString("0.0", CultureInfo.InvariantCulture);
                var tw = ImGui.CalcTextSize(text).X;
                // The palette's text or window ink, whichever reads better on the cell (dark on amber, light on slate and plum).
                var fill = HeatColor(heat);
                var light = Theme.Surface.Text;
                var dark = Theme.Surface.Window with { W = 1f };
                var ink = ColorMath.Contrast(light, fill) >= ColorMath.Contrast(dark, fill) ? light : dark;
                dl.AddText(new Vector2(cellMin.X + ((cellMax.X - cellMin.X - tw) * 0.5f), cellMin.Y + ((cellMax.Y - cellMin.Y - line) * 0.5f)), Theme.U32(ink), text);
                if (ImGui.IsMouseHoveringRect(cellMin, cellMax))
                {
                    ImGui.SetTooltip(HeatTooltip(column, states[i], states[j], heat));
                }
            }
        }

        return size;
    }

    /// <summary>Ready's lead for both looks: rows at 16 px, 48 px and up, and Completed's share of Ready at 48 px; a miss in amber.</summary>
    private float DrawLeadBox(Vector2 min)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var pad = 10f * scale;
        var width = 300f * scale;
        var rowHeight = line + (8f * scale);
        var legend = new (Vector4? Swatch, string Text)[]
        {
            (HeatColor(new HeatCell(30f, VisionMode.Grey, PairReading.Apart, false)), "reads apart"),
            (HeatClose, "close: under its mode's bar (12 grey and deut, 11 Machado)"),
            (HeatHard, "hard to tell apart: under 10 in any mode"),
            (null, "Outlined cells pair two sets. A pair within one set reads as the gates that set passed judged it."),
        };
        var legendHeight = 0f;
        foreach (var (_, text) in legend)
        {
            legendHeight += TextFlow.Height(text, width - (pad * 2f) - (18f * scale)) + (4f * scale);
        }

        var height = pad + line + (4f * scale) + (4 * rowHeight) + pad + legendHeight + pad;
        var max = min + new Vector2(width, height);
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Sunken), 6f * scale);
        dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), 6f * scale);
        var x = min.X + pad;
        var y = min.Y + pad;
        dl.AddText(new Vector2(x, y), Theme.U32(Theme.Surface.Text), "Ready's lead");
        y += line + (4f * scale);
        var colA = min.X + width - pad - (110f * scale);
        var colB = min.X + width - pad - (50f * scale);
        var a = MixRules.Evaluate(lookA.Column);
        var b = MixRules.Evaluate(lookB.Column);
        var dim = Theme.U32(Theme.Surface.TextSecondary);
        dl.AddText(new Vector2(colA, y), dim, "A");
        dl.AddText(new Vector2(colB, y), dim, "B");
        y += rowHeight;
        var rows = new (string Label, Func<MixVerdict, float> Value, Func<float, bool> Misses, string Format)[]
        {
            ("Rows, 16 px", static v => v.Lead(MixTier.Row), static f => MixTable.RatioUnder(f, MixTable.ReadyLeadBar), "0.00×"),
            ("48 px and up", static v => v.Lead(MixTier.Hero), static f => MixTable.RatioUnder(f, MixTable.ReadyLeadBar), "0.00×"),
            ("Completed of Ready, 48 px", static v => v.CompletedOfReady(MixTier.Hero), static f => MixTable.RatioOver(f, MixTable.CompletedOfReadyBar), "0.00"),
        };

        foreach (var (label, value, misses, format) in rows)
        {
            dl.AddText(new Vector2(x, y), Theme.U32(Theme.Surface.Text), label);
            DrawLeadValue(dl, new Vector2(colA, y), lookA, a, value, misses, format);
            DrawLeadValue(dl, new Vector2(colB, y), lookB, b, value, misses, format);
            y += rowHeight;
        }

        y += pad;
        foreach (var (swatch, text) in legend)
        {
            var textX = x;
            if (swatch is { } color)
            {
                var s = 10f * scale;
                dl.AddRectFilled(new Vector2(x, y + ((line - s) * 0.5f)), new Vector2(x + s, y + ((line + s) * 0.5f)), Theme.U32(color), 2f * scale);
                textX += 18f * scale;
            }

            var textWidth = width - (pad * 2f) - (18f * scale);
            TextFlow.DrawClamped(dl, new Vector2(textX, y), text, textWidth, 4, dim);
            y += TextFlow.Height(text, textWidth) + (4f * scale);
        }

        return height;
    }

    private static void DrawLeadValue(ImDrawListPtr dl, Vector2 at, ThemeLook look, MixVerdict verdict, Func<MixVerdict, float> value, Func<float, bool> misses, string format)
    {
        if (!MixRules.Applies(look.Resolved) || !verdict.Measured)
        {
            dl.AddText(at, Theme.U32(Theme.Surface.TextSecondary), "–");
            return;
        }

        var v = value(verdict);
        dl.AddText(at, Theme.U32(misses(v) ? HeatClose : Theme.Surface.Text), v.ToString(format, CultureInfo.InvariantCulture));
    }

    /// <summary>A cell's tooltip: the two moons, their sets, and the pair's value in every mode against its bar.</summary>
    private static string HeatTooltip(IReadOnlyList<GlyphSetId> column, QuestState a, QuestState b, HeatCell heat)
    {
        var setA = column[AppearanceStates.Index(a)];
        var setB = column[AppearanceStates.Index(b)];
        var lines = new List<string>
        {
            $"{Strings.StateName(a)} ({GlyphSets.Get(setA).Name}) beside {Strings.StateName(b)} ({GlyphSets.Get(setB).Name})",
            heat.CrossSet ? "Two sets: the mix table warns under a bar." : "One set: it passed this set's gates; the mix never warns about it.",
        };

        for (var i = 1; i < VisionModes.Length; i++)
        {
            var m = VisionModes[i]!.Value;
            if (MixTable.TryPair(setA, a, setB, b, m, out var v))
            {
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{VisionNames[i]}: {v:0.00} (bar {MixTable.CloseBar(m):0})"));
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>The heat colour: slate blue deepening with distance over the bar, amber when close, plum when hard.</summary>
    private static Vector4 HeatColor(HeatCell heat) => heat.Reading switch
    {
        PairReading.Hard => HeatHard,
        PairReading.Close => HeatClose,
        _ => SlateFor(heat.Value),
    };

    private static Vector4 SlateFor(float value)
    {
        var t = Math.Clamp((value - 12f) / 30f, 0f, 1f);
        return new Vector4((46f + (t * 10f)) / 255f, (64f + (t * 40f)) / 255f, (96f + (t * 50f)) / 255f, 1f);
    }

    /// <summary>The palette a look panel draws on: the look's own (Night for Follow Dalamud, which has no fixed colours here).</summary>
    private static Core.Ui.Themes.UiPalette PanelPalette(PaletteId id) => UiPalettes.Get(ThemesPage.Drawable(id == PaletteId.FollowDalamud ? PaletteId.Night : id, UiPalettes.IsRegistered));

    /// <summary>The keyline radius that makes a medal's box <paramref name="box"/> px (<see cref="MedalGlyph.Box"/>).</summary>
    private static float RadiusForBox(float box) => box * MedalArt.KeylineRadius / 128f;

    /// <summary>One of the two looks: its appearance (never saved), resolved when it changes, and its controls.</summary>
    private sealed class ThemeLook(string label)
    {
        private static readonly ThemePreset[] Themes = [.. ThemesPage.Themes.Where(static t => !t.Legacy)];
        private static readonly string[] ThemeNames = [.. Themes.Select(static t => t.Name)];
        private static readonly PaletteInfo[] Palettes = [.. PaletteChoices.All.Where(static p => p.Offered)];
        private static readonly string[] PaletteNames = ["From theme", .. Palettes.Select(static p => p.Name)];
        private static readonly FrameKitInfo[] Kits = [.. ThemesPage.Kits];
        private static readonly string[] KitNames = ["From theme", .. Kits.Select(static k => k.Name)];
        private static readonly string[] StateNames = [.. AppearanceStates.All.Select(Strings.StateName)];
        private static readonly string[] SetNames = ["From theme", .. MixRules.Choices.Select(static s => s.Name)];

        private readonly AppearanceCache cache = new();
        private AppearanceConfig? config;
        private int mixState;
        private string code = string.Empty;
        private string codeNote = string.Empty;

        public string Label { get; } = label;

        public ResolvedAppearance Resolved => cache.Get(config);

        public GlyphSetId[] Column => MixRules.Column(Resolved);

        /// <summary>Starts from <paramref name="current"/> the first time the tab draws.</summary>
        public void Start(AppearanceConfig current) => config ??= current.Clone();

        public void DrawControls(AppearanceConfig current)
        {
            var scale = ImGuiHelpers.GlobalScale;
            var look = config ??= current.Clone();
            var resolved = Resolved;
            using var id = ImRaii.PushId(Label);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"Look {Label}");

            ImGui.SameLine();
            var theme = Math.Max(0, Array.FindIndex(Themes, t => t.Id == resolved.Theme.Id));
            ImGui.SetNextItemWidth(170f * scale);
            if (ImGui.Combo("##theme", ref theme, ThemeNames))
            {
                // A theme brings its palette and frames, as on the Themes page; high contrast stays.
                AppearanceEdits.ApplyTheme(look, Themes[theme]);
            }

            ImGui.SameLine();
            var palette = look.Palette is null ? 0 : 1 + Math.Max(0, Array.FindIndex(Palettes, p => p.Id == resolved.Palette));
            ImGui.SetNextItemWidth(130f * scale);
            if (ImGui.Combo("##palette", ref palette, PaletteNames))
            {
                AppearanceEdits.SetPalette(look, palette == 0 ? null : Palettes[palette - 1]);
            }

            ImGui.SameLine();
            var kit = look.Frames is null ? 0 : 1 + Math.Max(0, Array.FindIndex(Kits, k => k.Id == resolved.Frames));
            ImGui.SetNextItemWidth(110f * scale);
            if (ImGui.Combo("##frames", ref kit, KitNames))
            {
                AppearanceEdits.SetFrames(look, kit == 0 ? null : Kits[kit - 1]);
            }

            // The mix, one state at a time.
            ImGui.SameLine();
            ImGui.SetNextItemWidth(140f * scale);
            ImGui.Combo("##mixState", ref mixState, StateNames);
            ImGui.SameLine();
            var state = AppearanceStates.All[mixState];
            var picked = ShareCode.OwnPick(look, state);
            var set = picked == 0 ? 0 : 1 + Math.Max(0, IndexOfChoice(picked));
            ImGui.SetNextItemWidth(170f * scale);
            if (ImGui.Combo("##mixSet", ref set, SetNames))
            {
                AppearanceEdits.SetGlyph(look, state, set == 0 ? null : MixRules.Choices[set - 1]);
            }

            ImGui.SameLine();
            if (ImGui.Button("Current look"))
            {
                config = current.Clone();
                code = string.Empty;
                codeNote = string.Empty;
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(170f * scale);
            if (ImGui.InputTextWithHint("##code", "Paste a code (TM1-…)", ref code, ShareCode.MaxTextLength))
            {
                var preview = SharePreview.Of(code, look);
                if (preview.Result is { } result)
                {
                    config = result.Clone();
                    codeNote = "applied to this look";
                }
                else
                {
                    codeNote = code.Length == 0 ? string.Empty : "doesn't read yet";
                }
            }

            if (codeNote.Length > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(codeNote);
            }
        }

        private static int IndexOfChoice(GlyphSetId set)
        {
            var choices = MixRules.Choices;
            for (var i = 0; i < choices.Count; i++)
            {
                if (choices[i].Id == set)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
