using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// Developer window that renders every state glyph at the four sizes the UI uses plus the filling moon, on a Night
/// panel, with a slider to try any radius. Opened with <c>/tsukimichi glyphs</c>. The 12 px column stays flat; from
/// 20 px up the shading and highlight arc (<see cref="MoonGlyph.ShadingMinRadius"/>) are visible.
/// </summary>
public sealed class GlyphDebugWindow : Window
{
    private static readonly (QuestState State, string Phase)[] States =
    [
        (QuestState.Completed, "Full moon"),
        (QuestState.Accepted, "Waxing gibbous, ring"),
        (QuestState.Ready, "First quarter, glow"),
        (QuestState.ReadyOnOtherJob, "First quarter, silver, gold ring"),
        (QuestState.DoneThisCycle, "Waning gibbous, silver"),
        (QuestState.Blocked, "New moon, silver ring"),
        (QuestState.Foreclosed, "Eclipsed"),
        (QuestState.Unknown, "Veiled"),
    ];

    /// <summary>Glyph diameters in unscaled pixels and where the UI draws them.</summary>
    private static readonly (float Diameter, string Use)[] Sizes =
    [
        (12f, "table row"),
        (20f, "toolbar"),
        (40f, "detail header"),
        (64f, "help"),
    ];

    private static readonly string[] SizeHeaders = Array.ConvertAll(Sizes, static s => $"{s.Diameter:0} px · {s.Use}");

    private static readonly float[] Fractions = [0f, 0.25f, 0.5f, 0.75f, 1f];

    private float testRadius = 24f;
    private bool nightPanel = true;

    public GlyphDebugWindow()
        : base("Tsukimichi Glyphs###TsukimichiGlyphs")
    {
        Size = new Vector2(760f, 720f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420f, 320f) };
    }

    public override void Draw()
    {
        ImGui.Checkbox("Night panel", ref nightPanel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Test radius", ref testRadius, 4f, 64f, "%.0f px");
        ImGui.SameLine();
        ImGui.TextDisabled($"scale {ImGuiHelpers.GlobalScale:0.00}");

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
    }

    private void DrawStates()
    {
        ImGui.TextUnformatted("Quest states");
        using var table = ImRaii.Table("##states", 2 + Sizes.Length + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH);
        if (!table) return;

        ImGui.TableSetupColumn("State");
        ImGui.TableSetupColumn("Phase");
        foreach (var header in SizeHeaders) ImGui.TableSetupColumn(header);
        ImGui.TableSetupColumn("Slider");
        ImGui.TableHeadersRow();

        foreach (var (state, phase) in States)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            using (Theme.PushText(Theme.StateColor(state)))
                ImGui.TextUnformatted(state.ToString());
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(phase);

            foreach (var (diameter, _) in Sizes)
            {
                ImGui.TableNextColumn();
                GlyphCell(diameter * 0.5f * ImGuiHelpers.GlobalScale, state);
            }

            ImGui.TableNextColumn();
            GlyphCell(testRadius * ImGuiHelpers.GlobalScale, state);
        }
    }

    private void DrawFilling()
    {
        ImGui.TextUnformatted("Filling moon (tree progress)");
        using var table = ImRaii.Table("##filling", 1 + Sizes.Length + 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH);
        if (!table) return;

        ImGui.TableSetupColumn("Fraction");
        foreach (var header in SizeHeaders) ImGui.TableSetupColumn(header);
        ImGui.TableSetupColumn("Slider");
        ImGui.TableHeadersRow();

        foreach (var f in Fractions)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{f:0.00}");

            foreach (var (diameter, _) in Sizes)
            {
                ImGui.TableNextColumn();
                FillingCell(diameter * 0.5f * ImGuiHelpers.GlobalScale, f);
            }

            ImGui.TableNextColumn();
            FillingCell(testRadius * ImGuiHelpers.GlobalScale, f);
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

    /// <summary>Reserves a square with room for the glow (1.6 r) and draws the glyph at exactly <paramref name="radius"/>.</summary>
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
}
