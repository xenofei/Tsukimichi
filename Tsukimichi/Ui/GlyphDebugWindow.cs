using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// Developer window that renders every state glyph at the radii the glyph rules gate on, plus the filling moon at
/// the fractions that matter, on a Night panel, with a slider to try any radius. Opened with <c>/tsukimichi glyphs</c>.
/// Radii are device pixels, not logical sizes, because the rim, ring, notch and shading gates are pixel rules.
///
/// The "Simulate" combo re-colours everything the panel draws through a colour-vision-deficiency simulation
/// (Machado, Oliveira and Fernandes 2009 at severity 1.0 in linear sRGB; greyscale is WCAG relative luminance —
/// docs/review/panel/accessibility.md Appendix C) so the owner can check that all eight states differ without a
/// legend (accessibility §2.3 rule 1). The simulation lives here only: after the panel is drawn its draw list's
/// vertex colours are rewritten, so <see cref="Theme"/> and <see cref="MoonGlyph"/> stay untouched.
/// </summary>
public sealed class GlyphDebugWindow : Window
{
    private enum Simulation
    {
        None,
        Greyscale,
        Deuteranopia,
        Protanopia,
        Tritanopia,
    }

    private static readonly string[] SimulationNames = ["None", "Greyscale", "Deuteranopia", "Protanopia", "Tritanopia"];

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

    // sRGB → linear lookup for the 256 channel values; the encode side is a pow per channel, cached per colour.
    private static readonly float[] ToLinear = BuildToLinear();

    // Machado 2009, severity 1.0, row-major, applied to linear RGB.
    private static readonly float[] Deuteranopia = [0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f];
    private static readonly float[] Protanopia = [0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f];
    private static readonly float[] Tritanopia = [1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f];

    private float testRadius = 24f;
    private bool nightPanel = true;
    private int simulation;

    /// <summary>Packed colour → simulated packed colour for the current mode; the panel draws a few dozen distinct colours.</summary>
    private readonly Dictionary<uint, uint> simulated = new();
    private int simulatedMode;

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

        using var colors = Theme.PushNightPanel(nightPanel);
        using var panel = ImRaii.Child("##glyphPanel", new Vector2(-1f, -1f), true);
        if (!panel) return;

        DrawStates();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawFilling();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawInlineRow();

        // Everything the child drew this frame, background included, is in its own draw list.
        ApplySimulation(ImGui.GetWindowDrawList());
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
            ImGui.TextDisabled(Strings.StateGlyphSubtitle(state) + " · " + phase);

            foreach (var (radius, _) in Radii)
            {
                ImGui.TableNextColumn();
                GlyphCell(radius, state);
            }

            ImGui.TableNextColumn();
            GlyphCell(testRadius, state);
        }
    }

    private void DrawFilling()
    {
        ImGui.TextUnformatted("Filling moon (tree progress)");
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
            MoonGlyph.DrawFillingInline(f, lineHeight);
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

    /// <summary>Rewrites every vertex colour in <paramref name="dl"/> through the selected simulation.</summary>
    private void ApplySimulation(ImDrawListPtr dl)
    {
        var mode = (Simulation)simulation;
        if (mode == Simulation.None) return;

        if (simulatedMode != simulation)
        {
            simulated.Clear();
            simulatedMode = simulation;
        }

        var vertices = dl.VtxBuffer;
        for (var i = 0; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            vertex.Col = SimulatePacked(vertex.Col, mode);
            vertices[i] = vertex;
        }
    }

    private uint SimulatePacked(uint packed, Simulation mode)
    {
        if (simulated.TryGetValue(packed, out var cached)) return cached;

        var color = Unpack(packed);
        var result = Pack(Simulate(color, mode));
        simulated[packed] = result;
        return result;
    }

    /// <summary>The colour as a viewer with the given deficiency sees it; alpha is untouched.</summary>
    private static Vector4 Simulate(Vector4 color, Simulation mode)
    {
        var r = ToLinear[Channel(color.X)];
        var g = ToLinear[Channel(color.Y)];
        var b = ToLinear[Channel(color.Z)];

        float r2, g2, b2;
        switch (mode)
        {
            case Simulation.Greyscale:
                r2 = g2 = b2 = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                break;
            case Simulation.Deuteranopia:
                (r2, g2, b2) = Multiply(Deuteranopia, r, g, b);
                break;
            case Simulation.Protanopia:
                (r2, g2, b2) = Multiply(Protanopia, r, g, b);
                break;
            case Simulation.Tritanopia:
                (r2, g2, b2) = Multiply(Tritanopia, r, g, b);
                break;
            default:
                return color;
        }

        return new Vector4(ToSrgb(r2), ToSrgb(g2), ToSrgb(b2), color.W);
    }

    private static (float R, float G, float B) Multiply(float[] m, float r, float g, float b) => (
        m[0] * r + m[1] * g + m[2] * b,
        m[3] * r + m[4] * g + m[5] * b,
        m[6] * r + m[7] * g + m[8] * b);

    private static float[] BuildToLinear()
    {
        var table = new float[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255f;
            table[i] = c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        return table;
    }

    private static float ToSrgb(float linear)
    {
        linear = Math.Clamp(linear, 0f, 1f);
        return linear <= 0.0031308f ? 12.92f * linear : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
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
