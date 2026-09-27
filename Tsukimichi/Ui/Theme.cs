using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// Color tokens (spec §2.3). Pushed only where the default Dalamud style is insufficient: glyphs, badges and the
/// Night panels. Everything else keeps the user's style so the window does not look foreign.
/// </summary>
public static class Theme
{
    /// <summary>#0F1424 – panel backgrounds for the detail pane and path view.</summary>
    public static readonly Vector4 Night = Rgb(0x0F1424);

    /// <summary>#F2D27A – Completed, Ready, gold accents, progress fill.</summary>
    public static readonly Vector4 Moon = Rgb(0xF2D27A);

    /// <summary>#DDE3F0 – primary text on Night, silver glyphs.</summary>
    public static readonly Vector4 Silver = Rgb(0xDDE3F0);

    /// <summary>#7C86A8 – secondary text, rings, separators.</summary>
    public static readonly Vector4 Dusk = Rgb(0x7C86A8);

    /// <summary>#B25C7F – Foreclosed, destructive actions.</summary>
    public static readonly Vector4 Eclipse = Rgb(0xB25C7F);

    /// <summary>#4A5270 – disabled, unknown.</summary>
    public static readonly Vector4 Veil = Rgb(0x4A5270);

    /// <summary>Dark disc of the unlit glyph states: Night lifted halfway toward Veil (#2C334A) so it stays visible on a Night panel.</summary>
    public static readonly Vector4 UnlitDisc = Vector4.Lerp(Night, Veil, 0.5f);

    public static readonly uint NightU32 = Pack(Night);
    public static readonly uint MoonU32 = Pack(Moon);
    public static readonly uint SilverU32 = Pack(Silver);
    public static readonly uint DuskU32 = Pack(Dusk);
    public static readonly uint EclipseU32 = Pack(Eclipse);
    public static readonly uint VeilU32 = Pack(Veil);
    public static readonly uint UnlitDiscU32 = Pack(UnlitDisc);

    /// <summary>Text color for a state badge next to a glyph.</summary>
    public static Vector4 StateColor(QuestState state) => state switch
    {
        QuestState.Completed => Moon,
        QuestState.Accepted => Moon,
        QuestState.Ready => Moon,
        QuestState.ReadyOnOtherJob => Silver,
        QuestState.DoneThisCycle => Silver,
        QuestState.Blocked => Dusk,
        QuestState.Foreclosed => Eclipse,
        QuestState.Unknown => Veil,
        _ => Dusk,
    };

    public static uint StateColorU32(QuestState state) => Pack(StateColor(state));

    /// <summary>The token with a different alpha, packed for ImDrawList calls.</summary>
    public static uint WithAlpha(Vector4 color, float alpha) => Pack(color with { W = Math.Clamp(alpha, 0f, 1f) });

    public static Vector4 WithAlphaVector(Vector4 color, float alpha) => color with { W = Math.Clamp(alpha, 0f, 1f) };

    /// <summary>
    /// Night panel colors for the detail pane, path view and similar: child background Night, text Silver, secondary
    /// text Dusk, borders and separators Veil. Dispose to pop.
    /// </summary>
    public static ImRaii.ColorDisposable PushNightPanel(bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.ChildBg, Night, condition)
              .Push(ImGuiCol.Text, Silver, condition)
              .Push(ImGuiCol.TextDisabled, Dusk, condition)
              .Push(ImGuiCol.Border, Veil, condition)
              .Push(ImGuiCol.Separator, Veil, condition)
              .Push(ImGuiCol.TableBorderLight, Veil, condition)
              .Push(ImGuiCol.TableBorderStrong, Dusk, condition);

    /// <summary>Eclipse-toned button for destructive actions (Forget character, Delete all data). Dispose to pop.</summary>
    public static ImRaii.ColorDisposable PushDestructiveButton(bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.Button, WithAlphaVector(Eclipse, 0.75f), condition)
              .Push(ImGuiCol.ButtonHovered, Eclipse, condition)
              .Push(ImGuiCol.ButtonActive, Vector4.Lerp(Eclipse, Night, 0.25f) with { W = 1f }, condition)
              .Push(ImGuiCol.Text, Silver, condition);

    /// <summary>Text in the token color, for badges. Dispose to pop.</summary>
    public static ImRaii.ColorDisposable PushText(Vector4 color, bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.Text, color, condition);

    private static Vector4 Rgb(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8) & 0xFF) / 255f,
        (hex & 0xFF) / 255f,
        1f);

    /// <summary>IM_COL32 layout (0xAABBGGRR). Done here rather than through ImGui so the static fields need no ImGui context.</summary>
    private static uint Pack(Vector4 c)
    {
        static uint Channel(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
        return Channel(c.X) | (Channel(c.Y) << 8) | (Channel(c.Z) << 16) | (Channel(c.W) << 24);
    }
}
